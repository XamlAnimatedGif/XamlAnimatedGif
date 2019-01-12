// Licensed under the MIT License.
// Copyright (C) 2018 Jumar A. Macato, All Rights Reserved.

using System;

namespace AvaloniaGif.NewDecoder
{
    public class GifFrame
    {
        public bool HasTransparency, _interlaced, _lctUsed;
        public byte _transparentColorIndex;
        public int _lzwMinCodeSize, _lctSize, _frameX, _frameY, _frameW, _frameH;
        public long _lzwStreamPos;
        public TimeSpan _frameDelay;
        public FrameDisposal _disposalMethod;
        public Memory<GifColor>? _localColorTable;
    }
}