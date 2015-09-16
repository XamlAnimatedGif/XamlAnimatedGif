using XamlAnimatedGif.Decoding;
using System;
using System.IO;
#if WPF
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
#elif WINRT
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
#endif

namespace XamlAnimatedGif
{
    public class GifAnimation
    {
        #region Public attached properties and events
        public Action<int> Progress { get; set; }

        #region RepeatBehavior
        private RepeatBehavior _repeatBehavior;
        public RepeatBehavior RepeatBehavior
        {
            get { return _repeatBehavior; }
            set { _repeatBehavior = value; }
        }
        #endregion

        #region Error
        public  event EventHandler<AnimationErrorEventArgs> Error;
        internal void OnError(Image image, Exception exception, AnimationErrorKind kind)
        {
#if WPF
            image.RaiseEvent(new AnimationErrorEventArgs(image, exception, kind));
#elif WINRT
            Error?.Invoke(image, new AnimationErrorEventArgs(exception, kind));
#endif
        }
        #endregion

        #region Loaded
        public event EventHandler Loaded;
        private  void OnLoaded(Image sender)
        {
#if WPF
            sender.RaiseEvent(new RoutedEventArgs(LoadedEvent, sender));
#elif WINRT
            Loaded?.Invoke(sender, EventArgs.Empty);
#endif
        }
        #endregion

        #endregion

        private Image _image;
        private Uri _sourceUri;
        private Stream _sourceStream;
        private bool _autoStart;
        private Animator _animator;

        public void InitAnimation(Image image, string url, Action<int> progress = null, bool autoStart = true)
        {
            image.Source = null;
            //ClearAnimatorCore();
            this._image = image;
            this._sourceUri = new Uri(url,UriKind.RelativeOrAbsolute);
            this._autoStart = autoStart;
            Progress = progress;

            try
            {
                var uri = GetAbsoluteUri(image);
                if (uri != null)
                {
                    InitUriAnimationAsync();
                }
            }
            catch (Exception ex)
            {
                OnError(image, ex, AnimationErrorKind.Loading);
            }
        }

        public void InitAnimation(Image image, Stream stream, bool autoStart = true)
        {
            image.Source = null;
            ClearAnimatorCore();

            this._image = image;
            this._sourceStream = stream;
            this._autoStart = autoStart;
            try
            {
                if (stream != null)
                {
                    InitStreamAnimationAsync();
                    return;
                }
            }
            catch (Exception ex)
            {
                OnError(image, ex, AnimationErrorKind.Loading);
            }
        }

        public void PlayAnimator()
        {
            if (null != this._animator)
            {
                this._animator.Play();
            }
        }

        public void PauseAnimator()
        {
            if (null != this._animator)
            {
                this._animator.Pause();
            }
        }

        private Uri GetAbsoluteUri(Image image)
        {
            var uri = _sourceUri;
            if (uri == null)
                return null;
            if (!uri.IsAbsoluteUri)
            {
#if WPF
                var baseUri = ((IUriContext)image).BaseUri;
#elif WINRT
                var baseUri = image.BaseUri;
#endif
                if (baseUri != null)
                {
                    uri = new Uri(baseUri, uri);
                }
                else
                {
                    throw new InvalidOperationException("Relative URI can't be resolved");
                }
            }
            return uri;
        }

        private async void InitUriAnimationAsync()
        {
            try
            {
                _animator = await Animator.CreateAsync(_image, _sourceUri, _repeatBehavior,Progress);
                SetAnimatorCore(_animator);
                OnLoaded(_image);
            }
            catch (InvalidSignatureException)
            {
                _image.Source = new BitmapImage(_sourceUri);
                OnLoaded(_image);
            }
            catch(Exception ex)
            {
                OnError(_image, ex, AnimationErrorKind.Loading);
            }
        }

        private async void InitStreamAnimationAsync()
        {
            try
            {
                _animator = await Animator.CreateAsync(_image, _sourceStream, _repeatBehavior);
                SetAnimatorCore(_animator);
                OnLoaded(_image);
            }
            catch (InvalidSignatureException)
            {
                var bmp = new BitmapImage();
#if WPF
                bmp.BeginInit();
                bmp.StreamSource = stream;
                bmp.EndInit();
#elif WINRT
                bmp.SetSource(_sourceStream.AsRandomAccessStream());
#endif
                _image.Source = bmp;
                OnLoaded(_image);
            }
            catch(Exception ex)
            {
                OnError(_image, ex, AnimationErrorKind.Loading);
            }
        }

        private void SetAnimatorCore(Animator animator)
        {
            _animator = animator;
            _image.Source = animator.Bitmap;
            if (_autoStart)
                animator.Play();
            else
                animator.ShowFirstFrame();
        }

        public void ClearAnimatorCore()
        {
            if (_animator == null)
                return;

            _animator.Dispose();
            _animator = null;
        }
    }
}
