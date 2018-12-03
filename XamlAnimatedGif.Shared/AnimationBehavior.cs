using XamlAnimatedGif.Decoding;
using System;
using System.IO;
using System.Threading.Tasks;
using XamlAnimatedGif.Extensions;
#if WPF || SILVERLIGHT
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
using Windows.UI.Xaml.Media;
#endif
#if SILVERLIGHT
using System.Windows.Media;
#endif

namespace XamlAnimatedGif
{
    public static class AnimationBehavior
    {
        #region Public attached properties and events

        #region SourceUri

#if WPF
        [AttachedPropertyBrowsableForType(typeof(Image))]
#endif
        public static Uri GetSourceUri(Image image)
        {
            return (Uri)image.GetValue(SourceUriProperty);
        }

        public static void SetSourceUri(Image image, Uri value)
        {
            image.SetValue(SourceUriProperty, value);
        }

        public static readonly DependencyProperty SourceUriProperty =
            DependencyProperty.RegisterAttached(
              "SourceUri",
              typeof(Uri),
              typeof(AnimationBehavior),
              new PropertyMetadata(
                null,
                SourceChanged));

        #endregion

        #region SourceStream

#if WPF
        [AttachedPropertyBrowsableForType(typeof(Image))]
#endif
        public static Stream GetSourceStream(DependencyObject obj)
        {
            return (Stream)obj.GetValue(SourceStreamProperty);
        }

        public static void SetSourceStream(DependencyObject obj, Stream value)
        {
            obj.SetValue(SourceStreamProperty, value);
        }

        public static readonly DependencyProperty SourceStreamProperty =
            DependencyProperty.RegisterAttached(
                "SourceStream",
                typeof(Stream),
                typeof(AnimationBehavior),
                new PropertyMetadata(
                    null,
                    SourceChanged));

        #endregion

        #region RepeatBehavior

#if WPF
        [AttachedPropertyBrowsableForType(typeof(Image))]
#endif
        public static RepeatBehavior GetRepeatBehavior(DependencyObject obj)
        {
            return (RepeatBehavior)obj.GetValue(RepeatBehaviorProperty);
        }

        public static void SetRepeatBehavior(DependencyObject obj, RepeatBehavior value)
        {
            obj.SetValue(RepeatBehaviorProperty, value);
        }

        public static readonly DependencyProperty RepeatBehaviorProperty =
            DependencyProperty.RegisterAttached(
              "RepeatBehavior",
              typeof(RepeatBehavior),
              typeof(AnimationBehavior),
              new PropertyMetadata(
                default(RepeatBehavior),
                RepeatBehaviorChanged));

        #endregion

        #region AutoStart

#if WPF
        [AttachedPropertyBrowsableForType(typeof(Image))]
#endif
        public static bool GetAutoStart(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoStartProperty);
        }

        public static void SetAutoStart(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoStartProperty, value);
        }

        public static readonly DependencyProperty AutoStartProperty =
            DependencyProperty.RegisterAttached(
                "AutoStart",
                typeof(bool),
                typeof(AnimationBehavior),
                new PropertyMetadata(true));

        #endregion

        #region AnimateInDesignMode


        public static bool GetAnimateInDesignMode(DependencyObject obj)
        {
            return (bool)obj.GetValue(AnimateInDesignModeProperty);
        }

        public static void SetAnimateInDesignMode(DependencyObject obj, bool value)
        {
            obj.SetValue(AnimateInDesignModeProperty, value);
        }

        public static readonly DependencyProperty AnimateInDesignModeProperty =
            DependencyProperty.RegisterAttached(
                "AnimateInDesignMode",
                typeof(bool),
                typeof(AnimationBehavior),
                new PropertyMetadata(
                    false,
                    AnimateInDesignModeChanged));

        #endregion

        #region Animator

        public static Animator GetAnimator(DependencyObject obj)
        {
            return (Animator) obj.GetValue(AnimatorProperty);
        }

        private static void SetAnimator(DependencyObject obj, Animator value)
        {
            obj.SetValue(AnimatorProperty, value);
        }

        public static readonly DependencyProperty AnimatorProperty =
            DependencyProperty.RegisterAttached(
                "Animator",
                typeof (Animator),
                typeof (AnimationBehavior),
                new PropertyMetadata(null));

        #endregion

        #region Error

