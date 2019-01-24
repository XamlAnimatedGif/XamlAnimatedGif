using System.IO;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using XamlAnimatedGif.Decoding;

namespace XamlAnimatedGif.Extensions
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
        public static int ReadBlock(this Stream stream, byte[] tempBuf)
        {
            stream.Read(tempBuf, 0, 1);

            var blockLength = (int)tempBuf[0];

            if (blockLength > 0)
                stream.Read(tempBuf,0, blockLength);

            // Guard against infinite loop.
            if (stream.Position >= stream.Length)
                throw new InvalidGifStreamException("Reach the end of the filestream without trailer block.");

            return blockLength;
        }

        /// <summary>
        /// Skips GIF blocks until it encounters an empty block.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SkipBlocks(this Stream stream, byte[] tempBuf)
        { 
            int blockLength;
            do
            {
                stream.Read(tempBuf, 0,1);
                blockLength = (int)tempBuf[0];
                stream.Position += blockLength;

                // Guard against infinite loop.
                if (stream.Position >= stream.Length)
                    throw new InvalidGifStreamException("Reach the end of the filestream without trailer block.");

            } while (blockLength > 0);
        }

        /// <summary>
        /// Read a <see cref="ushort"/> from stream by providing a temporary buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUShortS(this Stream stream, byte[] tempBuf)
        {
            stream.Read(tempBuf, 0,2);
            return SpanToShort(tempBuf);
        }

        /// <summary>
        /// Read a <see cref="ushort"/> from stream by providing a temporary buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReadByteS(this Stream stream, byte[] tempBuf)
        {
            stream.Read(tempBuf,0,1);
            var finalVal = tempBuf[0];
            return finalVal;
        }
        public static Stream AsBuffered(this Stream stream)
        {
            var bs = stream as BufferedStream;
            if (bs != null)
                return bs;
            return new BufferedStream(stream);
        }

        public static async Task CopyToAsync(this Stream source, Stream destination, IProgress<long> progress, int bufferSize = 81920, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte[] buffer = new byte[bufferSize];
            int bytesRead;
            long bytesCopied = 0;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                bytesCopied += bytesRead;
                progress?.Report(bytesCopied);
            }
        }
    }
}