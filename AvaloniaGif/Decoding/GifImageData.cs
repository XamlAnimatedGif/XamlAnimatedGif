using System.IO;
using System.Threading.Tasks;

namespace AvaloniaGif.Decoding
{
    internal class GifImageData
    {
        public byte LzwMinimumCodeSize { get; set; }
        public long CompressedDataStartOffset { get; set; }

        private GifImageData()
        {
        }

        internal static GifImageData ReadAsync(Stream stream)
        {
            var imgData = new GifImageData();
            imgData.ReadInternalAsync(stream);
            return imgData;
        }

        private void ReadInternalAsync(Stream stream)
        {
            LzwMinimumCodeSize = (byte)stream.ReadByte();
            CompressedDataStartOffset = stream.Position;
            GifHelpers.ConsumeDataBlocks(stream);
        }
    }
}
