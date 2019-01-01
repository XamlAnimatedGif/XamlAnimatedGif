using System;
using System.IO;
using System.Threading.Tasks;
using AvaloniaGif.Extensions;

namespace AvaloniaGif.Decoding
{
    internal class GifLogicalScreenDescriptor : IGifRect
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public bool HasGlobalColorTable { get; private set; }
        public int ColorResolution { get; private set; }
        public bool IsGlobalColorTableSorted { get; private set; }
        public int GlobalColorTableSize { get; private set; }
        public int BackgroundColorIndex { get; private set; }
        public double PixelAspectRatio { get; private set; }

        internal static GifLogicalScreenDescriptor ReadAsync(Stream stream)
        {
            var descriptor = new GifLogicalScreenDescriptor();
            descriptor.ReadInternalAsync(stream);
            return descriptor;
        }

        private void ReadInternalAsync(Stream stream)
        {
            byte[] bytes = new byte[7];
            stream.ReadAll(bytes, 0, bytes.Length);

            Width = BitConverter.ToUInt16(bytes, 0);
            Height = BitConverter.ToUInt16(bytes, 2);
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

        int IGifRect.Left
        {
            get { return 0; }
        }

        int IGifRect.Top
        {
            get { return 0; }
        }
    }
}
