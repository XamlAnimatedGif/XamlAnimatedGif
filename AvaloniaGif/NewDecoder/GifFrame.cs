using System;

namespace AvaloniaGif.NewDecoder
{
    public class GifFrame
    {
        internal TimeSpan _frameDelay;
        internal GifColor[] _localColorTable;
        internal byte _transparentColorIndex, _lzwCodeSize;
        internal int _disposalMethod, _localColorTableSize, _frameX, _frameY, _frameW, _frameH;
        internal bool _transparency, _interlaced, _localColorTableUsed;
        internal long _lzwStreamPos;
    }
}