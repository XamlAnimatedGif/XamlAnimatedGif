using System.Runtime.InteropServices;

namespace AvaloniaGif.NewDecoder
{
    [StructLayout(LayoutKind.Auto)]
    public struct GifColor
    {
        public byte r;
        public byte g;
        public byte b;

        public GifColor(byte r, byte g, byte b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }
    }
}