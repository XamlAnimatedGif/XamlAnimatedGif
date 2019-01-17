// Parts of this source file is derived from Chromium's Android GifPlayer
// as seen here (https://github.com/chromium/chromium/blob/master/third_party/gif_player/src/jp/tomorrowkey/android/gifplayer)

// Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
// Copyright (C) 2015 The Gifplayer Authors. All Rights Reserved.

// The rest of the source file is licensed under MIT License.
// Copyright (C) 2018 Jumar A. Macato, All Rights Reserved.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Media.Imaging;
using static AvaloniaGif.Decoding.StreamExtensions;
using System.Threading.Tasks;
using AvaloniaGif.Caching;

namespace AvaloniaGif.Decoding
{
    public sealed class GifDecoder : IDisposable
    {
        private static readonly ReadOnlyMemory<byte> G87AMagic
            = Encoding.ASCII.GetBytes("GIF87a").AsMemory();
        private static readonly ReadOnlyMemory<byte> G89AMagic
            = Encoding.ASCII.GetBytes("GIF89a").AsMemory();
        private static readonly ReadOnlyMemory<byte> NetscapeMagic
            = Encoding.ASCII.GetBytes("NETSCAPE2.0").AsMemory();

        private static readonly TimeSpan FrameDelayThreshold = TimeSpan.FromMilliseconds(10);
        private static readonly TimeSpan FrameDelayDefault = TimeSpan.FromMilliseconds(100);
        private static readonly GifColor TransparentColor = new GifColor(0, 0, 0, 0);
        private static readonly XXHash64 hasher = new XXHash64();
        private static readonly int MaxTempBuf = 768;
        private static readonly int MaxStackSize = 4096;
        private static readonly int MaxBits = 4097;

        private int _iterationCount, _gctSize, _bgIndex, _prevFrame;
        private Int32Rect _gifRect;
        private bool _gctUsed;
        private readonly Stream _fileStream;
        private GifHeader _gifHeader;
        private ulong _globalColorTable;
        public GifHeader Header => _gifHeader;
        public List<GifFrame> Frames = new List<GifFrame>();
        private readonly Mutex renderMutex = new Mutex();
        private readonly GifColor[] _bBuf;
        private readonly int _backBufferBytes;

        private readonly short[] _prefixBuf;
        private readonly byte[] _suffixBuf;
        private readonly byte[] _pixelStack;
        private readonly byte[] _indexBuf;
        private readonly byte[] _prevFrameIndexBuf;
        internal volatile bool _hasNewFrame;

        private bool firstFrame = true;

        private static ICache<ulong, GifColor[]> colorCache
            = Caches.KeyValue<ulong, GifColor[]>()
              .WithBackgroundPurge(TimeSpan.FromSeconds(5))
              .WithExpiration(TimeSpan.FromSeconds(10))
              .WithSlidingExpiration()
              .Build();

        public GifDecoder(Stream fileStream)
        {
            _fileStream = fileStream;

            ProcessHeaderData();
            ProcessFrameData();

            var pixelCount = _gifRect.TotalPixels;

            _bBuf = ArrayPool<GifColor>.Shared.Rent(pixelCount);
            _indexBuf = ArrayPool<byte>.Shared.Rent(pixelCount);
            _prevFrameIndexBuf = ArrayPool<byte>.Shared.Rent(pixelCount);

            _prefixBuf = ArrayPool<short>.Shared.Rent(MaxStackSize);
            _suffixBuf = ArrayPool<byte>.Shared.Rent(MaxStackSize);
            _pixelStack = ArrayPool<byte>.Shared.Rent(MaxStackSize + 1);

            _backBufferBytes = pixelCount * Marshal.SizeOf(typeof(GifColor));

            Array.Fill<short>(_prefixBuf, 0);
            Array.Fill<byte>(_suffixBuf, 0);
            Array.Fill<byte>(_indexBuf, 0);
            Array.Fill<byte>(_prevFrameIndexBuf, 0);
        }

