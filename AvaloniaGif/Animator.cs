using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskEx = System.Threading.Tasks.Task;
using AvaloniaGif.Decoding;
using AvaloniaGif.Decompression;
using AvaloniaGif.Extensions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Runtime.InteropServices;
using Avalonia.Animation;
using Avalonia;
using Avalonia.Platform;

namespace AvaloniaGif
{
    public abstract class Animator : AvaloniaObject, IDisposable
    {
        private readonly Stream _sourceStream;
        private readonly Uri _sourceUri;
        private readonly bool _isSourceStreamOwner;
        private readonly GifDataStream _metadata;
        private readonly Dictionary<int, GifPalette> _palettes;
        private readonly WriteableBitmap _bitmap;
        private readonly int _stride;
        private readonly byte[] _previousBackBuffer;
        private readonly byte[] _indexStreamBuffer;
        //private readonly TimingManager _timingManager;

        #region Constructor and factory methods

        internal Animator(Stream sourceStream, Uri sourceUri, GifDataStream metadata, RepeatCount RepeatCount)
        {
            _sourceStream = sourceStream;
            _sourceUri = sourceUri;
            _isSourceStreamOwner = sourceUri != null; // stream opened from URI, should close it
            _metadata = metadata;
            _palettes = CreatePalettes(metadata);
            _bitmap = CreateBitmap(metadata);
            var desc = metadata.Header.LogicalScreenDescriptor;
            _stride = 4 * ((desc.Width * 32 + 31) / 32);
            _previousBackBuffer = new byte[desc.Height * _stride];
            _indexStreamBuffer = CreateIndexStreamBuffer(metadata, _sourceStream);
            InitialFrames(metadata, RepeatCount);
            //  _timingManager = CreateTimingManager(metadata, RepeatCount);
        }

        List<TimeSpan> _gifFrames = new List<TimeSpan>();


        private void InitialFrames(GifDataStream metadata, RepeatCount repeatCount)
        {
            foreach (var frame in metadata.Frames)
            {
                _gifFrames.Add(GetFrameDelay(frame));
            }
        }

        public async void Play(CancellationToken cancellationToken)
        {
            Task.Run(() =>
            {
                while (true)
                {
                    // var timing = _timingManager.NextAsync(cancellationToken);
                    var k = _gifFrames[CurrentFrameIndex];
                    Thread.Sleep(k);
                    var rendering = RenderFrameAsync(CurrentFrameIndex, cancellationToken);
                    // await TaskEx.WhenAll(timing, rendering);
                    // if (!timing.Result)
                    //     break;
                    CurrentFrameIndex = (CurrentFrameIndex + 1) % FrameCount;
                }
            });
        }

        internal static async Task<TAnimator> CreateAsyncCore<TAnimator>(
            Uri sourceUri,
            IProgress<int> progress,
            Func<Stream, GifDataStream, TAnimator> create)
            where TAnimator : Animator
        {
            var loader = new UriLoader();
            var stream = await loader.GetStreamFromUriAsync(sourceUri, progress);
            try
            {
                // ReSharper disable once AccessToDisposedClosure
                return await CreateAsyncCore(stream, metadata => create(stream, metadata));
            }
            catch
            {
                stream?.Dispose();
                throw;
            }
        }

        internal static async Task<TAnimator> CreateAsyncCore<TAnimator>(
            Stream sourceStream,
            Func<GifDataStream, TAnimator> create)
            where TAnimator : Animator
        {
            if (!sourceStream.CanSeek)
                throw new ArgumentException("The stream is not seekable");
            sourceStream.Seek(0, SeekOrigin.Begin);
            var metadata = await GifDataStream.ReadAsync(sourceStream);
            return create(metadata);
        }

        #endregion

        #region Animation

        public int FrameCount => _metadata.Frames.Count;

        private bool _isStarted;
        private CancellationTokenSource _cancellationTokenSource;


        // try
        // {
        //     if (_timingManager.IsComplete)
        //     {
        //         _timingManager.Reset();
        //         _isStarted = false;
        //     }

