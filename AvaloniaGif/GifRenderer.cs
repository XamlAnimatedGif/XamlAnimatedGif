using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaGif.Decoding;
using AvaloniaGif.Decompression;
using AvaloniaGif.Extensions;

namespace AvaloniaGif
{
    public class GifRenderer : AvaloniaObject, IDisposable
    {
        private readonly Stream _sourceStream;
        private readonly GifDataStream _metadata;
        private readonly Dictionary<int, GifPalette> _palettes;
        private readonly int _stride;
        private readonly byte[] _indexStreamBuffer;
        private readonly PixelSize _gifSize;

        private int _previousFrameIndex;
        private GifFrame _previousFrame;
        private byte[] _previousBackBuffer;
        private readonly byte[] _bitmapScratchBuffer;
        private readonly Mutex _bitmapMutex = new Mutex();

        public int FrameCount => _metadata.Frames.Length;
        public readonly Memory<TimeSpan> GifFrameTimes;
        public Vector DPI = new Vector(96, 96);

        private int _zero = 0;
        
        internal GifRenderer(Stream stream)
        {
            _metadata = GifDataStream.Read(stream);
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

            for (var i = 0; i < _metadata.Frames.Length; i++)
                GifFrameTimes.Span[i] = GetFrameDelay(_metadata.Frames.Span[i]);
        }

        private IterationCount GetActualIterationCount(GifDataStream metadata, IterationCount iterationCount)
        {
            return iterationCount == default(IterationCount)
                ? GetIterationCountFromGif(metadata)
                : iterationCount;
        }

        private static Dictionary<int, GifPalette> CreatePalettes(GifDataStream metadata)
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

            for (var i = 0; i < metadata.Frames.Length; i++)
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
            var lastSize = stream.Length -
                           metadata.Frames.Span[metadata.Frames.Length - 1].ImageData.CompressedDataStartOffset;
            var maxSize = lastSize;

            if (metadata.Frames.Length <= 1) return (int) (maxSize + 4);
            var sizes = ArrayPool<long>.Shared.Rent(frames.Length * 2);
            var sizeCnt = 0;
            for (var s = 0; s < frames.Length; s++)
            {
                if (s + 1 >= frames.Length) break;
                sizes[sizeCnt] = frames[s + 1].ImageData.CompressedDataStartOffset -
                                 frames[s].ImageData.CompressedDataStartOffset;
                sizeCnt++;
            }

            maxSize = Math.Max(sizes.Max(), lastSize);
            ArrayPool<long>.Shared.Return(sizes, true);


            // Need 4 extra bytes so that BitReader doesn't need to check the size for every read
            return (int) (maxSize + 4);
        }

        private volatile bool _hasNewFrame = false;

        /// <summary>
        /// Renders the desired frame index onto the scratch bitmap buffer.
        /// </summary>
        internal void RenderFrame(int frameIndex)

