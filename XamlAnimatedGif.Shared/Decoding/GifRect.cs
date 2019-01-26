namespace XamlAnimatedGif.Decoding
{
    public readonly struct GifRect
    {
        public  int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public int TotalPixels { get; }

        public GifRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            TotalPixels = width * height;
        }

        public static bool operator ==(GifRect a, GifRect b)
        {
            return ((a.X == b.X) &&
                    (a.Y == b.Y) &&
                    (a.Width == b.Width) &&
                    (a.Height == b.Height));
        }
        public static bool operator !=(GifRect a, GifRect b)
        {
            return !(a == b);
        }
    }
}