// Licensed under the MIT License.
// Copyright (C) 2018 Jumar A. Macato, All Rights Reserved.

using System;
using System.Runtime.InteropServices;

namespace AvaloniaGif.NewDecoder
{
    public class GifHeader
    {
        public bool HasGlobalColorTable;
        public int GlobalColorTableSize;
        public Memory<GifColor>? GlobalColorTable;
        public int BackgroundColorIndex;
        public long HeaderSize;
        public Int32Rect Rect;
    }
}