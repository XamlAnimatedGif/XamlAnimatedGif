using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

using XamlAnimatedGif.Decoding;
using TaskEx = System.Threading.Tasks.Task;

namespace XamlAnimatedGif
{
    public abstract class Animator : DependencyObject, IDisposable
    {
        private readonly Stream _sourceStream;
        private readonly Uri _sourceUri;
        private readonly bool _isSourceStreamOwner;
        private readonly GifDecoder _decoder;
        internal BitmapSource Bitmap => _bitmap;

        ////private readonly GifDataStream _metadata;
        //private readonly Dictionary<int, GifPalette> _palettes;
        private readonly WriteableBitmap _bitmap;
        //private readonly int _stride;
        //private readonly byte[] _previousBackBuffer;
        //private readonly byte[] _indexStreamBuffer;
        private readonly TimingManager _timingManager;

        #region Constructor and factory methods

        internal Animator(Stream sourceStream, Uri sourceUri, RepeatBehavior repeatBehavior)
        {
            _sourceStream = sourceStream;
            _sourceUri = sourceUri;
            _isSourceStreamOwner = sourceUri != null; // stream opened from URI, should close it

            _decoder = new GifDecoder(sourceStream);

            //_metadata = metadata;
            //_palettes = CreatePalettes(metadata);
            _bitmap = CreateBitmap();
            //var desc = metadata.Header.LogicalScreenDescriptor;
            //_stride = 4;//* ((desc.Width * 32 + 31) / 32);
            //_previousBackBuffer = new byte[desc.Height * _stride];
            //_indexStreamBuffer = CreateIndexStreamBuffer(metadata, _sourceStream);

            _timingManager = CreateTimingManager(_decoder, RepeatBehavior.Forever);

        }

        internal static async Task<TAnimator> CreateAsyncCore<TAnimator>(
            Uri sourceUri,
            IProgress<int> progress,
            Func<Stream, TAnimator> create)
            where TAnimator : Animator
        {
            var loader = new UriLoader();
            var stream = await loader.GetStreamFromUriAsync(sourceUri, progress);
            try
            {
                // ReSharper disable once AccessToDisposedClosure
                return await CreateAsyncCore(stream, () => create(stream));
            }
            catch
            {
                stream?.Dispose();
                throw;
            }
        }

        internal static async Task<TAnimator> CreateAsyncCore<TAnimator>(
            Stream sourceStream,
            Func<TAnimator> create)
            where TAnimator : Animator
        {
            if (!sourceStream.CanSeek)
                throw new ArgumentException("The stream is not seekable");
            sourceStream.Seek(0, SeekOrigin.Begin);
            return create();
        }

        #endregion

        #region Animation

