using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AvaloniaGif.Extensions;

namespace AvaloniaGif.Decoding
{
    internal abstract class GifBlock
    {
        internal static GifBlock ReadAsync(Stream stream, IEnumerable<GifExtension> controlExtensions)
        {
            int blockId = stream.ReadByte();
            if (blockId < 0)
                throw new EndOfStreamException();
            switch (blockId)
            {
                case GifExtension.ExtensionIntroducer:
                    return  GifExtension.ReadAsync(stream, controlExtensions);
                case GifFrame.ImageSeparator:
                    return  GifFrame.ReadAsync(stream, controlExtensions);
                case GifTrailer.TrailerByte:
                    return  GifTrailer.ReadAsync();
                default:
                    throw GifHelpers.UnknownBlockTypeException(blockId);
            }
        }

        internal abstract GifBlockKind Kind { get; }
    }
}
