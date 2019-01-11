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
        public static void Skip(this Stream stream, long count)
        {
            stream.Position += count;
        }

        /// <summary>
        /// Read a <see cref="byte"/> from stream while advancing the position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReadByte(this Stream stream)
        {
            var tmpBuf = ArrayPool<byte>.Shared.Rent(2);
            var val = new Span<byte>(tmpBuf, 0, 2);

            stream.Read(val);
            var finalVal = stream.ReadByteA(ref tmpBuf);

            ArrayPool<byte>.Shared.Return(tmpBuf);
            return finalVal;
        }

        /// <summary>
        /// Read a <see cref="byte"/> from stream by providing a <see cref="ArrayPool{T}"/> rented buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReadByteA(this Stream stream, ref byte[] tmpBuf)
        {
            var val = new Span<byte>(tmpBuf, 0, 1);
            stream.Read(val);
            var finalVal = val[0];
            return finalVal;
        }

        /// <summary>
        /// Read a <see cref="ushort"/> from stream while advancing the position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt16 ReadUInt16(this Stream stream)
        {
            var tmpBuf = ArrayPool<byte>.Shared.Rent(2);
            var val = new Span<byte>(tmpBuf, 0, 2);

            stream.Read(val);
            var finalVal = stream.ReadUInt16A(ref tmpBuf);

            ArrayPool<byte>.Shared.Return(tmpBuf);
            return finalVal;
        }


        /// <summary>
        /// Read a <see cref="ushort"/> from stream by providing a <see cref="ArrayPool{T}"/> rented buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt16 ReadUInt16A(this Stream stream, ref byte[] tmpBuf)
        {
            var val = new Span<byte>(tmpBuf, 0, 2);
            stream.Read(val);
            var finalVal = (UInt16)(val[0] | (val[1] << 8));
            return finalVal;
        }
    }
}