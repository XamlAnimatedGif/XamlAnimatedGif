using System.IO;
using System.Threading.Tasks;

namespace AvaloniaGif.Decoding
{
    internal class GifCommentExtension : GifExtension
    {
        internal const int ExtensionLabel = 0xFE;

        public string Text { get; private set; }

        private GifCommentExtension()
        {
        }

        internal override GifBlockKind Kind
        {
            get { return GifBlockKind.SpecialPurpose; }
        }

        internal static GifCommentExtension ReadAsync(Stream stream)
        {
            var comment = new GifCommentExtension();
            comment.ReadInternalAsync(stream);
            return comment;
        }

        private void ReadInternalAsync(Stream stream)
        {
            // Note: at this point, the label (0xFE) has already been read

            var bytes = GifHelpers.ReadDataBlocks(stream);
            if (bytes != null)
                Text = GifHelpers.GetString(bytes);
        }
    }
}
