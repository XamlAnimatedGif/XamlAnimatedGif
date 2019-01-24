using System;
using System.IO;
using System.Threading.Tasks;
using XamlAnimatedGif.Extensions;

namespace XamlAnimatedGif.Decoding
{
    internal class GifImageDescriptor
    {
        public bool HasLocalColorTable { get; private set; }
        public bool Interlace { get; private set; }
        public bool IsLocalColorTableSorted { get; private set; }
        public int LocalColorTableSize { get; private set; }
        public GifRect Dimensions { get; private set; }

        private GifImageDescriptor()
        {
        }

        internal static async Task<GifImageDescriptor> ReadAsync(Stream stream)
        {
            var descriptor = new GifImageDescriptor();
            await descriptor.ReadInternalAsync(stream).ConfigureAwait(false);
            return descriptor;
        }

        private async Task ReadInternalAsync(Stream stream)
        {
            byte[] bytes = new byte[9];
            await stream.ReadAllAsync(bytes, 0, bytes.Length).ConfigureAwait(false);

            var x = BitConverter.ToUInt16(bytes, 0);
            var y = BitConverter.ToUInt16(bytes, 2);
            var width = BitConverter.ToUInt16(bytes, 4);
            var height = BitConverter.ToUInt16(bytes, 6);

            Dimensions = new GifRect(x, y, width, height);

            byte packedFields = bytes[8];
            HasLocalColorTable = (packedFields & 0x80) != 0;
            Interlace = (packedFields & 0x40) != 0;
            IsLocalColorTableSorted = (packedFields & 0x20) != 0;
            LocalColorTableSize = 1 << ((packedFields & 0x07) + 1);
        }
    }
}
