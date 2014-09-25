using System.IO;

namespace XamlAnimatedGif.Decoding
{
    internal class GifImageData
    {
        public byte LzwMinimumCodeSize { get; set; }
        public long CompressedDataStartOffset { get; set; }

        private GifImageData()
        {
        }

        internal static GifImageData ReadImageData(Stream stream)
        {
            var imgData = new GifImageData();
            imgData.Read(stream);
            return imgData;
        }

        private void Read(Stream stream)
        {
            LzwMinimumCodeSize = (byte)stream.ReadByte();
            CompressedDataStartOffset = stream.Position;
            GifHelpers.ReadDataBlocks(stream, true);
        }
    }
}