        {
            _bitmapMutex.WaitOne();

            if (frameIndex < 0)
                return;

            var frame = _metadata.Frames.Span[frameIndex];
            var desc = frame.Descriptor;
            var rect = GetFixedUpFrameRect(desc);

            using (var indexStream = GetIndexStream(frame))
            {
                if (frameIndex < _previousFrameIndex)
                    ClearAreaBackBuffer(new Int32Rect(0, 0, _metadata.Header.LogicalScreenDescriptor.Width,
                        _metadata.Header.LogicalScreenDescriptor.Height));
                else
                    DisposePreviousFrame(frame);

                var bufferLength = 4 * rect.Width;

                var indexBuffer = ArrayPool<byte>.Shared.Rent(desc.Width);
                var lineBuffer = ArrayPool<byte>.Shared.Rent(bufferLength);

                var palette = _palettes[frameIndex];
                var transparencyIndex = palette.TransparencyIndex ?? -1;

                var rows = desc.Interlace
                    ? InterlacedRows(rect.Height)
                    : NormalRows(rect.Height);

                foreach (var y in rows)
                {
                    indexStream.ReadAll(indexBuffer, 0, desc.Width);

                    var offset = (desc.Top + y) * _stride + desc.Left * 4;

                    if (transparencyIndex >= 0)
                    {
                        CopyFromScratchBuffer(ref lineBuffer, ref offset, ref bufferLength);
                    }

                    for (var x = 0; x < rect.Width; x++)
                    {
                        var index = indexBuffer[x];
                        var i = 4 * x;

                        if (index == transparencyIndex)
                            continue;

                        var color = palette[index];
                        WriteColor(ref lineBuffer, ref color, ref i);
                    }

                    CopyToScratchBuffer(ref lineBuffer, ref offset, ref bufferLength);
                }

                ArrayPool<byte>.Shared.Return(indexBuffer, true);
                ArrayPool<byte>.Shared.Return(lineBuffer, true);

                _previousFrame = frame;
                _previousFrameIndex = frameIndex;
            }

            _hasNewFrame = true;
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
                new {Start = 0, Step = 8},
                new {Start = 4, Step = 8},
                new {Start = 2, Step = 4},
                new {Start = 1, Step = 2}
            };
            foreach (var pass in passes)
            {
                var y = pass.Start;
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
        private void CopyToScratchBuffer(ref byte[] buffer, ref int offset, ref int length)
        {
            Buffer.BlockCopy(buffer, 0, _bitmapScratchBuffer, offset, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyFromScratchBuffer(ref byte[] buffer, ref int offset, ref int length)
        {
            Buffer.BlockCopy(_bitmapScratchBuffer, offset, buffer, 0, length);
        }

        /// <summary>
        /// Transfers scratch bitmap to a target <see cref="ILockedFramebuffer"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TransferScratchToBitmap(ILockedFramebuffer bitmap)
        {
            _bitmapMutex.WaitOne();
            if (_hasNewFrame)
            {
                Marshal.Copy(_bitmapScratchBuffer, 0, bitmap.Address, _bitmapScratchBuffer.Length);
                _hasNewFrame = false;
            }

            _bitmapMutex.ReleaseMutex();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteColor(ref byte[] lineBuffer, ref Color color, ref int startIndex)
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
            var pbl = _previousBackBuffer.Length;

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
                        CopyToScratchBuffer(ref _previousBackBuffer, ref _zero, ref pbl);

                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            var gce = currentFrame.GraphicControl;

            if (gce == null || gce.DisposalMethod != GifFrameDisposalMethod.RestorePrevious) return;

            CopyFromScratchBuffer(ref _previousBackBuffer, ref _zero, ref pbl);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearAreaBackBuffer(Int32Rect rect)
        {
            var bufferLength = 4 * rect.Width;
            var lineBuffer = ArrayPool<byte>.Shared.Rent(bufferLength);
            for (var y = 0; y < rect.Height; y++)
            {
                var offset = (rect.Y + y) * _stride + 4 * rect.X;
                CopyToScratchBuffer(ref lineBuffer, ref offset, ref bufferLength);
            }

            ArrayPool<byte>.Shared.Return(lineBuffer, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Stream GetIndexStream(GifFrame frame)
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
        private static TimeSpan GetFrameDelay(GifFrame frame)
        {
            var gce = frame.GraphicControl;
            return gce == null ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromMilliseconds(gce.Delay != 0 ? gce.Delay : 100);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IterationCount GetIterationCountFromGif(GifDataStream metadata)
        {
            return metadata.IterationCount == 0 ? IterationCount.Infinite : new IterationCount(metadata.IterationCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Int32Rect GetFixedUpFrameRect(IGifRect desc)
        {
            var width = Math.Min(desc.Width, _gifSize.Width - desc.Left);
            var height = Math.Min(desc.Height, _gifSize.Height - desc.Top);
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