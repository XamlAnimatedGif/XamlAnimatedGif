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
using System.Buffers;

namespace AvaloniaGif
{
    public partial class GifRenderer : AvaloniaObject, IDisposable
    {
        private readonly Stream _sourceStream;
        private readonly bool _isSourceStreamOwner;
        private readonly GifDataStream _metadata;
        private readonly Dictionary<int, GifPalette> _palettes;
        private readonly int _stride;
        private readonly byte[] _previousBackBuffer;
        private readonly byte[] _indexStreamBuffer;
        private readonly PixelSize _gifSize;

        private int _previousFrameIndex;
        private GifFrame _previousFrame;
        private byte[] _bitmapScratchBuffer;
        private Mutex _bitmapMutex = new Mutex();

        public int FrameCount => _metadata.Frames.Length;
        public readonly Memory<TimeSpan> GifFrameTimes;
        public Vector DPI { get; set; } = new Vector(96, 96);

        internal GifRenderer(Stream stream)
        {
            _metadata = GifDataStream.ReadAsync(stream);
            _sourceStream = stream;
            _palettes = CreatePalettes(_metadata);

            var desc = _metadata.Header.LogicalScreenDescriptor;

            this._bitmapScratchBuffer = new byte[desc.Height * desc.Width * 4];
            _gifSize = new PixelSize(desc.Width, desc.Height);

            _stride = 4 * ((desc.Width * 32 + 31) / 32);
            _previousBackBuffer = ArrayPool<byte>.Shared.Rent(desc.Height * _stride);

            var isbs = CreateIndexStreamBufferSize(_metadata, _sourceStream);
            _indexStreamBuffer = ArrayPool<byte>.Shared.Rent(isbs);

            GifFrameTimes = new TimeSpan[_metadata.Frames.Length];

            for (int i = 0; i < _metadata.Frames.Length; i++)
                GifFrameTimes.Span[i] = GetFrameDelay(_metadata.Frames.Span[i]);
        }
        private IterationCount GetActualIterationCount(GifDataStream metadata, IterationCount IterationCount)
        {
            return IterationCount == default(IterationCount)
                    ? GetIterationCountFromGif(metadata)
                    : IterationCount;
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

            for (int i = 0; i < metadata.Frames.Length; i++)
            {
                var frame = metadata.Frames.Span[i];
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

        private static int CreateIndexStreamBufferSize(GifDataStream metadata, Stream stream)
        {
            // Find the size of the largest frame pixel data
            // (ignoring the fact that we include the next frame's header)
            var frames = metadata.Frames.Span;
            long lastSize = stream.Length - metadata.Frames.Span[metadata.Frames.Length - 1].ImageData.CompressedDataStartOffset;
            long maxSize = lastSize;
            if (metadata.Frames.Length > 1)
            { 
                var sizes = ArrayPool<long>.Shared.Rent(frames.Length * 2);
                int sizeCnt = 0;
                for (int s = 0; s < frames.Length; s++)
                {
                    if (s + 1 >= frames.Length) break;
                    sizes[sizeCnt] = frames[s + 1].ImageData.CompressedDataStartOffset - frames[s].ImageData.CompressedDataStartOffset;
                    sizeCnt++;
                }
                maxSize = Math.Max(sizes.Max(), lastSize);
                ArrayPool<long>.Shared.Return(sizes, true);
            }
            // Need 4 extra bytes so that BitReader doesn't need to check the size for every read
            return (int)(maxSize + 4);
        }
        volatile bool hasNewFrame = false;

        /// <summary>
        /// Renders the desired frame index onto the scratch bitmap buffer.
        /// </summary>
        internal async Task RenderFrameAsync(int frameIndex)
        {
            _bitmapMutex.WaitOne();

            if (frameIndex < 0)
                return;

            var frame = _metadata.Frames.Span[frameIndex];
            var desc = frame.Descriptor;
            var rect = GetFixedUpFrameRect(desc);

            using (var indexStream = GetIndexStreamAsync(frame))
            {
                if (frameIndex < _previousFrameIndex)
                    ClearAreaBackBuffer(new Int32Rect(0, 0, _metadata.Header.LogicalScreenDescriptor.Width,
                     _metadata.Header.LogicalScreenDescriptor.Height));
                else
                    DisposePreviousFrame(frame);

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
                        CopyFromScratchBuffer(lineBuffer, offset, bufferLength);
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
                    CopyToScratchBuffer(lineBuffer, offset, bufferLength);

                }
                _previousFrame = frame;
                _previousFrameIndex = frameIndex;
            }

            hasNewFrame = true;
            _bitmapMutex.ReleaseMutex();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IEnumerable<int> NormalRows(int height)
        {
            return Enumerable.Range(0, height); 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        public WriteableBitmap CreateBitmapForRender()
        {
            return new WriteableBitmap(_gifSize, DPI, PixelFormat.Bgra8888);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyToScratchBuffer(byte[] buffer, int offset, int length)
        {
            Buffer.BlockCopy(buffer, 0, _bitmapScratchBuffer, offset, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyFromScratchBuffer(byte[] buffer, int offset, int length)
        {
            Buffer.BlockCopy(_bitmapScratchBuffer, offset, buffer, 0, length);
        }

        /// <summary>
        /// Transfers scratch bitmap to a target <see cref="ILockedFramebuffer"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TransferScratchToBitmap(ILockedFramebuffer _bitmap)
        {
            _bitmapMutex.WaitOne();
            if (hasNewFrame)
            {
                Marshal.Copy(_bitmapScratchBuffer, 0, _bitmap.Address, _bitmapScratchBuffer.Length);
                hasNewFrame = false;
            }
            _bitmapMutex.ReleaseMutex();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteColor(byte[] lineBuffer, Color color, int startIndex)
        {
            lineBuffer[startIndex] = color.B;
            lineBuffer[startIndex + 1] = color.G;
            lineBuffer[startIndex + 2] = color.R;
            lineBuffer[startIndex + 3] = color.A;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DisposePreviousFrame(GifFrame currentFrame)
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
                            ClearAreaBackBuffer(GetFixedUpFrameRect(_previousFrame.Descriptor));
                            break;
                        }
                    case GifFrameDisposalMethod.RestorePrevious:
                        {
                            CopyToScratchBuffer(_previousBackBuffer, 0, _previousBackBuffer.Length);

                            break;
                        }
                }
            }

            var gce = currentFrame.GraphicControl;
            if (gce != null && gce.DisposalMethod == GifFrameDisposalMethod.RestorePrevious)
            {
                CopyFromScratchBuffer(_previousBackBuffer, 0, _previousBackBuffer.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearAreaBackBuffer(Int32Rect rect)
        {
            var bufferLength = 4 * rect.Width;
            var lineBuffer = ArrayPool<byte>.Shared.Rent(bufferLength);
            for (int y = 0; y < rect.Height; y++)
            {
                int offset = (rect.Y + y) * _stride + 4 * rect.X;
                CopyToScratchBuffer(lineBuffer, offset, bufferLength);
            }
            ArrayPool<byte>.Shared.Return(lineBuffer, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Stream GetIndexStreamAsync(GifFrame frame)
        {
            var data = frame.ImageData;
            _sourceStream.Seek(data.CompressedDataStartOffset, SeekOrigin.Begin);
            using (var ms = new MemoryStream(_indexStreamBuffer))
            {
                GifHelpers.CopyDataBlocksToStream(_sourceStream, ms);
            }
            var lzwStream = new LzwDecompressStream(_indexStreamBuffer, data.LzwMinimumCodeSize);
            return lzwStream;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TimeSpan GetFrameDelay(GifFrame frame)
        {
            var gce = frame.GraphicControl;
            if (gce != null)
            {
                if (gce.Delay != 0)
                    return TimeSpan.FromMilliseconds(gce.Delay);
            }
            return TimeSpan.FromMilliseconds(100);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IterationCount GetIterationCountFromGif(GifDataStream metadata)
        {
            if (metadata.IterationCount == 0)
                return IterationCount.Infinite;

            return new IterationCount(metadata.IterationCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Int32Rect GetFixedUpFrameRect(GifImageDescriptor desc)
        {
            int width = Math.Min(desc.Width, _gifSize.Width - desc.Left);
            int height = Math.Min(desc.Height, _gifSize.Height - desc.Top);
            return new Int32Rect(desc.Left, desc.Top, width, height);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DisposeCore()
        {
            ArrayPool<byte>.Shared.Return(_previousBackBuffer, true);
            ArrayPool<byte>.Shared.Return(_indexStreamBuffer, true);
        }

        public void Dispose()
        {
            DisposeCore();
        }

        ~GifRenderer()
        {
            DisposeCore();
        }
    }
}