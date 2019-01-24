using System.Runtime.InteropServices;

namespace XamlAnimatedGif
{
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct GifColor
    {
        [FieldOffset(3)]
        public readonly byte A;

        [FieldOffset(2)]
        public readonly byte R;

        [FieldOffset(1)]
        public readonly byte G;

        [FieldOffset(0)]
        public readonly byte B;

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
            this.A = a ?? 255;
            this.R = r;
            this.G = g;
            this.B = b;
        }
    }
}