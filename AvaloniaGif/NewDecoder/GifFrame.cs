// Licensed under the MIT License.
// Copyright (C) 2018 Jumar A. Macato, All Rights Reserved.

using System;

namespace AvaloniaGif.NewDecoder
{
    public class GifFrame
    {
        internal bool _transparency, _interlaced, _lctUsed;
        internal byte _transparentColorIndex;
        internal int _lzwMinCodeSize, _lctSize, _frameX, _frameY, _frameW, _frameH;
        internal long _lzwStreamPos;
        internal TimeSpan _frameDelay;
        internal FrameDisposal _disposalMethod;
        internal Memory<GifColor>? _localColorTable;
    }
}