// Licensed under the MIT License.
// Copyright (C) 2018 Jumar A. Macato, All Rights Reserved.

using System.IO;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AvaloniaGif.NewDecoder
{
    [DebuggerStepThrough]
    internal static class StreamExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort SpanToShort(Span<byte> b)
            => (ushort)(b[0] | (b[1] << 8));


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Skip(this Stream stream, long count)
        {
            stream.Position += count;
        }

        /// <summary>
        /// Read a Gif block from stream while advancing the position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadBlock(this Stream stream, Span<byte> tempBuf)
        {
            stream.Read(tempBuf.Slice(0, 1));

            var blockLength = (int)tempBuf[0];

            if (blockLength > 0)
                stream.Read(tempBuf.Slice(0, blockLength));

            return blockLength;
        }

        /// <summary>
        /// Skips GIF blocks until it encounters an empty block.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SkipBlocks(this Stream stream)
        {
            Span<byte> val = stackalloc byte[1];
            int blockLength;
            do
            {
                stream.Read(val);
                blockLength = (int)val[0];
                stream.Position += blockLength;

                // Guard against infinite loop.
                if (stream.Position >= stream.Length)
                    throw new InvalidProgramException("Reach the end of the filestream without trailer block.");

            } while (blockLength > 0);
        }

        /// <summary>
        /// Read a <see cref="ushort"/> from stream by providing a temporary buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUShortS(this Stream stream)
        {
            Span<byte> val = stackalloc byte[2];
            stream.Read(val);
            return SpanToShort(val);
        }

        /// <summary>
        /// Read a <see cref="ushort"/> from stream by providing a temporary buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReadByteS(this Stream stream)
        {
            Span<byte> val = stackalloc byte[1];
            stream.Read(val);
            var finalVal = val[0];
            return finalVal;
        }
    }
}