#if WPF
        public static readonly RoutedEvent ErrorEvent =
            EventManager.RegisterRoutedEvent(
                "Error",
                RoutingStrategy.Bubble,
                typeof (AnimationErrorEventHandler),
                typeof (AnimationBehavior));

        public static void AddErrorHandler(DependencyObject d, AnimationErrorEventHandler handler)
        {
            (d as UIElement)?.AddHandler(ErrorEvent, handler);
        }

        public static void RemoveErrorHandler(DependencyObject d, AnimationErrorEventHandler handler)
        {
            (d as UIElement)?.RemoveHandler(ErrorEvent, handler);
        }
#elif WINRT || SILVERLIGHT
        // WinRT doesn't support custom attached events, use a normal CLR event instead
        public static event EventHandler<AnimationErrorEventArgs> Error;
#endif

        internal static void OnError(Image image, Exception exception, AnimationErrorKind kind)
        {
#if WPF
            image.RaiseEvent(new AnimationErrorEventArgs(image, exception, kind));
#elif WINRT || SILVERLIGHT
            Error?.Invoke(image, new AnimationErrorEventArgs(image, exception, kind));
#endif
        }

        private static void AnimatorError(object sender, AnimationErrorEventArgs e)
        {
#if WPF
            var source = e.Source as UIElement;
            source?.RaiseEvent(e);
#elif WINRT || SILVERLIGHT
            Error?.Invoke(e.Source, e);
#endif
        }

        #endregion

        #region DownloadProgress

#if WPF
        public static readonly RoutedEvent DownloadProgressEvent =
            EventManager.RegisterRoutedEvent(
                "DownloadProgress",
                RoutingStrategy.Bubble,
                typeof (DownloadProgressEventHandler),
                typeof (AnimationBehavior));

        public static void AddDownloadProgressHandler(DependencyObject d, DownloadProgressEventHandler handler)
        {
            (d as UIElement)?.AddHandler(DownloadProgressEvent, handler);
        }

        public static void RemoveDownloadProgressHandler(DependencyObject d, DownloadProgressEventHandler handler)
        {
            (d as UIElement)?.RemoveHandler(DownloadProgressEvent, handler);
        }

#elif WINRT || SILVERLIGHT
        // WinRT doesn't support custom attached events, use a normal CLR event instead
        public static event EventHandler<DownloadProgressEventArgs> DownloadProgress;
#endif

        internal static void OnDownloadProgress(Image image, int downloadPercentage)
        {
#if WPF
            image.RaiseEvent(new DownloadProgressEventArgs(image, downloadPercentage));
#elif WINRT || SILVERLIGHT
            DownloadProgress?.Invoke(image, new DownloadProgressEventArgs(downloadPercentage));
#endif
        }
        #endregion

        #region Loaded

#if WPF
        public static readonly RoutedEvent LoadedEvent =
            EventManager.RegisterRoutedEvent(
                "Loaded",
                RoutingStrategy.Bubble,
                typeof (RoutedEventHandler),
                typeof (AnimationBehavior));

        public static void AddLoadedHandler(DependencyObject d, RoutedEventHandler handler)
        {
            (d as UIElement)?.AddHandler(LoadedEvent, handler);
        }

        public static void RemoveLoadedHandler(DependencyObject d, RoutedEventHandler handler)
        {
            (d as UIElement)?.RemoveHandler(LoadedEvent, handler);
        }
#elif WINRT || SILVERLIGHT
        // WinRT doesn't support custom attached events, use a normal CLR event instead
        public static event EventHandler Loaded;
#endif

        private static void OnLoaded(Image sender)
        {
#if WPF
            sender.RaiseEvent(new RoutedEventArgs(LoadedEvent, sender));
#elif WINRT || SILVERLIGHT
            Loaded?.Invoke(sender, EventArgs.Empty);
#endif
        }

        #endregion

        #region AnimationCompleted

