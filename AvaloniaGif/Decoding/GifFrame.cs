// Licensed under the MIT License.
// Copyright (C) 2018 Jumar A. Macato, All Rights Reserved.

using System;

namespace AvaloniaGif.Decoding
{
    public class GifFrame
    {
        public bool HasTransparency, _interlaced, _lctUsed;
        public byte _transparentColorIndex;
        public int _lzwMinCodeSize, _lctSize;
        public long _lzwStreamPos;
        public TimeSpan _frameDelay;
        public FrameDisposal _disposalMethod;
        internal GifColor[] _lctBackBuf;
        public ReadOnlyMemory<GifColor>? _localColorTable;
        internal bool _doBackup;
        public Int32Rect _rect;
    }
}