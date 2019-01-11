using System.IO;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using static AvaloniaGif.NewDecoder.StreamExtensions;

namespace AvaloniaGif.NewDecoder
{
    public class GifDecoder
    {
        private static ReadOnlyMemory<byte> GIFMagic
                            = Encoding.ASCII.GetBytes("GIF").AsMemory();
        private static ReadOnlyMemory<byte> G87AMagic
                     = Encoding.ASCII.GetBytes("87a").AsMemory();
        private static ReadOnlyMemory<byte> G89AMagic
                     = Encoding.ASCII.GetBytes("89a").AsMemory();
        private static ReadOnlyMemory<byte> NetscapeMagic
                    = Encoding.ASCII.GetBytes("NETSCAPE2.0").AsMemory();

        private static readonly TimeSpan _frameDelayThreshold = TimeSpan.FromMilliseconds(10);
        private static readonly TimeSpan _frameDelayDefault = TimeSpan.FromMilliseconds(100);


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


        public GifDecoder(Stream fileStream)
        {
            _fileStream = fileStream;
            ReadHeader();
            ReadFrameData();
        }


        /// <summary>
        /// Processes GIF Header.
        /// </summary>
        private void ReadHeader()
        {
            Span<byte> val = stackalloc byte[256];

            var triplet = val.Slice(0, 3);

            _fileStream.Read(triplet);

            if (!triplet.SequenceEqual(GIFMagic.Span))
                throw new InvalidFormatException("Invalid GIF Header");

            _fileStream.Read(triplet);

            if (!(triplet.SequenceEqual(G87AMagic.Span) | triplet.SequenceEqual(G89AMagic.Span)))
                throw new InvalidFormatException("Unsupported GIF Version: " +
                     Encoding.ASCII.GetString(triplet));

            ReadLogicalScreenDescriptor(val);

            if (_globalColorTableUsed)
            {
                ReadColorTable(ref _fileStream, val, _globalColorTable.Span, _gctSize);
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
            _width = _fileStream.ReadUShortS(tempBuf);
            _height = _fileStream.ReadUShortS(tempBuf);

            var packed = _fileStream.ReadByteS(tempBuf);

            _globalColorTableUsed = (packed & 0x80) != 0; 
            _gctSize = 2 << (packed & 7);
            _bgIndex = _fileStream.ReadByteS(tempBuf);

            _fileStream.Skip(1);
        }

        /// <summary>
        /// Parses all frame data for random-seeking.
        /// </summary>
        private void ReadFrameData()
        {
            var str = _fileStream;
            str.Position = Header.HeaderSize;

            Span<byte> tempBuf = stackalloc byte[256];
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
            currentFrame._localColorTableUsed = (packed & 0x80) != 0;
            currentFrame._localColorTableSize = (int)Math.Pow(2, (packed & 0x07) + 1);

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

                    currentFrame._disposalMethod = (packed & 0x1c) >> 2;
                    currentFrame._transparency = (packed & 1) != 0;
                    currentFrame._frameDelay =
                        TimeSpan.FromMilliseconds(SpanToShort(tempBuf.Slice(1)) * 10);

                    // It seems that there are broken tools out there that set a 0ms or 10ms
                    // timeout when they really want a "default" one.

                    // Following WebKit's lead (http://trac.webkit.org/changeset/73295)
                    // we'll use 10 frames per second as the default frame rate.

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