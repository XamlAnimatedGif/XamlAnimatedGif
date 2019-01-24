using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks; 
using XamlAnimatedGif.Decoding;

namespace XamlAnimatedGif
{
    public class Animator : IDisposable
    {
        private readonly Stream _sourceStream;
        private readonly Uri _sourceUri;
        private readonly bool _isSourceStreamOwner;
        private readonly GifBackgroundWorker _bgWorker;
        private readonly UriLoader _uriLoader;
        private readonly GifDecoder _decoder;

        public GifDecoder Decoder => _decoder;

        #region Constructor and factory methods

        public Animator(Stream sourceStream, Uri sourceUri)
        {
            _sourceStream = sourceStream;
            _sourceUri = sourceUri;
            _isSourceStreamOwner = sourceUri != null; // stream opened from URI, should close it

            _decoder = new GifDecoder(sourceStream);
            _bgWorker = new GifBackgroundWorker(_decoder);
            _bgWorker.RepeatCount = _decoder.Header.RepeatCount;
            _bgWorker.SendCommand(GifBackgroundWorker.Command.Start);
        }
        
        #endregion

        #region Animation

        private bool _isStarted;

        private CancellationTokenSource _cancellationTokenSource;

        public async void Play()
        {
            try
            {
                if (_bgWorker.GetState() == GifBackgroundWorker.State.Complete)
                {
                    _bgWorker.SendCommand(GifBackgroundWorker.Command.Reset);
                    _isStarted = false;
                }

                if (!_isStarted)
                {
                    _isStarted = true;
                    if (_bgWorker.GetState() == GifBackgroundWorker.State.Paused)
                        _bgWorker.SendCommand(GifBackgroundWorker.Command.Resume);
                }
                else if (_bgWorker.GetState() == GifBackgroundWorker.State.Paused)
                    _bgWorker.SendCommand(GifBackgroundWorker.Command.Resume);
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

        }

        public void Pause()
        {

        }

        public bool IsPaused => (_bgWorker.GetState() == GifBackgroundWorker.State.Paused);

        public bool IsComplete
        {
            get
            {
                if (_isStarted)
                    return (_bgWorker.GetState() == GifBackgroundWorker.State.Complete);
                return false;
            }
        }

        public event EventHandler CurrentFrameChanged;

        protected virtual void OnCurrentFrameChanged()
        {
            CurrentFrameChanged?.Invoke(this, EventArgs.Empty);
        }
        
        public event EventHandler<AnimationErrorEventArgs> Error;

        protected virtual void OnError(Exception ex, AnimationErrorKind kind)
        {
            Error?.Invoke(this, new AnimationErrorEventArgs(this, ex, kind));
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
                _bgWorker?.SendCommand(GifBackgroundWorker.Command.Stop);
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
            //try
            //{
            //    _decoder.RenderFrame(0);
            //    CurrentFrameIndex = 0;
            //    _bgWorker.SendCommand(GifBackgroundWorker.Command.Pause);
            //}
            //catch (Exception ex)
            //{
            //    OnError(ex, AnimationErrorKind.Rendering);
            //}
        }

        public async void Rewind()
        {
            CurrentFrameIndex = 0;
            var state = _bgWorker.GetState();
            bool isStopped = state == GifBackgroundWorker.State.Paused || state == GifBackgroundWorker.State.Complete;
            _bgWorker.SendCommand(GifBackgroundWorker.Command.Reset);
            if (isStopped)
            {
                _bgWorker.SendCommand(GifBackgroundWorker.Command.Pause);
                _isStarted = false;
                try
                {
                    _decoder.RenderFrame(0);
                }
                catch (Exception ex)
                {
                    OnError(ex, AnimationErrorKind.Rendering);
                }
            }
        }

        internal void OnRepeatBehaviorChanged()
        {
            //if (_timingManager == null)
            //    return;

            //var newValue = GetSpecifiedRepeatBehavior();
            //var newActualValue = GetActualRepeatBehavior(_decoder, newValue);
            //if (_timingManager.RepeatBehavior == newActualValue)
            //    return;

            //_timingManager.RepeatBehavior = newActualValue;
            //Rewind();
        }
    }
}
