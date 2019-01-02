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

        internal static GifImageData Read(Stream stream)
        {
            var imgData = new GifImageData();
            imgData.ReadInternal(stream);
            return imgData;
        }

        private void ReadInternal(Stream stream)
        {
            LzwMinimumCodeSize = (byte)stream.ReadByte();
            CompressedDataStartOffset = stream.Position;
            GifHelpers.ConsumeDataBlocks(stream);
        }
    }
}
