using System.Runtime.InteropServices;

namespace AvaloniaGif.Decoding
{
    [StructLayout(LayoutKind.Explicit)]
    internal readonly struct GifColor
    {
        [FieldOffset(3)]
        public readonly byte a;

        [FieldOffset(2)]
        public readonly byte r;

        [FieldOffset(1)]
        public readonly byte g;

        [FieldOffset(0)]
        public readonly byte b;

        /// <summary>
        /// A struct that represents a ARGB color and is aligned as
        /// a BGRA bytefield in memory.
        /// </summary>
        /// <param name="r">Red</param>
        /// <param name="g">Green</param>
        /// <param name="b">Blue</param>
        /// <param name="a">Alpha</param>
        public GifColor(byte r, byte g, byte b, byte? a = null)
        {
            this.a = a ?? 255;
            this.r = r;
            this.g = g;
            this.b = b;
        }
    }
}