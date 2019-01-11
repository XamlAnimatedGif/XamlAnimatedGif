using System;
using System.Runtime.InteropServices;

namespace AvaloniaGif.NewDecoder
{
    [StructLayout(LayoutKind.Auto)]
    public struct GifHeader
    {
        public int Width;
        public int Height;
        public bool HasGlobalColorTable;
        public int GlobalColorTableSize;
        public Memory<GifColor> GlobalColorTable;
        public GifColor BackgroundColor;
        public long HeaderSize;
    }
}