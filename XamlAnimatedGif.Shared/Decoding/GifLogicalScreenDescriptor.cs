using System;
using System.IO;
using System.Threading.Tasks;
using XamlAnimatedGif.Extensions;

namespace XamlAnimatedGif.Decoding
{
    internal class GifLogicalScreenDescriptor
    {
        public bool HasGlobalColorTable { get; private set; }
        public int ColorResolution { get; private set; }
        public bool IsGlobalColorTableSorted { get; private set; }
        public int GlobalColorTableSize { get; private set; }
        public int BackgroundColorIndex { get; private set; }
        public double PixelAspectRatio { get; private set; }
        public GifRect Dimensions { get; private set; }

        internal static async Task<GifLogicalScreenDescriptor> ReadAsync(Stream stream)
        {
            var descriptor = new GifLogicalScreenDescriptor();
            await descriptor.ReadInternalAsync(stream).ConfigureAwait(false);
            return descriptor;
        }

        private async Task ReadInternalAsync(Stream stream)
        {
            byte[] bytes = new byte[7];
            await stream.ReadAllAsync(bytes, 0, bytes.Length).ConfigureAwait(false);

            var width = BitConverter.ToUInt16(bytes, 0);
            var height = BitConverter.ToUInt16(bytes, 2);
            Dimensions = new GifRect(0, 0, width, height);

            byte packedFields = bytes[4];
            HasGlobalColorTable = (packedFields & 0x80) != 0;
            ColorResolution = ((packedFields & 0x70) >> 4) + 1;
            IsGlobalColorTableSorted = (packedFields & 0x08) != 0;
            GlobalColorTableSize = 1 << ((packedFields & 0x07) + 1);
            BackgroundColorIndex = bytes[5];
            PixelAspectRatio =
                bytes[6] == 0
                    ? 0.0
                    : (15 + bytes[6]) / 64.0;
        }
        
    }
}