#if WPF
        public static readonly RoutedEvent AnimationCompletedEvent =
            EventManager.RegisterRoutedEvent(
                "AnimationCompleted",
                RoutingStrategy.Bubble,
                typeof(AnimationCompletedEventHandler),
                typeof(AnimationBehavior));

        public static void AddAnimationCompletedHandler(DependencyObject d, AnimationCompletedEventHandler handler)
        {
            (d as UIElement)?.AddHandler(AnimationCompletedEvent, handler);
        }

        public static void RemoveAnimationCompletedHandler(DependencyObject d, AnimationCompletedEventHandler handler)
        {
            (d as UIElement)?.RemoveHandler(AnimationCompletedEvent, handler);
        }
#elif WINRT || SILVERLIGHT
        // WinRT doesn't support custom attached events, use a normal CLR event instead
        public static event EventHandler<AnimationCompletedEventArgs> AnimationCompleted;
#endif

        private static void AnimatorAnimationCompleted(object sender, AnimationCompletedEventArgs e)
        {
#if WPF
            var element = e.Source as Image;
            element?.RaiseEvent(e);
#elif WINRT || SILVERLIGHT
            AnimationCompleted?.Invoke(e.Source, e);
#endif
        }

        #endregion

        #endregion

        #region Private attached properties

        private static int GetSeqNum(DependencyObject obj)
        {
            return (int)obj.GetValue(SeqNumProperty);
        }

        private static void SetSeqNum(DependencyObject obj, int value)
        {
            obj.SetValue(SeqNumProperty, value);
        }

        private static readonly DependencyProperty SeqNumProperty =
            DependencyProperty.RegisterAttached("SeqNum", typeof(int), typeof(AnimationBehavior), new PropertyMetadata(0));

        #endregion

        private static void SourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var image = o as Image;
            if (image == null)
                return;

            InitAnimation(image);
        }

        private static void RepeatBehaviorChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            GetAnimator(o)?.OnRepeatBehaviorChanged();
        }

        private static void AnimateInDesignModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var image = d as Image;
            if (image == null)
                return;

            InitAnimation(image);
        }

        private static bool CheckDesignMode(Image image, Uri sourceUri, Stream sourceStream)
        {
            if (IsInDesignMode(image) && !GetAnimateInDesignMode(image))
            {
                try
                {
                    if (sourceStream != null)
                    {
                        SetStaticImage(image, sourceStream);
                    }
                    else if (sourceUri != null)
                    {
                        var bmp = new BitmapImage();
                        bmp.UriSource = sourceUri;
                        image.Source = bmp;
                    }
                }
                catch
                {
                    image.Source = null;
                }
                return false;
            }
            return true;
        }

        private static void InitAnimation(Image image)
        {
            if (IsLoaded(image))
            {
                image.Unloaded += Image_Unloaded;
            }
            else
            {
                image.Loaded += Image_Loaded;
                return;
            }

            int seqNum = GetSeqNum(image) + 1;
            SetSeqNum(image, seqNum);

            image.Source = null;
            ClearAnimatorCore(image);

            try
            {
                var stream = GetSourceStream(image);
                if (stream != null)
                {
                    InitAnimationAsync(image, stream.AsBuffered(), GetRepeatBehavior(image), seqNum);
                    return;
                }

                var uri = GetAbsoluteUri(image);
                if (uri != null)
                {
                    InitAnimationAsync(image, uri, GetRepeatBehavior(image), seqNum);
                }
            }
            catch (Exception ex)
            {
                OnError(image, ex, AnimationErrorKind.Loading);
            }
        }

        private static void Image_Loaded(object sender, RoutedEventArgs e)
        {
            var image = (Image) sender;
            image.Loaded -= Image_Loaded;
            InitAnimation(image);
        }

        private static void Image_Unloaded(object sender, RoutedEventArgs e)
        {
            var image = (Image) sender;
            image.Unloaded -= Image_Unloaded;
            image.Loaded += Image_Loaded;
            ClearAnimatorCore(image);
        }

        private static bool IsLoaded(FrameworkElement element)
        {
#if WPF
            return element.IsLoaded;
#elif WINRT || SILVERLIGHT
            return VisualTreeHelper.GetParent(element) != null;
#endif
        }

        private static Uri GetAbsoluteUri(Image image)
        {
            var uri = GetSourceUri(image);
            if (uri == null)
                return null;
#if !SILVERLIGHT
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
#endif
            return uri;
        }

        private static async void InitAnimationAsync(Image image, Uri sourceUri, RepeatBehavior repeatBehavior, int seqNum)
        {
            if (!CheckDesignMode(image, sourceUri, null))
                return;

            try
            {
                var progress = new Progress<int>(percentage => OnDownloadProgress(image, percentage));
                var animator = await ImageAnimator.CreateAsync(sourceUri, repeatBehavior, progress, image);
                // Check that the source hasn't changed while we were loading the animation
                if (GetSeqNum(image) != seqNum)
                {
                    animator.Dispose();
                    return;
                }
                await SetAnimatorCoreAsync(image, animator);
                OnLoaded(image);
            }
            catch (InvalidSignatureException)
            {
                await SetStaticImageAsync(image, sourceUri);
                OnLoaded(image);
            }
            catch(Exception ex)
            {
                OnError(image, ex, AnimationErrorKind.Loading);
            }
        }

        private static async void InitAnimationAsync(Image image, Stream stream, RepeatBehavior repeatBehavior, int seqNum)
        {
            if (!CheckDesignMode(image, null, stream))
                return;

            try
            {
                var animator = await ImageAnimator.CreateAsync(stream, repeatBehavior, image);
                await SetAnimatorCoreAsync(image, animator);
                // Check that the source hasn't changed while we were loading the animation
                if (GetSeqNum(image) != seqNum)
                {
                    animator.Dispose();
                    return;
                }
                OnLoaded(image);
            }
            catch (InvalidSignatureException)
            {
                SetStaticImage(image, stream);
                OnLoaded(image);
            }
            catch(Exception ex)
            {
                OnError(image, ex, AnimationErrorKind.Loading);
            }
        }

        private static async Task SetAnimatorCoreAsync(Image image, Animator animator)
        {
            SetAnimator(image, animator);
            animator.Error += AnimatorError;
            animator.AnimationCompleted += AnimatorAnimationCompleted;
            image.Source = animator.Bitmap;
            if (GetAutoStart(image))
                animator.Play();
            else
                await animator.ShowFirstFrameAsync();
        }

        private static void ClearAnimatorCore(Image image)
        {
            var animator = GetAnimator(image);
            if (animator == null)
                return;

            animator.AnimationCompleted -= AnimatorAnimationCompleted;
            animator.Error -= AnimatorError;
            animator.Dispose();
            SetAnimator(image, null);
        }

        // ReSharper disable once UnusedParameter.Local (used in WPF)
        private static bool IsInDesignMode(DependencyObject obj)
        {
#if WPF
            return DesignerProperties.GetIsInDesignMode(obj);
#elif WINRT
            return DesignMode.DesignModeEnabled;
#elif SILVERLIGHT
            return DesignerProperties.IsInDesignTool;
#endif
        }

        private static async Task SetStaticImageAsync(Image image, Uri sourceUri)
        {
            try
            {
                var loader = new UriLoader();
                var progress = new Progress<int>(percentage => OnDownloadProgress(image, percentage));
                var stream = await loader.GetStreamFromUriAsync(sourceUri, progress);
                SetStaticImageCore(image, stream);
            }
            catch (Exception ex)
            {
                OnError(image, ex, AnimationErrorKind.Loading);
            }
        }

        private static void SetStaticImage(Image image, Stream stream)
        {
            try
            {
                SetStaticImageCore(image, stream);
            }
            catch (Exception ex)
            {
                OnError(image, ex, AnimationErrorKind.Loading);
            }
        }

        private static void SetStaticImageCore(Image image, Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var bmp = new BitmapImage();
#if WPF
            bmp.BeginInit();
            bmp.StreamSource = stream;
            bmp.EndInit();
#elif WINRT
            bmp.SetSource(stream.AsRandomAccessStream());
#elif SILVERLIGHT
            bmp.SetSource(stream);
#endif
            image.Source = bmp;
        }
    }
}
