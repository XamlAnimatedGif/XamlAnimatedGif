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
            int blockId = await stream.ReadByteAsync().ConfigureAwait(false);
            if (blockId < 0)
                throw GifHelpers.UnexpectedEndOfStreamException();
            switch (blockId)
            {
                case GifExtension.ExtensionIntroducer:
                    return await GifExtension.ReadAsync(stream, controlExtensions).ConfigureAwait(false);
                case GifFrame.ImageSeparator:
                    return await GifFrame.ReadAsync(stream, controlExtensions).ConfigureAwait(false);
                case GifTrailer.TrailerByte:
                    return await GifTrailer.ReadAsync().ConfigureAwait(false);
                default:
                    throw GifHelpers.UnknownBlockTypeException(blockId);
            }
        }

        internal abstract GifBlockKind Kind { get; }
    }
}
