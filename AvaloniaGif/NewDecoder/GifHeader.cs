using System;

namespace AvaloniaGif.NewDecoder
{
    public struct GifHeader
    {
        public int Width;
        public int Height;
        public bool HasGlobalColorTable;
        public int GlobalColorTableSize;
        public Memory<GifColor> GlobalColorTable;
        public GifColor BackgroundColor;
    }
}