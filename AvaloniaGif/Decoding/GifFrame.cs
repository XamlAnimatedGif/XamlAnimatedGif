// Licensed under the MIT License.
// Copyright (C) 2018 Jumar A. Macato, All Rights Reserved.

using System;

namespace AvaloniaGif.Decoding
{
    public class GifFrame
    {
        public bool HasTransparency, IsInterlaced, IsLocalColorTableUsed;
        public byte TransparentColorIndex;
        public int LZWMinCodeSize, LocalColorTableSize;
        public long LZWStreamPosition;
        public TimeSpan FrameDelay;
        public FrameDisposal FrameDisposalMethod;
        public ulong LocalColorTableCacheID;
        public bool ShouldBackup;
        public Int32Rect Dimensions;
    }
}