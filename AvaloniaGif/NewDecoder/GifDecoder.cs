using System.IO;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using static AvaloniaGif.NewDecoder.StreamExtensions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia.Platform;
using Avalonia.Media.Imaging;
using Avalonia;

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

        private static readonly int StackAllocMax = 256 * 3;

        private int _iterationCount, _width, _height, mHeaderSize, _gctSize, _bgIndex;
        private bool _globalColorTableUsed;
        private GifColor _bgColor;
        private Stream _fileStream;
        private GifHeader _gifHeader;

        private Memory<GifColor> _backBufMem;
        private GifColor[] _bBuf;

        private Memory<GifColor> _globalColorTable
            = new Memory<GifColor>(new GifColor[256]);

        internal Stream FileStream => _fileStream;
        public GifHeader Header => _gifHeader;
        public int IterationCount => _iterationCount;
        public List<GifFrame> Frames = new List<GifFrame>();
        int _backBufferSize;

        public GifDecoder(Stream fileStream)
        {
            _fileStream = fileStream;

            ReadHeader();
            ReadFrameData();


            var pixelCount = Header.Height * Header.Width;
            _bBuf = new GifColor[pixelCount];
            _backBufferSize = pixelCount * Marshal.SizeOf(typeof(GifColor));

            for (var p = 0; p < pixelCount; p++)
            {
                _bBuf[p] = new GifColor(15, 99, 56);
            }

            _backBufMem = _bBuf.AsMemory();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int PixCoord(int x, int y) => x + y * Header.Width;




        public void DecodeFrame(int fIndex)
        {
            // var curFrame = Frames[fIndex];
            // var str = _fileStream;

            // str.Position = curFrame._lzwStreamPos;

            // o %= Header.Width;
            // z %= Header.Height;

            // _backBufMem.Span[PixCoord(o++, z++)]
            //     = new GifColor(
            //              255, 0, 0
            //             );


            // reuse backbuffer as LZW index stream 
        }

        public void WriteBackBufToFB(ILockedFramebuffer lockBuf)
        {
            unsafe
            {
                fixed (void* src = &_bBuf[0])
                {
                    Buffer.MemoryCopy(src, lockBuf.Address.ToPointer(), _backBufferSize, _backBufferSize);
                }
            }
        }

        /// <summary>
        /// Processes GIF Header.
        /// </summary>
        private void ReadHeader()
        {
            var str = _fileStream;
            var tmpB = ArrayPool<byte>.Shared.Rent(StackAllocMax);
            var tempBuf = tmpB.AsSpan();

            var triplet = tempBuf.Slice(0, 3);

            str.Read(triplet);

            if (!triplet.SequenceEqual(GIFMagic.Span))
                throw new InvalidFormatException("Invalid GIF Header");

            str.Read(triplet);

            if (!(triplet.SequenceEqual(G87AMagic.Span) | triplet.SequenceEqual(G89AMagic.Span)))
                throw new InvalidFormatException("Unsupported GIF Version: " +
                     Encoding.ASCII.GetString(triplet));

            ReadLogicalScreenDescriptor(tempBuf);

            if (_globalColorTableUsed)
            {
                ReadColorTable(ref str, tempBuf, _globalColorTable.Span, _gctSize);
                _bgColor = _globalColorTable.Span[_bgIndex];
            }

            _gifHeader = new GifHeader()
            {
                Width = _width,
                Height = _height,
                HasGlobalColorTable = _globalColorTableUsed,
                GlobalColorTable = _globalColorTable,
                GlobalColorTableSize = _gctSize,
                BackgroundColor = _bgColor,
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

            var tmpB = ArrayPool<byte>.Shared.Rent(StackAllocMax);

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

            currentFrame._lzwCodeSize = str.ReadByteS(tempBuf);
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
                        str.SkipBlocks();

                    break;
                default:
                    str.SkipBlocks();
                    break;
            }
        }
    }
}