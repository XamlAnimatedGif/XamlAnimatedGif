using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace XamlAnimatedGif.Decoding
{
    internal abstract class GifExtension : GifBlock
    {
        internal const int ExtensionIntroducer = 0x21;

        internal new static async Task<GifExtension> ReadAsync(Stream stream, IEnumerable<GifExtension> controlExtensions)
        {
            // Note: at this point, the Extension Introducer (0x21) has already been read

            int label = stream.ReadByte();
            if (label < 0)
                throw new EndOfStreamException();
            switch (label)
            {
                case GifGraphicControlExtension.ExtensionLabel:
                    return await GifGraphicControlExtension.ReadAsync(stream).ConfigureAwait(false);
                case GifCommentExtension.ExtensionLabel:
                    return await GifCommentExtension.ReadAsync(stream).ConfigureAwait(false);
                case GifPlainTextExtension.ExtensionLabel:
                    return await GifPlainTextExtension.ReadAsync(stream, controlExtensions).ConfigureAwait(false);
                case GifApplicationExtension.ExtensionLabel:
                    return await GifApplicationExtension.ReadAsync(stream).ConfigureAwait(false);
                default:
                    throw GifHelpers.UnknownExtensionTypeException(label);
            }
        }
    }
}
