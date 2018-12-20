using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaGif.Decoding;
using AvaloniaGif.Decompression;
using AvaloniaGif.Extensions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Runtime.InteropServices;
using Avalonia.Animation;
using Avalonia;
using Avalonia.Platform;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using TaskEx = System.Threading.Tasks.Task;

namespace AvaloniaGif
{
    public partial class GifRenderer : AvaloniaObject, IDisposable
    {
        public Stream _sourceStream;
        private readonly bool _isSourceStreamOwner;
        private readonly GifDataStream _metadata;
        private readonly Dictionary<int, GifPalette> _palettes;
        public readonly WriteableBitmap _bitmap;
        private readonly int _stride;
        private readonly byte[] _previousBackBuffer;
        private readonly byte[] _indexStreamBuffer;
        public int FrameCount => _metadata.Frames.Count;
        public readonly List<TimeSpan> GifFrameTimes = new List<TimeSpan>();
        public Vector DPI { get; set; } = new Vector(96, 96);
        private int _previousFrameIndex;
        private GifFrame _previousFrame;

        internal GifRenderer(Stream stream)
        {
            _metadata = GifDataStream.ReadAsync(stream).Result;
            _sourceStream = stream;
            _palettes = CreatePalettes(_metadata);
            _bitmap = CreateBitmap(_metadata);

            var desc = _metadata.Header.LogicalScreenDescriptor;
            _stride = 4 * ((desc.Width * 32 + 31) / 32);
            _previousBackBuffer = new byte[desc.Height * _stride];
            _indexStreamBuffer = CreateIndexStreamBuffer(_metadata, _sourceStream);

            foreach (var frame in _metadata.Frames)
                GifFrameTimes.Add(GetFrameDelay(frame));

        }
        private IterationCount GetActualIterationCount(GifDataStream metadata, IterationCount IterationCount)
        {
            return IterationCount == default(IterationCount)
                    ? GetIterationCountFromGif(metadata)
                    : IterationCount;
        }

        private WriteableBitmap CreateBitmap(GifDataStream metadata)
        {
            var desc = metadata.Header.LogicalScreenDescriptor;
            var bitmap = new WriteableBitmap(new PixelSize(desc.Width, desc.Height), DPI, PixelFormat.Bgra8888);
            return bitmap;
        }

        private Dictionary<int, GifPalette> CreatePalettes(GifDataStream metadata)
        {
            var palettes = new Dictionary<int, GifPalette>();
            Color[] globalColorTable = null;
            if (metadata.Header.LogicalScreenDescriptor.HasGlobalColorTable)
            {
                globalColorTable =
                    metadata.GlobalColorTable
                        .Select(gc => Color.FromArgb(0xFF, gc.R, gc.G, gc.B))
                        .ToArray();
            }

            for (int i = 0; i < metadata.Frames.Count; i++)
            {
                var frame = metadata.Frames[i];
                var colorTable = globalColorTable;
                if (frame.Descriptor.HasLocalColorTable)
                {
                    colorTable =
                        frame.LocalColorTable
                            .Select(gc => Color.FromArgb(0xFF, gc.R, gc.G, gc.B))
                            .ToArray();
                }

                int? transparencyIndex = null;
                var gce = frame.GraphicControl;
                if (gce != null && gce.HasTransparency)
                {
                    transparencyIndex = gce.TransparencyIndex;
                }

                palettes[i] = new GifPalette(transparencyIndex, colorTable);
            }

            return palettes;
        }

        private static byte[] CreateIndexStreamBuffer(GifDataStream metadata, Stream stream)
        {
            // Find the size of the largest frame pixel data
            // (ignoring the fact that we include the next frame's header)

            long lastSize = stream.Length - metadata.Frames.Last().ImageData.CompressedDataStartOffset;
            long maxSize = lastSize;
            if (metadata.Frames.Count > 1)
            {
                var sizes = metadata.Frames.Zip(metadata.Frames.Skip(1),
                    (f1, f2) => f2.ImageData.CompressedDataStartOffset - f1.ImageData.CompressedDataStartOffset);
                maxSize = Math.Max(sizes.Max(), lastSize);
            }
            // Need 4 extra bytes so that BitReader doesn't need to check the size for every read
            return new byte[maxSize + 4];
        }

        internal async Task RenderFrameAsync(int frameIndex, CancellationToken cancellationToken)
        {
            if (frameIndex < 0)
                return;

            var frame = _metadata.Frames[frameIndex];
            var desc = frame.Descriptor;
            var rect = GetFixedUpFrameRect(desc);

            using (var lockedBitmap = _bitmap.Lock())
            using (var indexStream = await GetIndexStreamAsync(frame, cancellationToken))
            {

                if (frameIndex < _previousFrameIndex)
                    ClearArea(new Int32Rect(0, 0, _metadata.Header.LogicalScreenDescriptor.Width,
                     _metadata.Header.LogicalScreenDescriptor.Height), lockedBitmap);
                else
                    DisposePreviousFrame(frame, lockedBitmap);

                int bufferLength = 4 * rect.Width;
                byte[] indexBuffer = new byte[desc.Width];
                byte[] lineBuffer = new byte[bufferLength];

                var palette = _palettes[frameIndex];
                int transparencyIndex = palette.TransparencyIndex ?? -1;

                var rows = desc.Interlace
                    ? InterlacedRows(rect.Height)
                    : NormalRows(rect.Height);

                foreach (int y in rows)
                {
                    indexStream.ReadAll(indexBuffer, 0, desc.Width);

                    int offset = (desc.Top + y) * _stride + desc.Left * 4;

                    if (transparencyIndex >= 0)
                    {
                        CopyFromBitmap(lineBuffer, lockedBitmap, offset, bufferLength);
                    }

                    for (int x = 0; x < rect.Width; x++)
                    {
                        byte index = indexBuffer[x];
                        int i = 4 * x;
                        if (index != transparencyIndex)
                        {
                            WriteColor(lineBuffer, palette[index], i);
                        }
                    }
                    CopyToBitmap(lineBuffer, lockedBitmap, offset, bufferLength);

                }
                _previousFrame = frame;
                _previousFrameIndex = frameIndex;
            }
        }

