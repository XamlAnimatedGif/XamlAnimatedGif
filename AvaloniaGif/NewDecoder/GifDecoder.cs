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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;

using Avalonia;
using Avalonia.Platform;
using Avalonia.Media.Imaging;

using static AvaloniaGif.NewDecoder.StreamExtensions;

namespace AvaloniaGif.NewDecoder
{
    public class GifDecoder
    {
        private static readonly ReadOnlyMemory<byte> GIFMagic
                            = Encoding.ASCII.GetBytes("GIF").AsMemory();
        private static readonly ReadOnlyMemory<byte> G87AMagic
                            = Encoding.ASCII.GetBytes("87a").AsMemory();
        private static readonly ReadOnlyMemory<byte> G89AMagic
                            = Encoding.ASCII.GetBytes("89a").AsMemory();
        private static readonly ReadOnlyMemory<byte> NetscapeMagic
                            = Encoding.ASCII.GetBytes("NETSCAPE2.0").AsMemory();

        private static readonly TimeSpan _frameDelayThreshold = TimeSpan.FromMilliseconds(10);
        private static readonly TimeSpan _frameDelayDefault = TimeSpan.FromMilliseconds(100);

        private const int MAX_TEMP_BUF = 768;
        private const int MAX_STACK_SIZE = 4096;
        private const int MAX_BITS = 4097;

        private int _iterationCount, _width, _height, mHeaderSize, _gctSize, _bgIndex;
        private bool _globalColorTableUsed;
        private GifColor _bgColor;
        private Stream _fileStream;
        private GifHeader _gifHeader;

        private Memory<GifColor> _globalColorTable
            = new Memory<GifColor>(new GifColor[256]);

        internal Stream FileStream => _fileStream;
        public GifHeader Header => _gifHeader;
        public int IterationCount => _iterationCount;
        public List<GifFrame> Frames = new List<GifFrame>();


        private Memory<GifColor> _backBufMem;
        private GifColor[] _bBuf;
        int _backBufferBytes;


        // LZW decoder working arrays
        private short[] _prefixBuf = new short[MAX_STACK_SIZE];
        private byte[] _suffixBuf = new byte[MAX_STACK_SIZE];
        private byte[] _pixelStack = new byte[MAX_STACK_SIZE + 1];
        private byte[] _indexBuf;

        public GifDecoder(Stream fileStream)
        {
            _fileStream = fileStream;

            ReadHeader();
            ReadFrameData();

            var pixelCount = Header.Height * Header.Width;

            _bBuf = new GifColor[pixelCount];
            _indexBuf = new byte[pixelCount];
            _backBufferBytes = pixelCount * Marshal.SizeOf(typeof(GifColor));
            _backBufMem = _bBuf.AsMemory();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int PixCoord(int x, int y) => x + y * Header.Width;

        public void RenderFrame(int fIndex)
        {
            var tmpB = ArrayPool<byte>.Shared.Rent(4);

            var curFrame = Frames[fIndex];

            DecompressFrameToIndexScratch(ref curFrame, tmpB.AsSpan(0, 4));

            ArrayPool<byte>.Shared.Return(tmpB);
        }

        private void DecompressFrameToIndexScratch(ref GifFrame curFrame, Span<byte> tempBuf)
        {
            var str = _fileStream;

            str.Position = curFrame._lzwStreamPos;
            var totalPixels = curFrame._frameH * curFrame._frameW;

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
            int datum, bits, first, top, pixelIndex;
            datum = bits = first = top = pixelIndex = 0;

            while (pixelIndex < totalPixels)
            {
                str.ReadByteS(tempBuf);
                var blockSize = tempBuf[0];

                if (blockSize == 0)
                    break;

                var blockEnd = str.Position + blockSize;

                while (str.Position < blockEnd)
                {
                    str.ReadByteS(tempBuf);
                    datum += (tempBuf[0]) << bits;
                    bits += 8;

                    while (bits >= codeSize)
                    {
                        // Get the next code.
                        int code = datum & codeMask;
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
                            str.Position = blockEnd;
                            return;
                        }

                        if (oldCode == -1)
                        {
                            _indexBuf[pixelIndex++] = _suffixBuf[code];
                            oldCode = code;
                            first = code;
                            continue;
                        }

                        int inCode = code;
                        if (code >= available)
                        {
                            _pixelStack[top++] = (byte)first;
                            code = oldCode;

                            if (top == MAX_BITS)
                                throw new GifLZWDecompressionError();
                        }

                        while (code >= clear)
                        {
                            if (code >= MAX_BITS || code == _prefixBuf[code])
                                throw new GifLZWDecompressionError();

                            _pixelStack[top++] = _suffixBuf[code];
                            code = _prefixBuf[code];

                            if (top == MAX_BITS)
                                throw new GifLZWDecompressionError();
                        }

                        first = _suffixBuf[code];
                        _pixelStack[top++] = (byte)first;

                        // Add new code to the dictionary
                        if (available < MAX_STACK_SIZE)
                        {
                            _prefixBuf[available] = (short)oldCode;
                            _suffixBuf[available] = (byte)first;
                            available++;

                            if (((available & codeMask) == 0) && (available < MAX_STACK_SIZE))
                            {
                                codeSize++;
                                codeMask += available;
                            }
                        }

                        oldCode = inCode;

                        // Drain the pixel stack.
                        do
                        {
                            _indexBuf[pixelIndex++] = _pixelStack[--top];
                        } while (top > 0);
                    }
                }
            }

            while (pixelIndex < totalPixels)
                _indexBuf[pixelIndex++] = 0; // clear missing pixels
        }

