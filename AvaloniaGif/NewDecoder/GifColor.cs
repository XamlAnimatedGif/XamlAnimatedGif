// Licensed under the MIT License.
// Copyright (C) 2018 Jumar A. Macato, All Rights Reserved.

using System.Runtime.InteropServices;

namespace AvaloniaGif.NewDecoder
{
    [StructLayout(LayoutKind.Explicit)]
    public struct GifColor
    {
        [FieldOffset(3)]
        public byte a;

        [FieldOffset(2)]
        public byte r;

        [FieldOffset(1)]
        public byte g;

        [FieldOffset(0)]
        public byte b;

        /// <summary>
        /// A struct that represents a ARGB color and is aligned as
        /// a BGRA bytefield in memory.
        /// </summary>
        /// <param name="r">Red</param>
        /// <param name="g">Green</param>
        /// <param name="b">Blue</param>
        /// <param name="a">Alpha</param>
        public GifColor(byte r, byte g, byte b, byte? a = null)
        {
            this.a = a ?? 255;
            this.r = r;
            this.g = g;
            this.b = b;
        }
    }
}