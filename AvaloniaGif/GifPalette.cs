using Avalonia.Media;

namespace AvaloniaGif
{
    internal class GifPalette
    {
        private readonly Color[] _colors;

        public GifPalette(int? transparencyIndex, Color[] colors)
        {
            TransparencyIndex = transparencyIndex;
            _colors = colors;
        }

        public int? TransparencyIndex { get; }

        public Color this[int i] => _colors[i];
    }
}