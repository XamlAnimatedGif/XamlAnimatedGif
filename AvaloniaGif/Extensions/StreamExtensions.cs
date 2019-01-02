﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaGif.Extensions
{
    static class StreamExtensions
    {
        public static void ReadAll(this Stream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int n = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (n == 0)
                    throw new EndOfStreamException();
                totalRead += n;
            }
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