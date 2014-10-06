using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
#if WINRT
using XamlAnimatedGif.Extensions;
#endif

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

        internal static async Task<GifDataStream> ReadAsync(Stream stream)
        {
            var file = new GifDataStream();
            await file.ReadInternalAsync(stream);
            return file;
        }

        private async Task ReadInternalAsync(Stream stream)
        {
            Header = await GifHeader.ReadAsync(stream);

            if (Header.LogicalScreenDescriptor.HasGlobalColorTable)
            {
                GlobalColorTable = await GifHelpers.ReadColorTableAsync(stream, Header.LogicalScreenDescriptor.GlobalColorTableSize);
            }
            await ReadFramesAsync(stream);

            var netscapeExtension =
                            Extensions
                                .OfType<GifApplicationExtension>()
                                .FirstOrDefault(GifHelpers.IsNetscapeExtension);

            RepeatCount = netscapeExtension != null
                ? GifHelpers.GetRepeatCount(netscapeExtension)
                : (ushort)1;
        }

        private async Task ReadFramesAsync(Stream stream)
        {
            List<GifFrame> frames = new List<GifFrame>();
            List<GifExtension> controlExtensions = new List<GifExtension>();
            List<GifExtension> specialExtensions = new List<GifExtension>();
            while (true)
            {
                var block = await GifBlock.ReadAsync(stream, controlExtensions);

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
