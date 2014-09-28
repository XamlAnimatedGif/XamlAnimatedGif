using System.IO;
using System.Threading.Tasks;

namespace XamlAnimatedGif.Decoding
{
    internal class GifHeader : GifBlock
    {
        public string Signature { get; private set; }
        public string Version { get; private set; }
        public GifLogicalScreenDescriptor LogicalScreenDescriptor { get; private set; }

        private GifHeader()
        {
        }

        internal override GifBlockKind Kind
        {
            get { return GifBlockKind.Other; }
        }

        internal static async Task<GifHeader> ReadAsync(Stream stream)
        {
            var header = new GifHeader();
            await header.ReadInternalAsync(stream);
            return header;
        }

        private async Task ReadInternalAsync(Stream stream)
        {
            Signature = await GifHelpers.ReadStringAsync(stream, 3);
            if (Signature != "GIF")
                throw GifHelpers.InvalidSignatureException(Signature);
            Version = await GifHelpers.ReadStringAsync(stream, 3);
            if (Version != "87a" && Version != "89a")
                throw GifHelpers.UnsupportedVersionException(Version);
            LogicalScreenDescriptor = await GifLogicalScreenDescriptor.ReadAsync(stream);
        }
    }
}