        public int FrameCount => _decoder.Frames.Count;

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
                    OnError(ex, AnimationErrorKind.Rendering);
            }
        }

        private int _frameIndex;
        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
               // cancellationToken.ThrowIfCancellationRequested();
                var timing = _timingManager.NextAsync(cancellationToken);
                var rendering = new Task(() => _decoder.RenderFrame(CurrentFrameIndex));
                await TaskEx.WhenAll(timing, rendering);
                TransferToTarget();
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

        public event EventHandler<AnimationCompletedEventArgs> AnimationCompleted;

        protected virtual void OnAnimationCompleted()
        {
            AnimationCompleted?.Invoke(this, new AnimationCompletedEventArgs(AnimationSource));
        }

        public event EventHandler<AnimationErrorEventArgs> Error;

        protected virtual void OnError(Exception ex, AnimationErrorKind kind)
        {
            Error?.Invoke(this, new AnimationErrorEventArgs(AnimationSource, ex, kind));
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

        private TimingManager CreateTimingManager(GifDecoder metadata, RepeatBehavior repeatBehavior)
        {
                        var actualRepeatBehavior = GetActualRepeatBehavior(metadata, repeatBehavior);

            var manager = new TimingManager(actualRepeatBehavior);
            foreach (var frame in metadata.Frames)
            {
                manager.Add(frame.FrameDelay);
            }

            manager.Completed += TimingManagerCompleted;
            return manager;
        }

        private RepeatBehavior GetActualRepeatBehavior(GifDecoder metadata, RepeatBehavior repeatBehavior)
        {
            //return repeatBehavior == default(RepeatBehavior)
            //        ? metadata.Header.Iterations
            //        : repeatBehavior;
            return repeatBehavior;
        }

        protected abstract RepeatBehavior GetSpecifiedRepeatBehavior();

        private void TimingManagerCompleted(object sender, EventArgs e)
        {
            OnAnimationCompleted();
        }

        #endregion

        #region Rendering

        void TransferToTarget()
        {
            _bitmap.Lock();
            _decoder.WriteBackBufToFb(_bitmap.BackBuffer);
            _bitmap.Unlock();
        }

        private WriteableBitmap CreateBitmap()
        {
            var desc = _decoder.Header.Rect;
            return new WriteableBitmap(desc.Width, desc.Height, 96, 96, PixelFormats.Bgra32, null);
        }

        #endregion

        #region Helper methods

        //private static TimeSpan GetFrameDelay(GifFrame frame)
        //{
        //    var gce = frame.GraphicControl;
        //    if (gce != null)
        //    {
        //        if (gce.Delay != 0)
        //            return TimeSpan.FromMilliseconds(gce.Delay);
        //    }
        //    return TimeSpan.FromMilliseconds(100);
        //}

        //private static RepeatBehavior GetRepeatBehaviorFromGif(GifDataStream metadata)
        //{
        //    if (metadata.RepeatCount == 0)
        //        return RepeatBehavior.Forever;
        //    return new RepeatBehavior(metadata.RepeatCount);
        //}

        //private Int32Rect GetFixedUpFrameRect(GifImageDescriptor desc)
        //{
        //    int width = Math.Min(desc.Dimensions.Width, _bitmap.PixelWidth - desc.Dimensions.X);
        //    int height = Math.Min(desc.Dimensions.Height, _bitmap.PixelHeight - desc.Dimensions.Y);
        //    return new Int32Rect(desc.Dimensions.X, desc.Dimensions.Y, width, height);
        //}

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
                if (_timingManager != null) _timingManager.Completed -= TimingManagerCompleted;
                _cancellationTokenSource?.Cancel();
                if (_isSourceStreamOwner)
                {
                    try
                    {
                        _decoder?.Dispose();
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

        internal async Task ShowFirstFrameAsync()
        {
            try
            {
                _decoder.RenderFrame(0);
                TransferToTarget();
                CurrentFrameIndex = 0;
                _timingManager.Pause();
            }
            catch (Exception ex)
            {
                OnError(ex, AnimationErrorKind.Rendering);
            }
        }

        public async void Rewind()
        {
            //CurrentFrameIndex = 0;
            //bool isStopped = _timingManager.IsPaused || _timingManager.IsComplete;
            //_timingManager.Reset();
            //if (isStopped)
            //{
            //    _timingManager.Pause();
            //    _isStarted = false;
            //    try
            //    {
            //        await RenderFrameAsync(0, CancellationToken.None);
            //    }
            //    catch (Exception ex)
            //    {
            //        OnError(ex, AnimationErrorKind.Rendering);
            //    }
            //}
        }

        protected abstract object AnimationSource { get; }

        internal void OnRepeatBehaviorChanged()
        {
            //if (_timingManager == null)
            //    return;

            //var newValue = GetSpecifiedRepeatBehavior();
            //var newActualValue = GetActualRepeatBehavior(_metadata, newValue);
            //if (_timingManager.RepeatBehavior == newActualValue)
            //    return;

            //_timingManager.RepeatBehavior = newActualValue;
            //Rewind();
        }
    }
}
