using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XamlAnimatedGif.Decoding
{
    internal class GifDataStream
    {
        public GifHeader Header { get; private set; }
        public GifColor[] GlobalColorTable { get; set; }
        public IList<GifFrame> Frames { get; set; }
        public IList<GifExtension> Extensions { get; set; }
        public ushort RepeatCount { get; set; }

        private GifDataStream()
        {
        }

        internal static GifDataStream ReadGifDataStream(Stream stream)
        {
            var file = new GifDataStream();
            file.Read(stream);
            return file;
        }

        private void Read(Stream stream)
        {
            Header = GifHeader.ReadHeader(stream);

            if (Header.LogicalScreenDescriptor.HasGlobalColorTable)
            {
                GlobalColorTable = GifHelpers.ReadColorTable(stream, Header.LogicalScreenDescriptor.GlobalColorTableSize);
            }
            ReadFrames(stream);

            var netscapeExtension =
                            Extensions
                                .OfType<GifApplicationExtension>()
                                .FirstOrDefault(GifHelpers.IsNetscapeExtension);

            RepeatCount = netscapeExtension != null
                ? GifHelpers.GetRepeatCount(netscapeExtension)
                : (ushort)1;
        }

        private void ReadFrames(Stream stream)
        {
            List<GifFrame> frames = new List<GifFrame>();
            List<GifExtension> controlExtensions = new List<GifExtension>();
            List<GifExtension> specialExtensions = new List<GifExtension>();
            while (true)
            {
                var block = GifBlock.ReadBlock(stream, controlExtensions);

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

            this.Frames = frames.AsReadOnly();
            this.Extensions = specialExtensions.AsReadOnly();
        }
    }
}
