using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AvaloniaGif.Decoding
{
    internal class GifDataStream
    {
        public GifHeader Header { get; private set; }
        public GifColor[] GlobalColorTable { get; set; }
        public Memory<GifFrame> Frames { get; set; }
        public IList<GifExtension> Extensions { get; set; }
        public ushort IterationCount { get; set; }

        private GifDataStream()
        {
        }

        internal static GifDataStream Read(Stream stream)
        {
            var file = new GifDataStream();
            file.ReadInternal(stream);
            return file;
        }

        private void ReadInternal(Stream stream)
        {
            Header = GifHeader.Read(stream);

            if (Header.LogicalScreenDescriptor.HasGlobalColorTable)
            {
                GlobalColorTable = GifHelpers.ReadColorTable(stream, Header.LogicalScreenDescriptor.GlobalColorTableSize);
            }
            ReadFrames(stream);

            var netscapeExtension =
                            Extensions
                                .OfType<GifApplicationExtension>()
                                .FirstOrDefault(GifHelpers.IsNetscapeExtension);

            IterationCount = netscapeExtension != null
                ? GifHelpers.GetIterationCount(netscapeExtension)
                : (ushort)1;
        }

        private void ReadFrames(Stream stream)
        {
            List<GifFrame> frames = new List<GifFrame>();
            List<GifExtension> controlExtensions = new List<GifExtension>();
            List<GifExtension> specialExtensions = new List<GifExtension>();
            while (true)
            {
                try
                {
                    var block = GifBlock.Read(stream, controlExtensions);

                    if (block.Kind == GifBlockKind.GraphicRendering)
                        controlExtensions = new List<GifExtension>();

                    if (block is GifFrame)
                    {
                        frames.Add((GifFrame)block);
                    }
                    else if (block is GifExtension)
                    {
                        var extension = (GifExtension)block;
                        switch (extension.Kind)
                        {
                            case GifBlockKind.Control:
                                controlExtensions.Add(extension);
                                break;
                            case GifBlockKind.SpecialPurpose:
                                specialExtensions.Add(extension);
                                break;

                                // Just discard plain text extensions for now, since we have no use for it
                        }
                    }
                    else if (block is GifTrailer)
                    {
                        break;
                    }
                }
                // Follow the same approach as Firefox:
                // If we find extraneous data between blocks, just assume the stream
                // was successfully terminated if we have some successfully decoded frames
                // https://dxr.mozilla.org/firefox/source/modules/libpr0n/decoders/gif/nsGIFDecoder2.cpp#894-909
                catch (UnknownBlockTypeException) when (frames.Count > 0)
                {
                    break;
                }
            }

            this.Frames = frames.ToArray().AsMemory();
            this.Extensions = specialExtensions.AsReadOnly();
        }
    }
}
