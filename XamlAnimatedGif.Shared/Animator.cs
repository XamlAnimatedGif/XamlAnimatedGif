﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#if WPF || SILVERLIGHT
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
#elif WINRT
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Media.Animation;
using System.Runtime.InteropServices.WindowsRuntime;
#endif
#if !NET40 && !SILVERLIGHT
using TaskEx = System.Threading.Tasks.Task;
#endif

using XamlAnimatedGif.Extensions;
using XamlAnimatedGif.Decoding;
using XamlAnimatedGif.Decompression;

namespace XamlAnimatedGif
{
#if WINRT
    [Bindable]
#endif
    public class Animator : DependencyObject, IDisposable
    {
        private readonly Stream _sourceStream;
        private readonly Uri _sourceUri;
        private readonly GifDataStream _metadata;
        private readonly Image _image;
        private readonly Dictionary<int, GifPalette> _palettes;
        private readonly WriteableBitmap _bitmap;
        private readonly int _stride;
        private readonly byte[] _previousBackBuffer;
        private readonly byte[] _indexStreamBuffer;
        private readonly TimingManager _timingManager;
        

        #region Constructor and factory methods

        private Animator(Stream sourceStream, Uri sourceUri, GifDataStream metadata, RepeatBehavior repeatBehavior, Image image)
        {
            _sourceStream = sourceStream;
            _sourceUri = sourceUri;
            _metadata = metadata;
            _image = image;
            _palettes = CreatePalettes(metadata);
            _bitmap = CreateBitmap(metadata);
            var desc = metadata.Header.LogicalScreenDescriptor;
            _stride = 4 * ((desc.Width * 32 + 31) / 32);
            _previousBackBuffer = new byte[desc.Height * _stride];
            _indexStreamBuffer = CreateIndexStreamBuffer(metadata, _sourceStream);
            _timingManager = CreateTimingManager(metadata, repeatBehavior);
        }

        internal static async Task<Animator> CreateAsync(Image image, Uri sourceUri, RepeatBehavior repeatBehavior = default(RepeatBehavior))
        {
            var loader = new UriLoader();
            var progress = new Progress<int>(percentage =>
            {
                AnimationBehavior.OnDownloadProgress(image, percentage);
            });
            var stream = await loader.GetStreamFromUriAsync(sourceUri, progress);
            try
            {
                return await CreateAsync(stream, sourceUri, repeatBehavior, image);
            }
            catch
            {
                stream?.Dispose();
                throw;
            }
        }

        internal static Task<Animator> CreateAsync(Image image, Stream sourceStream, RepeatBehavior repeatBehavior = default(RepeatBehavior))
        {
            return CreateAsync(sourceStream, null, repeatBehavior, image);
        }

        private static async Task<Animator> CreateAsync(Stream sourceStream, Uri sourceUri, RepeatBehavior repeatBehavior, Image image)
        {
            var stream = sourceStream.AsBuffered();
            var metadata = await GifDataStream.ReadAsync(stream);
            return new Animator(stream, sourceUri, metadata, repeatBehavior, image);
        }

        #endregion

        #region Animation

        public int FrameCount => _metadata.Frames.Count;

        private bool _isStarted;
        private CancellationTokenSource _cancellationTokenSource;

        public async void Play()
        {
            try
            {
                if (_timingManager.IsComplete)
                {
                    _timingManager.Reset();
                    _isStarted = false;
                }

                if (!_isStarted)
                {
                    _cancellationTokenSource?.Dispose();
                    _cancellationTokenSource = new CancellationTokenSource();
                    _isStarted = true;
                    if (_timingManager.IsPaused)
                        _timingManager.Resume();
                    await RunAsync(_cancellationTokenSource.Token);
                }
                else if (_timingManager.IsPaused)
                {
                    _timingManager.Resume();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                // ignore errors that might occur during Dispose
                if (!_disposing)
                    AnimationBehavior.OnError(_image, ex, AnimationErrorKind.Rendering);
            }
        }

        private int _frameIndex;
        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var timing = _timingManager.NextAsync(cancellationToken);
                var rendering = RenderFrameAsync(CurrentFrameIndex, cancellationToken);
                await TaskEx.WhenAll(timing, rendering);
                if (!timing.Result)
                    break;
                CurrentFrameIndex = (CurrentFrameIndex + 1) % FrameCount;
            }
        }

        public void Pause()
        {
            _timingManager.Pause();
        }

        public bool IsPaused => _timingManager.IsPaused;

        public bool IsComplete
        {
            get
            {
                if (_isStarted)
                    return _timingManager.IsComplete;
                return false;
            }
        }

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

        public int CurrentFrameIndex
        {
            get { return _frameIndex; }
            internal set
            {
                _frameIndex = value;
                OnCurrentFrameChanged();
            }
        }

        private TimingManager CreateTimingManager(GifDataStream metadata, RepeatBehavior repeatBehavior)
        {
            var actualRepeatBehavior =
                repeatBehavior == default(RepeatBehavior)
                    ? GetRepeatBehavior(metadata)
                    : repeatBehavior;

            var manager = new TimingManager(actualRepeatBehavior);
            foreach (var frame in metadata.Frames)
            {
                manager.Add(GetFrameDelay(frame));
            }

            manager.Completed += TimingManagerCompleted;
            return manager;
        }

