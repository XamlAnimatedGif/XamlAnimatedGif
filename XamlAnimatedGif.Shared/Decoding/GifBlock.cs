using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using XamlAnimatedGif.Extensions;

namespace XamlAnimatedGif.Decoding
{
    internal abstract class GifBlock
    {
        internal static async Task<GifBlock> ReadAsync(Stream stream, IEnumerable<GifExtension> controlExtensions)
        {
            int blockId = await stream.ReadByteAsync();
            if (blockId < 0)
                throw GifHelpers.UnexpectedEndOfStreamException();
            switch (blockId)
            {
                case GifExtension.ExtensionIntroducer:
                    return await GifExtension.ReadAsync(stream, controlExtensions);
                case GifFrame.ImageSeparator:
                    return await GifFrame.ReadAsync(stream, controlExtensions);
                case GifTrailer.TrailerByte:
                    return await GifTrailer.ReadAsync();
                default:
                    throw GifHelpers.UnknownBlockTypeException(blockId);
            }
        }

        internal abstract GifBlockKind Kind { get; }
    }
}