        //     if (!_isStarted)
        //     {
        //         _cancellationTokenSource?.Dispose();
        //         _cancellationTokenSource = new CancellationTokenSource();
        //         _isStarted = true;
        //         if (_timingManager.IsPaused)
        //             _timingManager.Resume();
        //         await RunAsync(_cancellationTokenSource.Token);
        //     }
        //     else if (_timingManager.IsPaused)
        //     {
        //         _timingManager.Resume();
        //     }
        // }
        // catch (OperationCanceledException)
        // {
        // }
        // catch (Exception ex)
        // {
        //     // ignore errors that might occur during Dispose
        //     if (!_disposing)
        //         OnError(ex, AnimationErrorKind.Rendering);
        // }
        //}

        private int _frameIndex;

        public void Pause()
        {
            //  _timingManager.Pause();
        }

        // public bool IsPaused => _timingManager.IsPaused;

        // public bool IsComplete
        // {
        //     get
        //     {
        //         if (_isStarted)
        //             return _timingManager.IsComplete;
        //         return false;
        //     }
        // }

        public event EventHandler CurrentFrameChanged;

        protected virtual void OnCurrentFrameChanged()
        {
            CurrentFrameChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler AnimationCompleted;

        protected virtual void OnAnimationCompleted()
        {
            AnimationCompleted?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler<AnimationErrorEventArgs> Error;

        protected virtual void OnError(Exception ex, AnimationErrorKind kind)
        {
            Error?.Invoke(this, new AnimationErrorEventArgs(null, ex, kind));
        }

        public int CurrentFrameIndex
        {
            get { return _frameIndex; }
            internal set
            {
                _frameIndex = value;
                OnCurrentFrameChanged();
            }
        }

        // private TimingManager CreateTimingManager(GifDataStream metadata, RepeatCount RepeatCount)
        // {
        //     var actualRepeatCount = GetActualRepeatCount(metadata, RepeatCount);

        //     var manager = new TimingManager(actualRepeatCount);

        //     foreach (var frame in metadata.Frames)
        //     {
        //         manager.Add(GetFrameDelay(frame));
        //     }

        //     manager.Completed += TimingManagerCompleted;
        //     return manager;
        // }

        private RepeatCount GetActualRepeatCount(GifDataStream metadata, RepeatCount RepeatCount)
        {
            return RepeatCount == default(RepeatCount)
                    ? GetRepeatCountFromGif(metadata)
                    : RepeatCount;
        }

        protected abstract RepeatCount GetSpecifiedRepeatCount();

        private void TimingManagerCompleted(object sender, EventArgs e)
        {
            OnAnimationCompleted();
        }

        #endregion

        #region Rendering

        private static WriteableBitmap CreateBitmap(GifDataStream metadata)
        {
            var desc = metadata.Header.LogicalScreenDescriptor;
            var bitmap = new WriteableBitmap(desc.Width, desc.Height, PixelFormat.Bgra8888);
            return bitmap;
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

        private int _previousFrameIndex;
        private GifFrame _previousFrame;

        private async Task RenderFrameAsync(int frameIndex, CancellationToken cancellationToken)
        {
            if (frameIndex < 0)
                return;

            var frame = _metadata.Frames[frameIndex];
            var desc = frame.Descriptor;
            var rect = GetFixedUpFrameRect(desc);
            
            using (var indexStream = await GetIndexStreamAsync(frame, cancellationToken))
            {
                
                if (frameIndex < _previousFrameIndex)
                    ClearArea(new Int32Rect(0, 0, _metadata.Header.LogicalScreenDescriptor.Width, _metadata.Header.LogicalScreenDescriptor.Height));
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
                        CopyFromBitmap(lineBuffer, _bitmap, offset, bufferLength);
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
                    CopyToBitmap(lineBuffer, _bitmap, offset, bufferLength);

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

        private static void CopyToBitmap(byte[] buffer, WriteableBitmap bitmap, int offset, int length)
        {
            using (var l = bitmap.Lock())
            {
                Marshal.Copy(buffer, 0, l.Address + offset, length);
            }
        }

        private static void CopyFromBitmap(byte[] buffer, WriteableBitmap bitmap, int offset, int length)
        {
            using (var l = bitmap.Lock())
            {
                Marshal.Copy(l.Address + offset, buffer, 0, length);
            }
        }

        private static void WriteColor(byte[] lineBuffer, Color color, int startIndex)
        {
            lineBuffer[startIndex] = color.B;
            lineBuffer[startIndex + 1] = color.G;
            lineBuffer[startIndex + 2] = color.R;
            lineBuffer[startIndex + 3] = color.A;
        }

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
                            ClearArea(GetFixedUpFrameRect(_previousFrame.Descriptor));
                            break;
                        }
                    case GifFrameDisposalMethod.RestorePrevious:
                        {
                            CopyToBitmap(_previousBackBuffer, _bitmap, 0, _previousBackBuffer.Length);

                            break;
                        }
                }
            }

            var gce = currentFrame.GraphicControl;
            if (gce != null && gce.DisposalMethod == GifFrameDisposalMethod.RestorePrevious)
            {
                CopyFromBitmap(_previousBackBuffer, _bitmap, 0, _previousBackBuffer.Length);
            }
        }

        private void ClearArea(Int32Rect rect)
        {
            int bufferLength = 4 * rect.Width;
            byte[] lineBuffer = new byte[bufferLength];
            for (int y = 0; y < rect.Height; y++)
            {
                int offset = (rect.Y + y) * _stride + 4 * rect.X;
                CopyToBitmap(lineBuffer, _bitmap, offset, bufferLength);
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

        internal Bitmap Bitmap => _bitmap;

        #endregion

        #region Helper methods

        private static TimeSpan GetFrameDelay(GifFrame frame)
        {
            var gce = frame.GraphicControl;
            if (gce != null)
            {
                if (gce.Delay != 0)
                    return TimeSpan.FromMilliseconds(gce.Delay);
            }
            return TimeSpan.FromMilliseconds(100);
        }

        private static RepeatCount GetRepeatCountFromGif(GifDataStream metadata)
        {
            if (metadata.RepeatCount == 0)
                return RepeatCount.Loop;

            return new RepeatCount(metadata.RepeatCount);
        }

        private Int32Rect GetFixedUpFrameRect(GifImageDescriptor desc)
        {
            int width = Math.Min(desc.Width, _bitmap.PixelWidth - desc.Left);
            int height = Math.Min(desc.Height, _bitmap.PixelHeight - desc.Top);
            return new Int32Rect(desc.Left, desc.Top, width, height);
        }

        #endregion

        #region Finalizer and Dispose

        ~Animator()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private volatile bool _disposing;
        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposing = true;
                // if (_timingManager != null) _timingManager.Completed -= TimingManagerCompleted;
                // _cancellationTokenSource?.Cancel();
                if (_isSourceStreamOwner)
                {
                    try
                    {
                        _sourceStream?.Dispose();
                    }
                    catch
                    {
                        /* ignored */
                    }
                }
                _disposed = true;
            }
        }

        #endregion

        public override string ToString()
        {
            string s = _sourceUri?.ToString() ?? _sourceStream.ToString();
            return "GIF: " + s;
        }

        class GifPalette
        {
            private readonly Color[] _colors;

            public GifPalette(int? transparencyIndex, Color[] colors)
            {
                TransparencyIndex = transparencyIndex;
                _colors = colors;
            }

            public int? TransparencyIndex { get; }

            public Color this[int i] => _colors[i];
        }

        internal async Task ShowFirstFrameAsync()
        {
            try
            {
                await RenderFrameAsync(0, CancellationToken.None);
                CurrentFrameIndex = 0;
                // _timingManager.Pause();
            }
            catch (Exception ex)
            {
                OnError(ex, AnimationErrorKind.Rendering);
            }
        }

        public async void Rewind()
        {
            // CurrentFrameIndex = 0;
            // bool isStopped = _timingManager.IsPaused || _timingManager.IsComplete;
            // _timingManager.Reset();
            // if (isStopped)
            // {
            //     _timingManager.Pause();
            //     _isStarted = false;
            //     try
            //     {
            //         await RenderFrameAsync(0, CancellationToken.None);
            //     }
            //     catch (Exception ex)
            //     {
            //         OnError(ex, AnimationErrorKind.Rendering);
            //     }
            // }
        }

        protected abstract object ErrorSource { get; }

        internal void OnRepeatCountChanged()
        {
            // if (_timingManager == null)
            //     return;

            // var newValue = GetSpecifiedRepeatCount();
            // var newActualValue = GetActualRepeatCount(_metadata, newValue);
            // if (_timingManager.RepeatCount == newActualValue)
            //     return;

            // _timingManager.RepeatCount = newActualValue;
            // Rewind();
        }
    }


    struct Int32Rect
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }

        public Int32Rect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}