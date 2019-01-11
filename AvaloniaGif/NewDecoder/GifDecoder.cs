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
        private GifStream _gifStream;
        private static ReadOnlyMemory<byte> NetscapeMagic 
                    = Encoding.ASCII.GetBytes("NETSCAPE2.0").AsMemory();
        private int _iterationCount;
        private static readonly TimeSpan _frameDelayThreshold = TimeSpan.FromMilliseconds(10);
        private static readonly TimeSpan _frameDelayDefault = TimeSpan.FromMilliseconds(100);


        public List<GifFrame> Frames = new List<GifFrame>();


        public GifDecoder(GifStream gifStream)
        {
            _gifStream = gifStream;
            GenerateFrameData();
        }

        private void GenerateFrameData()
        {
            var str = _gifStream.FileStream;
            str.Position = _gifStream.Header.HeaderSize;

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

        private void ProcessImageDescriptor(ref int curFrame, Span<byte> tempBuf)
        {
            var str = _gifStream.FileStream;
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

        private void ProcessExtensions(ref int curFrame, Span<byte> tempBuf)
        {
            var str = _gifStream.FileStream;

            str.ReadByteS(tempBuf);
            var extType = (ExtensionType)tempBuf[0];

            Span<byte> blockBuf = stackalloc byte[256];

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

                    var blockLen = str.ReadBlock(blockBuf);
                    var blockSpan = blockBuf.Slice(0, blockLen);
                    var blockHeader = blockBuf.Slice(0, NetscapeMagic.Length);

                    if (blockHeader.SequenceEqual(NetscapeMagic.Span))
                    {
                        var count = 1;

                        while (count > 0)
                        {
                            count = str.ReadBlock(tempBuf);
                        }

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