        public unsafe void WriteBackBufToFB(ILockedFramebuffer lockBuf)
        {
            fixed (void* src = &_bBuf[0])
                Buffer.MemoryCopy(src, lockBuf.Address.ToPointer(), _backBufferBytes, _backBufferBytes);
        }

        /// <summary>
        /// Processes GIF Header.
        /// </summary>
        private void ReadHeader()
        {
            var str = _fileStream;
            var tmpB = ArrayPool<byte>.Shared.Rent(MAX_TEMP_BUF);
            var tempBuf = tmpB.AsSpan();

            var triplet = tempBuf.Slice(0, 3);

            str.Read(triplet);

            if (!triplet.SequenceEqual(GIFMagic.Span))
                throw new InvalidGifStreamException("Invalid GIF Header");

            str.Read(triplet);

            if (!(triplet.SequenceEqual(G87AMagic.Span) | triplet.SequenceEqual(G89AMagic.Span)))
                throw new InvalidGifStreamException("Unsupported GIF Version: " +
                     Encoding.ASCII.GetString(triplet));

            ReadLogicalScreenDescriptor(tempBuf);

            if (_globalColorTableUsed)
                ReadColorTable(ref str, tempBuf, _globalColorTable.Span, _gctSize);

            _gifHeader = new GifHeader()
            {
                Width = _width,
                Height = _height,
                HasGlobalColorTable = _globalColorTableUsed,
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
            var pixSiz = new PixelSize(Header.Width, Header.Height);
            return new WriteableBitmap(pixSiz, defDpi, PixelFormat.Bgra8888);
        }

        /// <summary>
        /// Parses colors from file stream to target color table.
        /// </summary> 
        private static bool ReadColorTable(ref Stream stream, Span<byte> rentedBuf,
                                           Span<GifColor> targetColorTable, int ncolors)
        {
            var nbytes = 3 * ncolors;
            var rawBufSpan = rentedBuf.Slice(0, nbytes);
            var n = stream.Read(rawBufSpan);

            if (n < nbytes)
                return false;

            int i = 0;
            int j = 0;

            while (i < ncolors)
            {
                var r = rawBufSpan[j++];
                var g = rawBufSpan[j++];
                var b = rawBufSpan[j++];
                targetColorTable[i++] = new GifColor(r, g, b);
            }

            return true;
        }

        /// <summary>
        /// Parses screen and other GIF descriptors. 
        /// </summary>
        private void ReadLogicalScreenDescriptor(Span<byte> tempBuf)
        {
            var str = _fileStream;

            _width = str.ReadUShortS(tempBuf);
            _height = str.ReadUShortS(tempBuf);

            var packed = str.ReadByteS(tempBuf);

            _globalColorTableUsed = (packed & 0x80) != 0;
            _gctSize = 2 << (packed & 7);
            _bgIndex = str.ReadByteS(tempBuf);

            str.Skip(1);
        }

        /// <summary>
        /// Parses all frame data for random-seeking.
        /// </summary>
        private void ReadFrameData()
        {
            var str = _fileStream;
            str.Position = Header.HeaderSize;

            var tmpB = ArrayPool<byte>.Shared.Rent(MAX_TEMP_BUF);

            var tempBuf = tmpB.AsSpan();

            var terminate = false;

            var curFrame = 0;

            Frames.Add(new GifFrame());

            do
            {
                str.ReadByteS(tempBuf);
                var blockType = (BlockTypes)tempBuf[0];

                switch (blockType)
                {
                    case BlockTypes.EMPTY:
                        break;

                    case BlockTypes.EXTENSION:
                        ProcessExtensions(ref curFrame, tempBuf);
                        break;

                    case BlockTypes.IMAGE_DESCRIPTOR:
                        ProcessImageDescriptor(ref curFrame, tempBuf);
                        str.SkipBlocks(tempBuf);
                        break;

                    case BlockTypes.TRAILER:
                        Frames.RemoveAt(Frames.Count - 1);
                        terminate = true;
                        break;

                    default:
                        str.SkipBlocks(tempBuf);
                        break;
                }

            } while (!terminate & str.Position != str.Length);

            ArrayPool<byte>.Shared.Return(tmpB);
        }

        /// <summary>
        /// Parses GIF Image Descriptor Block.
        /// </summary>
        private void ProcessImageDescriptor(ref int curFrame, Span<byte> tempBuf)
        {
            var str = _fileStream;
            var currentFrame = Frames[curFrame];

            currentFrame._frameX = str.ReadUShortS(tempBuf);
            currentFrame._frameY = str.ReadUShortS(tempBuf);
            currentFrame._frameW = str.ReadUShortS(tempBuf);
            currentFrame._frameH = str.ReadUShortS(tempBuf);

            var packed = str.ReadByteS(tempBuf);

            currentFrame._interlaced = (packed & 0x40) != 0;
            currentFrame._lctUsed = (packed & 0x80) != 0;
            currentFrame._lctSize = (int)Math.Pow(2, (packed & 0x07) + 1);

            if (currentFrame._lctUsed)
            {
                if (!currentFrame._localColorTable.HasValue)
                    currentFrame._localColorTable
                        = new Memory<GifColor>(new GifColor[currentFrame._lctSize]);

                ReadColorTable(ref str, tempBuf, currentFrame._localColorTable.Value.Span, currentFrame._lctSize);
            }

            // Don't remove these as the parsing flow gets infinite looped when 
            // the the LZW code size isn't consumed.
            currentFrame._lzwMinCodeSize = str.ReadByteS(tempBuf);
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

            str.ReadByteS(tempBuf);
            var extType = (ExtensionType)tempBuf[0];

            switch (extType)
            {
                case ExtensionType.GRAPHICS_CONTROL:

                    str.ReadBlock(tempBuf);

                    var currentFrame = Frames[curFrame];

                    var packed = tempBuf[0]; // Packed fields

                    currentFrame._disposalMethod = (FrameDisposal)((packed & 0x1c) >> 2);
                    currentFrame._transparency = (packed & 1) != 0;
                    currentFrame._frameDelay =
                        TimeSpan.FromMilliseconds(SpanToShort(tempBuf.Slice(1)) * 10);

                    if (currentFrame._frameDelay <= _frameDelayThreshold)
                        currentFrame._frameDelay = _frameDelayDefault;

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
                        str.SkipBlocks(tempBuf);
                    break;

                default:
                    str.SkipBlocks(tempBuf);
                    break;
            }
        }
    }
}