// Licensed under the MIT License.
// Copyright (C) 2018 Jumar A. Macato, All Rights Reserved.

using System;
using System.Runtime.InteropServices;

namespace XamlAnimatedGif.Decoding
{
    public class GifHeader
    {
        public bool HasGlobalColorTable;
        public int GlobalColorTableSize;
        public ulong GlobalColorTable;
        public int BackgroundColorIndex;
        public long HeaderSize;
        public int Iterations = 0;
        public GifRect Rect;
    }
}