        public void Dispose()
        {
            renderMutex.WaitOne();

            ArrayPool<GifColor>.Shared.Return(_bBuf);
            ArrayPool<short>.Shared.Return(_prefixBuf);
            ArrayPool<byte>.Shared.Return(_suffixBuf);
            ArrayPool<byte>.Shared.Return(_pixelStack);
            ArrayPool<byte>.Shared.Return(_indexBuf);
            ArrayPool<byte>.Shared.Return(_prevFrameIndexBuf);

            _fileStream?.Dispose();
            renderMutex.ReleaseMutex();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int PixCoord(int x, int y) => x + (y * _gifRect.Width);

        /*
         * 4 passes:
         * Pass 1: rows 0, 8, 16, 24...
         * Pass 2: rows 4, 12, 20, 28...
         * Pass 3: rows 2, 6, 10, 14...
         * Pass 4: rows 1, 3, 5, 7...
         * */
        static readonly (int Start, int Step)[] passes =
        {
            (0, 8),
            (4, 8),
            (2, 4),
            (1, 2)
        };

        private static readonly Action<int, Action<int>> InterlaceRows = (height, RowAction) =>
        {
            for (int i = 0; i < 4; i++)
            {
                var pass = passes[i];
                var y = pass.Start;
                while (y < height)
                {
                    RowAction(y);
                    y += pass.Step;
                }
            }
        };

        private static readonly Action<int, Action<int>> NormalRows = (height, RowAction) =>
        {
            for (int i = 0; i < height; i++)
            {
                RowAction(i);
            }
        };

        public void RenderFrame(int fIndex)
        {
            renderMutex.WaitOne();
            try
            {
                if (fIndex < 0 | fIndex >= Frames.Count)
                    return;

                if (fIndex == 0)
                    ClearArea(_gifRect);

                var tmpB = ArrayPool<byte>.Shared.Rent(MaxTempBuf);

                var curFrame = Frames[fIndex];

                DisposePreviousFrame();
                DecompressFrameToIndexBuffer(curFrame, _indexBuf, tmpB);

                if (curFrame._doBackup)
                    Buffer.BlockCopy(_indexBuf, 0, _prevFrameIndexBuf, 0, curFrame._rect.TotalPixels);

                DrawFrame(curFrame, _indexBuf);

                _prevFrame = fIndex;
                _hasNewFrame = true;

                ArrayPool<byte>.Shared.Return(tmpB);
            }
            finally
            {
                renderMutex.ReleaseMutex();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawFrame(GifFrame curFrame, Memory<byte> _frameIndexSpan)
        {
            var activeColorTableHash = curFrame._lctUsed ? curFrame._localColorTable : _globalColorTable;
            var activeColorTable = colorCache.Get(activeColorTableHash);

            var cX = curFrame._rect.X;
            var cY = curFrame._rect.Y;
            var cH = curFrame._rect.Height;
            var cW = curFrame._rect.Width;

            if (curFrame._interlaced)
                InterlaceRows(cH, DrawRow);
            else
                NormalRows(cH, DrawRow);

            //for (var row = 0; row < cH; row++)
            void DrawRow(int row)
            {
                // Get the starting point of the current row on frame's index stream.
                var indexOffset = row * cW;

                // Get the buffer window from the offset. 
                var indexSpan = _frameIndexSpan.Slice(indexOffset, cW).Span;

                // Get the target backbuffer offset from the frames coords.
                var targetOffset = PixCoord(cX, row + cY);
                var len = _bBuf.Length;

                for (var i = 0; i < cW; i++)
                {
                    var indexColor = indexSpan[i];

                    if (targetOffset >= len | indexColor >= activeColorTable.Length) return;

                    if (!(curFrame.HasTransparency & indexColor == curFrame._transparentColorIndex))
                        _bBuf[targetOffset] = activeColorTable[indexColor];

                    targetOffset++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DisposePreviousFrame()
        {

            if (firstFrame)
            {
                ClearArea(_gifRect);
                firstFrame = false;
            }

            var prevFrame = Frames[_prevFrame];

            switch (prevFrame._disposalMethod)
            {
                case FrameDisposal.DISPOSAL_METHOD_LEAVE:
                case FrameDisposal.DISPOSAL_METHOD_UNKNOWN:
                    break;
                case FrameDisposal.DISPOSAL_METHOD_BACKGROUND:
                    ClearArea(prevFrame._rect);
                    break;
                case FrameDisposal.DISPOSAL_METHOD_RESTORE:
                    DrawFrame(prevFrame, _prevFrameIndexBuf);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearArea(Int32Rect area)
        {
            for (int y = 0; y < area.Height; y++)
            {
                var targetOffset = PixCoord(area.X, y + area.Y);
                Array.Fill(_bBuf, TransparentColor, targetOffset, area.Width);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecompressFrameToIndexBuffer(GifFrame curFrame, Span<byte> indexSpan, Span<byte> tempBuf)
        {
            var str = _fileStream;

            str.Position = curFrame._lzwStreamPos;
            var totalPixels = curFrame._rect.TotalPixels;

            // Initialize GIF data stream decoder.
            var dataSize = curFrame._lzwMinCodeSize;
            var clear = 1 << dataSize;
            var endOfInformation = clear + 1;
            var available = clear + 2;
            var oldCode = -1;
            var codeSize = dataSize + 1;
            var codeMask = (1 << codeSize) - 1;

            for (var code = 0; code < clear; code++)
            {
                _prefixBuf[code] = 0;
                _suffixBuf[code] = (byte)code;
            }

            // Decode GIF pixel stream.
            int bits, first, top, pixelIndex, blockPos;
            var datum = bits = first = top = pixelIndex = blockPos = 0;

            while (pixelIndex < totalPixels)
            {
                var blockSize = str.ReadBlock(tempBuf);

                if (blockSize == 0)
                    break;

                blockPos = 0;

                while (blockPos < blockSize)
                {
                    datum += (tempBuf[blockPos]) << bits;
                    blockPos++;

                    bits += 8;

                    while (bits >= codeSize)
                    {
                        // Get the next code.
                        var code = datum & codeMask;
                        datum >>= codeSize;
                        bits -= codeSize;

                        // Interpret the code
                        if (code == clear)
                        {
                            // Reset decoder.
                            codeSize = dataSize + 1;
                            codeMask = (1 << codeSize) - 1;
                            available = clear + 2;
                            oldCode = -1;
                            continue;
                        }

                        // Check for explicit end-of-stream
                        if (code == endOfInformation)
                        {
                            return;
                        }

                        if (oldCode == -1)
                        {
                            indexSpan[pixelIndex++] = _suffixBuf[code];
                            oldCode = code;
                            first = code;
                            continue;
                        }

                        var inCode = code;
                        if (code >= available)
                        {
                            _pixelStack[top++] = (byte)first;
                            code = oldCode;

                            if (top == MaxBits)
                                ThrowException();
                        }

                        while (code >= clear)
                        {
                            if (code >= MaxBits || code == _prefixBuf[code])
                                ThrowException();

                            _pixelStack[top++] = _suffixBuf[code];
                            code = _prefixBuf[code];

                            if (top == MaxBits)
                                ThrowException();
                        }

                        first = _suffixBuf[code];
                        _pixelStack[top++] = (byte)first;

                        // Add new code to the dictionary
                        if (available < MaxStackSize)
                        {
                            _prefixBuf[available] = (short)oldCode;
                            _suffixBuf[available] = (byte)first;
                            available++;

                            if (((available & codeMask) == 0) && (available < MaxStackSize))
                            {
                                codeSize++;
                                codeMask += available;
                            }
                        }

                        oldCode = inCode;

                        // Drain the pixel stack.
                        do
                        {
                            indexSpan[pixelIndex++] = _pixelStack[--top];
                        } while (top > 0);
                    }
                }
            }

            while (pixelIndex < totalPixels)
                indexSpan[pixelIndex++] = 0; // clear missing pixels


            void ThrowException() => throw new LzwDecompressionException();

        }

        /// <summary>
        /// Directly copies the <see cref="GifColor"/> struct array to a <see cref="ILockedFramebuffer"/>.
        /// </summary>
        public void WriteBackBufToFb(ILockedFramebuffer lockBuf)
        {
            renderMutex.WaitOne();
            try
            {
                if (_hasNewFrame)
                    unsafe
                    {
                        fixed (void* src = &_bBuf[0])
                            Buffer.MemoryCopy(src, lockBuf.Address.ToPointer(), _backBufferBytes, _backBufferBytes);
                        _hasNewFrame = false;
                    }
            }
            finally
            {
                renderMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Processes GIF Header.
        /// </summary>
        private void ProcessHeaderData()
        {
            var str = _fileStream;
            var tmpB = ArrayPool<byte>.Shared.Rent(MaxTempBuf);
            var tempBuf = tmpB.AsSpan();

            var headerMagic = tempBuf.Slice(0, 6);

            str.Read(headerMagic);

            if (!(headerMagic.SequenceEqual(G87AMagic.Span) | headerMagic.SequenceEqual(G89AMagic.Span)))
                throw new InvalidGifStreamException("Unsupported stream or invalid GIF Header: " +
                                                    Encoding.ASCII.GetString(headerMagic));

            ProcessScreenDescriptor(tempBuf);

            if (_gctUsed)
                _globalColorTable = ProcessColorTable(ref str, tempBuf, _gctSize);


            _gifHeader = new GifHeader()
            {
                Rect = _gifRect,
                HasGlobalColorTable = _gctUsed,
                GlobalColorTable = _globalColorTable,
                GlobalColorTableSize = _gctSize,
                BackgroundColorIndex = _bgIndex,
                HeaderSize = _fileStream.Position
            };

            ArrayPool<byte>.Shared.Return(tmpB);
        }

        public WriteableBitmap CreateBitmapForRender(Vector? dpi = null)
        {
            var defDpi = dpi ?? new Vector(96, 96);
            var pxSize = new PixelSize(_gifRect.Width, _gifRect.Height);
            return new WriteableBitmap(pxSize, defDpi, PixelFormat.Bgra8888);
        }

        /// <summary>
        /// Parses colors from file stream to target color table.
        /// </summary> 
        private static ulong ProcessColorTable(ref Stream stream, Span<byte> rentedBuf, int nColors)
        {
            var nBytes = 3 * nColors;
            var rawBufSpan = rentedBuf.Slice(0, nBytes);
            var targ = new GifColor[nColors];

            var n = stream.Read(rawBufSpan);

            if (n < nBytes)
                throw new InvalidOperationException("Wrong color table bytes.");

            Span<byte> rawHash = new byte[sizeof(ulong)];
            hasher.TryComputeHash(rawBufSpan, rawHash, out var bytes);
            var tableHash = BitConverter.ToUInt64(rawHash);

            int i = 0, j = 0;

            while (i < nColors)
            {
                var r = rawBufSpan[j++];
                var g = rawBufSpan[j++];
                var b = rawBufSpan[j++];
                targ[i++] = new GifColor(r, g, b);
            }

            colorCache.Set(tableHash, targ);

            return tableHash;
        }

        /// <summary>
        /// Parses screen and other GIF descriptors. 
        /// </summary>
        private void ProcessScreenDescriptor(Span<byte> tempBuf)
        {
            var str = _fileStream;

            var _width = str.ReadUShortS();
            var _height = str.ReadUShortS();

            var packed = str.ReadByteS();

            _gctUsed = (packed & 0x80) != 0;
            _gctSize = 2 << (packed & 7);
            _bgIndex = str.ReadByteS();

            _gifRect = new Int32Rect(0, 0, _width, _height);
            str.Skip(1);
        }

        /// <summary>
        /// Parses all frame data.
        /// </summary>
        private void ProcessFrameData()
        {
            var str = _fileStream;
            str.Position = _gifHeader.HeaderSize;

            var tmpB = ArrayPool<byte>.Shared.Rent(MaxTempBuf);
            var tempBuf = tmpB.AsSpan();
            var terminate = false;
            var curFrame = 0;

            Frames.Add(new GifFrame());

            do
            {
                var blockType = (BlockTypes)str.ReadByteS();

                switch (blockType)
                {
                    case BlockTypes.EMPTY:
                        break;

                    case BlockTypes.EXTENSION:
                        ProcessExtensions(ref curFrame, tempBuf);
                        break;

                    case BlockTypes.IMAGE_DESCRIPTOR:
                        ProcessImageDescriptor(ref curFrame, tempBuf);
                        str.SkipBlocks();
                        break;

                    case BlockTypes.TRAILER:
                        Frames.RemoveAt(Frames.Count - 1);
                        terminate = true;
                        break;

                    default:
                        str.SkipBlocks();
                        break;
                }

                // Break the loop when the stream is not valid anymore.
                if (str.Position >= str.Length & terminate == false)
                    throw new InvalidProgramException("Reach the end of the filestream without trailer block.");

            } while (!terminate);

            ArrayPool<byte>.Shared.Return(tmpB);
        }

        /// <summary>
        /// Parses GIF Image Descriptor Block.
        /// </summary>
        private void ProcessImageDescriptor(ref int curFrame, Span<byte> tempBuf)
        {
            var str = _fileStream;
            var currentFrame = Frames[curFrame];

            // Parse frame dimensions.
            var _frameX = str.ReadUShortS();
            var _frameY = str.ReadUShortS();
            var _frameW = str.ReadUShortS();
            var _frameH = str.ReadUShortS();

            _frameW = (ushort)Math.Min(_frameW, _gifRect.Width - _frameX);
            _frameH = (ushort)Math.Min(_frameH, _gifRect.Height - _frameY);

            currentFrame._rect = new Int32Rect(_frameX, _frameY, _frameW, _frameH);

            // Unpack interlace and lct info.
            var packed = str.ReadByteS();
            currentFrame._interlaced = (packed & 0x40) != 0;
            currentFrame._lctUsed = (packed & 0x80) != 0;
            currentFrame._lctSize = (int)Math.Pow(2, (packed & 0x07) + 1);

            if (currentFrame._lctUsed)
                currentFrame._localColorTable = ProcessColorTable(ref str, tempBuf, currentFrame._lctSize);

            currentFrame._lzwMinCodeSize = str.ReadByteS();
            currentFrame._lzwStreamPos = str.Position;

            curFrame += 1;
            Frames.Add(new GifFrame());
        }

        /// <summary>
        /// Parses GIF Extension Blocks.
        /// </summary>
        private void ProcessExtensions(ref int curFrame, Span<byte> tempBuf)
        {
            var str = _fileStream;

            var extType = (ExtensionType)str.ReadByteS();

            switch (extType)
            {
                case ExtensionType.GRAPHICS_CONTROL:

                    str.ReadBlock(tempBuf);
                    var currentFrame = Frames[curFrame];
                    var packed = tempBuf[0];

                    currentFrame._disposalMethod = (FrameDisposal)((packed & 0x1c) >> 2);

                    if (currentFrame._disposalMethod != FrameDisposal.DISPOSAL_METHOD_RESTORE |
                        currentFrame._disposalMethod != FrameDisposal.DISPOSAL_METHOD_BACKGROUND)
                        currentFrame._doBackup = true;

                    currentFrame.HasTransparency = (packed & 1) != 0;

                    currentFrame._frameDelay =
                        TimeSpan.FromMilliseconds(SpanToShort(tempBuf.Slice(1)) * 10);

                    if (currentFrame._frameDelay <= FrameDelayThreshold)
                        currentFrame._frameDelay = FrameDelayDefault;

                    currentFrame._transparentColorIndex = tempBuf[3];
                    break;

                case ExtensionType.APPLICATION:
                    var blockLen = str.ReadBlock(tempBuf);
                    var blockSpan = tempBuf.Slice(0, blockLen);
                    var blockHeader = tempBuf.Slice(0, NetscapeMagic.Length);

                    if (blockHeader.SequenceEqual(NetscapeMagic.Span))
                    {
                        var count = 1;

                        while (count > 0)
                            count = str.ReadBlock(tempBuf);

                        _iterationCount = SpanToShort(tempBuf.Slice(1));
                    }
                    else
                        str.SkipBlocks();

                    break;

                default:
                    str.SkipBlocks();
                    break;
            }
        }
    }
}