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
            Span<byte> lenR = stackalloc byte[1];

            stream.Read(lenR);

            var blockLength = (int)lenR[0];

            if (blockLength > 0)
            {
                stream.Read(tempBuf.Slice(0, blockLength));
            }

            return blockLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SkipBlocks(this Stream stream)
        {
            Span<byte> lenR = stackalloc byte[1];
            int blockLength;
            do
            {
                stream.Read(lenR);
                blockLength = (int)lenR[0];
                stream.Position += blockLength;
            } while (blockLength > 0);
        }

        /// <summary>
        /// Read a <see cref="ushort"/> from stream while advancing the position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUShort(this Stream stream)
        {
            Span<byte> val = stackalloc byte[2];

            stream.Read(val);
            var finalVal = stream.ReadUShortS(val);

            return finalVal;
        }


        /// <summary>
        /// Read a <see cref="ushort"/> from stream by providing a <see cref="ArrayPool{T}"/> rented buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUShortS(this Stream stream, Span<byte> tempBuf)
        {
            var val = tempBuf.Slice(0, 2);
            stream.Read(val);
            return SpanToShort(val);
        }

        /// <summary>
        /// Read a <see cref="ushort"/> from stream while advancing the position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReadByte(this Stream stream)
        {
            Span<byte> val = stackalloc byte[1];

            stream.Read(val);
            var finalVal = stream.ReadByteS(val);

            return finalVal;
        }

        /// <summary>
        /// Read a <see cref="ushort"/> from stream by providing a <see cref="ArrayPool{T}"/> rented buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReadByteS(this Stream stream, Span<byte> tempBuf)
        {
            var val = tempBuf.Slice(0, 1);
            stream.Read(val);
            var finalVal = val[0];
            return finalVal;
        }
    }
}