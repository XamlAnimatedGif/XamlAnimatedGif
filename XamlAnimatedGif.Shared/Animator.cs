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
        public bool NewFrameAvailable => _decoder._hasNewFrame;

        #region Constructor and factory methods

        public Animator(Stream sourceStream, Uri sourceUri)
        {
            _sourceStream = sourceStream;
            _sourceUri = sourceUri;
            _isSourceStreamOwner = sourceUri != null; // stream opened from URI, should close it

            _decoder = new GifDecoder(sourceStream);
            _bgWorker = new GifBackgroundWorker(_decoder);
            _bgWorker.RepeatCount = _decoder.Header.RepeatCount;
            _bgWorker.SendCommand(GifBackgroundWorker.Command.Play);
        }
        
        #endregion

        #region Animation

        private bool _isStarted;

        private CancellationTokenSource _cancellationTokenSource;

        public async void Play()
        {
            _bgWorker.SendCommand(GifBackgroundWorker.Command.Play);
        }
        

        public void Pause()
        {
            _bgWorker.SendCommand(GifBackgroundWorker.Command.Pause);
        }

        public bool IsPaused => (_bgWorker.GetState() == GifBackgroundWorker.State.Paused);

        public bool IsComplete
        {
            get
            {
               return (_bgWorker.GetState() == GifBackgroundWorker.State.Complete);
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
            get { return 0; }
            internal set
            {
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
                _bgWorker?.SendCommand(GifBackgroundWorker.Command.Dispose);
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

        public void ShowFirstFrameAsync()
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
            _decoder.RenderFrame(0);
        }

        public async void Rewind()
        {
            //CurrentFrameIndex = 0;
            //var state = _bgWorker.GetState();
            //bool isStopped = state == GifBackgroundWorker.State.Paused || state == GifBackgroundWorker.State.Complete;
            //_bgWorker.SendCommand(GifBackgroundWorker.Command.Reset);
            //if (isStopped)
            //{
            //    _bgWorker.SendCommand(GifBackgroundWorker.Command.Pause);
            //    _isStarted = false;
            //    try
            //    {
            //        _decoder.RenderFrame(0);
            //    }
            //    catch (Exception ex)
            //    {
            //        OnError(ex, AnimationErrorKind.Rendering);
            //    }
            //}
        }

        public GifRepeatBehavior RepeatBehavior
        {
            get => _bgWorker.RepeatCount;
            set => _bgWorker.RepeatCount = value;
        }
    }
}