        private void TimingManagerCompleted(object sender, EventArgs e)
        {
            OnAnimationCompleted();
        }

        #endregion

        #region Rendering

        private static WriteableBitmap CreateBitmap(GifDataStream metadata)
        {
            var desc = metadata.Header.LogicalScreenDescriptor;
#if WPF
            var bitmap = new WriteableBitmap(desc.Width, desc.Height, 96, 96, PixelFormats.Bgra32, null);
#elif WINRT || SILVERLIGHT
            var bitmap = new WriteableBitmap(desc.Width, desc.Height);
#else
            #error Not implemented
#endif
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
            using (var indexStream = await GetIndexStreamAsync(frame, cancellationToken))
            {
#if WPF
                _bitmap.Lock();
                try
                {
#endif
                    if (frameIndex < _previousFrameIndex)
                        ClearArea(_metadata.Header.LogicalScreenDescriptor);
                    else
                        DisposePreviousFrame(frame);

                    int bufferLength = 4 * desc.Width;
                    byte[] indexBuffer = new byte[desc.Width];
                    byte[] lineBuffer = new byte[bufferLength];

                    var palette = _palettes[frameIndex];
                    int transparencyIndex = palette.TransparencyIndex ?? -1;

                    var rows = frame.Descriptor.Interlace
                        ? InterlacedRows(frame.Descriptor.Height)
                        : NormalRows(frame.Descriptor.Height);

                    foreach (int y in rows)
                    {
                        int read = indexStream.Read(indexBuffer, 0, desc.Width);
                        if (read != desc.Width)
                            throw new EndOfStreamException();

                        int offset = (desc.Top + y) * _stride + desc.Left * 4;

                        if (transparencyIndex >= 0)
                        {
                            CopyFromBitmap(lineBuffer, _bitmap, offset, bufferLength);
                        }

                        for (int x = 0; x < desc.Width; x++)
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
#if WPF
                    var rect = new Int32Rect(desc.Left, desc.Top, desc.Width, desc.Height);
                    _bitmap.AddDirtyRect(rect);
                }
                finally
                {
                    _bitmap.Unlock();
                }
#elif WINRT || SILVERLIGHT
                _bitmap.Invalidate();
#endif
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
#if WPF
            Marshal.Copy(buffer, 0, bitmap.BackBuffer + offset, length);
#elif WINRT
            buffer.CopyTo(0, bitmap.PixelBuffer, (uint)offset, length);
#elif SILVERLIGHT
            Buffer.BlockCopy(buffer, 0, bitmap.Pixels, offset, length);
#else
            #error Not implemented
#endif
        }

        private static void CopyFromBitmap(byte[] buffer, WriteableBitmap bitmap, int offset, int length)
        {
#if WPF
            Marshal.Copy(bitmap.BackBuffer + offset, buffer, 0, length);
#elif WINRT
            bitmap.PixelBuffer.CopyTo((uint)offset, buffer, 0, length);
#elif SILVERLIGHT
            Buffer.BlockCopy(bitmap.Pixels, offset, buffer, 0, length);
#else
            #error Not implemented
#endif
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
                        ClearArea(_previousFrame.Descriptor);
                        break;
                    }
                    case GifFrameDisposalMethod.RestorePrevious:
                    {
                        CopyToBitmap(_previousBackBuffer, _bitmap, 0, _previousBackBuffer.Length);
#if WPF
                        var desc = _metadata.Header.LogicalScreenDescriptor;
                        var rect = new Int32Rect(0, 0, desc.Width, desc.Height);
                        _bitmap.AddDirtyRect(rect);
#endif
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

        private void ClearArea(IGifRect rect)
        {
            int bufferLength = 4 * rect.Width;
            byte[] lineBuffer = new byte[bufferLength];
            for (int y = 0; y < rect.Height; y++)
            {
                int offset = (rect.Top + y) * _stride + 4 * rect.Left;
                CopyToBitmap(lineBuffer, _bitmap, offset, bufferLength);
            }
#if WPF
            _bitmap.AddDirtyRect(new Int32Rect(rect.Left, rect.Top, rect.Width, rect.Height));
#endif
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

        internal BitmapSource Bitmap => _bitmap;

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

        private static RepeatBehavior GetRepeatBehavior(GifDataStream metadata)
        {
            if (metadata.RepeatCount == 0)
                return RepeatBehavior.Forever;
            return new RepeatBehavior(metadata.RepeatCount);
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
                _timingManager.Completed -= TimingManagerCompleted;
                _cancellationTokenSource?.Cancel();
                try { _sourceStream?.Dispose(); } catch { /* ignored */ }
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
                _timingManager.Pause();
            }
            catch (Exception ex)
            {
                AnimationBehavior.OnError(_image, ex, AnimationErrorKind.Rendering);
            }
        }

        public async void Rewind()
        {
            CurrentFrameIndex = 0;
            bool isStopped = _timingManager.IsPaused || _timingManager.IsComplete;
            _timingManager.Reset();
            if (isStopped)
            {
                _timingManager.Pause();
                _isStarted = false;
                try
                {
                    await RenderFrameAsync(0, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    AnimationBehavior.OnError(_image, ex, AnimationErrorKind.Rendering);
                }
            }
        }
    }
}
