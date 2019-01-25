using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using XamlAnimatedGif.Decoding;

namespace XamlAnimatedGif
{
    public abstract class WpfAnimator : DependencyObject, IDisposable
    {
        private static readonly WpfUriLoader _uriLoader = new WpfUriLoader();
        private readonly Animator _core;

        internal BitmapSource Bitmap => _bitmap;

        private readonly WriteableBitmap _bitmap;
        private readonly Int32Rect _int32RectDim;


        #region Constructor and factory methods

        internal WpfAnimator(Stream sourceStream, Uri sourceUri, RepeatBehavior repeatBehavior)
        {

            _core = new Animator(sourceStream, sourceUri);

            _bitmap = CreateBitmap();

            var header = _core.Decoder.Header;
            _int32RectDim = new Int32Rect(0, 0, header.Dimensions.Width, header.Dimensions.Height);

            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (_core.Decoder != null & _core.NewFrameAvailable & _bitmap != null)
            {
                _bitmap.Lock();
                _core.Decoder.WriteBackBufToFb(_bitmap.BackBuffer);
                _bitmap.AddDirtyRect(_int32RectDim);
                _bitmap.Unlock();
            }
        }

        internal static async Task<TAnimator> CreateAsyncCore<TAnimator>(
            Uri sourceUri,
            IProgress<int> progress,
            Func<Stream, TAnimator> create)
            where TAnimator : WpfAnimator
        {
            var stream = await _uriLoader.GetStreamFromUriAsync(sourceUri, progress);
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
            where TAnimator : WpfAnimator
        {
            if (!sourceStream.CanSeek)
                throw new ArgumentException("The stream is not seekable");
            sourceStream.Seek(0, SeekOrigin.Begin);
            return create();
        }

        #endregion

        #region Animation

        public int FrameCount => _core.Decoder.Frames.Count;

        public async void Play()
        {
            _core.Play();
        }

        public void Pause()
        {
            _core.Pause();
        }

        public bool IsPaused => _core.IsPaused;

        public bool IsComplete => _core.IsComplete;

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

        public int CurrentFrameIndex => _core.CurrentFrameIndex;

        private GifRepeatBehavior GetActualRepeatBehavior(GifDecoder decoder, RepeatBehavior repeatBehavior)
        {
            return repeatBehavior == default(RepeatBehavior)
            ? decoder.Header.RepeatCount
            : ConvertGifRepeatCount(repeatBehavior);
        }

        private GifRepeatBehavior ConvertGifRepeatCount(RepeatBehavior repeatCount)
        {
            if (repeatCount == RepeatBehavior.Forever)
                return new GifRepeatBehavior() { LoopForever = true };
            else
                return new GifRepeatBehavior() { Count = (int)repeatCount.Count };
        }

        protected abstract RepeatBehavior GetSpecifiedRepeatBehavior();

        private void TimingManagerCompleted(object sender, EventArgs e)
        {
            OnAnimationCompleted();
        }
        #endregion

        #region Rendering
        private WriteableBitmap CreateBitmap()
        {
            var desc = _core.Decoder.Header.Dimensions;
            return new WriteableBitmap(desc.Width, desc.Height, 96, 96, PixelFormats.Bgra32, null);
        }

        #endregion

        #region Finalizer and Dispose

        ~WpfAnimator()
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
                _core?.Dispose();
            }
        }

        #endregion

        public override string ToString()
        {
            return _core.ToString();
        }

        internal async Task ShowFirstFrameAsync()
        {
            _core.ShowFirstFrameAsync();
        }

        public async void Rewind()
        {
            _core.Rewind();
        }

        protected abstract object AnimationSource { get; }

        internal void OnRepeatBehaviorChanged()
        {
            if (_core == null) return;

            var newValue = GetSpecifiedRepeatBehavior();
            var newActualValue = GetActualRepeatBehavior(_core.Decoder, newValue);
            if (_core.RepeatBehavior == newActualValue)
                return;

            _core.RepeatBehavior = newActualValue;

            Rewind();
        }
    }
}
