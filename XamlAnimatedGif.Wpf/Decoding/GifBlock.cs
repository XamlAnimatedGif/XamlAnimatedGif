using System.Collections.Generic;
using System.IO;

namespace XamlAnimatedGif.Decoding
{
    internal abstract class GifBlock
    {
        internal static GifBlock ReadBlock(Stream stream, IEnumerable<GifExtension> controlExtensions)
        {
            int blockId = stream.ReadByte();
            if (blockId < 0)
                throw GifHelpers.UnexpectedEndOfStreamException();
            switch (blockId)
            {
                case GifExtension.ExtensionIntroducer:
                    return GifExtension.ReadExtension(stream, controlExtensions);
                case GifFrame.ImageSeparator:
                    return GifFrame.ReadFrame(stream, controlExtensions);
                case GifTrailer.TrailerByte:
                    return GifTrailer.ReadTrailer();
                default:
                    throw GifHelpers.UnknownBlockTypeException(blockId);
            }
        }

        internal abstract GifBlockKind Kind { get; }
    }
}