        private static IEnumerable<int> NormalRows(int height)
        {
            return Enumerable.Range(0, height);
        }

        private static IEnumerable<int> InterlacedRows(int height)
        {
            /*
             * 4 passes:
             * Pass 1: rows 0, 8, 16, 24...
             * Pass 2: rows 4, 12, 20, 28...
             * Pass 3: rows 2, 6, 10, 14...
             * Pass 4: rows 1, 3, 5, 7...
             * */
            var passes = new[]
            {
                new { Start = 0, Step = 8 },
                new { Start = 4, Step = 8 },
                new { Start = 2, Step = 4 },
                new { Start = 1, Step = 2 }
            };
            foreach (var pass in passes)
            {
                int y = pass.Start;
                while (y < height)
                {
                    yield return y;
                    y += pass.Step;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyToBitmap(byte[] buffer, ILockedFramebuffer bitmap, int offset, int length)
        {
            Marshal.Copy(buffer, 0, bitmap.Address + offset, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyFromBitmap(byte[] buffer, ILockedFramebuffer bitmap, int offset, int length)
        {
            Marshal.Copy(bitmap.Address + offset, buffer, 0, length);
        }

        private void WriteColor(byte[] lineBuffer, Color color, int startIndex)
        {
            lineBuffer[startIndex] = color.B;
            lineBuffer[startIndex + 1] = color.G;
            lineBuffer[startIndex + 2] = color.R;
            lineBuffer[startIndex + 3] = color.A;
        }

        private void DisposePreviousFrame(GifFrame currentFrame, ILockedFramebuffer bitmap)
        {
            var pgce = _previousFrame?.GraphicControl;

            if (pgce != null)
            {
                switch (pgce.DisposalMethod)
                {
                    case GifFrameDisposalMethod.None:
                    case GifFrameDisposalMethod.DoNotDispose:
                        {
                            // Leave previous frame in place
                            break;
                        }
                    case GifFrameDisposalMethod.RestoreBackground:
                        {
                            ClearArea(GetFixedUpFrameRect(_previousFrame.Descriptor), bitmap);
                            break;
                        }
                    case GifFrameDisposalMethod.RestorePrevious:
                        {
                            CopyToBitmap(_previousBackBuffer, bitmap, 0, _previousBackBuffer.Length);

                            break;
                        }
                }
            }

            var gce = currentFrame.GraphicControl;
            if (gce != null && gce.DisposalMethod == GifFrameDisposalMethod.RestorePrevious)
            {
                CopyFromBitmap(_previousBackBuffer, bitmap, 0, _previousBackBuffer.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearArea(Int32Rect rect, ILockedFramebuffer bitmap)
        {
            int bufferLength = 4 * rect.Width;
            byte[] lineBuffer = new byte[bufferLength];
            for (int y = 0; y < rect.Height; y++)
            {
                int offset = (rect.Y + y) * _stride + 4 * rect.X;
                CopyToBitmap(lineBuffer, bitmap, offset, bufferLength);
            }

        }

        private async Task<Stream> GetIndexStreamAsync(GifFrame frame, CancellationToken cancellationToken)
        {
            var data = frame.ImageData;
            cancellationToken.ThrowIfCancellationRequested();
            _sourceStream.Seek(data.CompressedDataStartOffset, SeekOrigin.Begin);
            using (var ms = new MemoryStream(_indexStreamBuffer))
            {
                await GifHelpers.CopyDataBlocksToStreamAsync(_sourceStream, ms, cancellationToken).ConfigureAwait(false);
            }
            var lzwStream = new LzwDecompressStream(_indexStreamBuffer, data.LzwMinimumCodeSize);
            return lzwStream;
        }

        private TimeSpan GetFrameDelay(GifFrame frame)
        {
            return TimeSpan.FromMilliseconds(frame.GraphicControl?.Delay ?? 100);
        }

        private IterationCount GetIterationCountFromGif(GifDataStream metadata)
        {
            if (metadata.IterationCount == 0)
                return IterationCount.Infinite;

            return new IterationCount(metadata.IterationCount);
        }

        private Int32Rect GetFixedUpFrameRect(GifImageDescriptor desc)
        {
            int width = Math.Min(desc.Width, _bitmap.PixelSize.Width - desc.Left);
            int height = Math.Min(desc.Height, _bitmap.PixelSize.Height - desc.Top);
            return new Int32Rect(desc.Left, desc.Top, width, height);
        }

        public void Dispose()
        {
            _bitmap.Dispose();
        }
    }
}