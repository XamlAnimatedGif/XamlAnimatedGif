using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AvaloniaGif.Decoding
{
    internal abstract class GifExtension : GifBlock
    {
        internal const int ExtensionIntroducer = 0x21;

        internal new static GifExtension ReadAsync(Stream stream, IEnumerable<GifExtension> controlExtensions)
        {
            // Note: at this point, the Extension Introducer (0x21) has already been read

            int label = stream.ReadByte();
            if (label < 0)
                throw new EndOfStreamException();
            switch (label)
            {
                case GifGraphicControlExtension.ExtensionLabel:
                    return GifGraphicControlExtension.ReadAsync(stream);
                case GifCommentExtension.ExtensionLabel:
                    return GifCommentExtension.ReadAsync(stream);
                case GifPlainTextExtension.ExtensionLabel:
                    return GifPlainTextExtension.ReadAsync(stream, controlExtensions);
                case GifApplicationExtension.ExtensionLabel:
                    return GifApplicationExtension.Read(stream);
                default:
                    throw GifHelpers.UnknownExtensionTypeException(label);
            }
        }
    }
}
