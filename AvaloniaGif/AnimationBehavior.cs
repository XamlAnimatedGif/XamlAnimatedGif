using AvaloniaGif.Decoding;
using System;
using System.IO;
using System.Threading.Tasks;
using AvaloniaGif.Extensions;
using Avalonia;
using Avalonia.Media.Imaging;

using Avalonia.Animation;
using Avalonia.Controls;

namespace AvaloniaGif
{
    public class AnimationBehavior
    {

        static AnimationBehavior()
        {
            SourceUriProperty.Changed.Subscribe(SourceChanged);
        }
        #region Public attached properties and events

        #region SourceUri
        public static readonly AttachedProperty<Uri> SourceUriProperty =
                    AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, Uri>("SourceUri");


        public static Uri GetSourceUri(Image target)
        {
            return target.GetValue(SourceUriProperty);
        }

        public static void SetSourceUri(Image target, Uri value)
        {
            target.SetValue(SourceUriProperty, value);
        }
        /*
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

                public static readonly AvaloniaProperty SourceUriProperty =
                    AvaloniaProperty.RegisterAttached(
                      "SourceUri",
                      typeof(Uri),
                      typeof(AnimationBehavior),
                      new PropertyMetadata(
                        null,
                        SourceChanged));
        */
        #endregion

        #region SourceStream

#if WPF
        [AttachedPropertyBrowsableForType(typeof(Image))]
#endif
        public static Stream GetSourceStream(AvaloniaObject obj)
        {
            return (Stream)obj.GetValue(SourceStreamProperty);
        }

        public static void SetSourceStream(AvaloniaObject obj, Stream value)
        {
            obj.SetValue(SourceStreamProperty, value);
        }

        public static readonly AvaloniaProperty SourceStreamProperty =
            AvaloniaProperty.RegisterAttached(
                "SourceStream",
                typeof(Stream),
                typeof(AnimationBehavior),
                new PropertyMetadata(
                    null,
                    SourceChanged));

        #endregion

        #region RepeatCount

#if WPF
        [AttachedPropertyBrowsableForType(typeof(Image))]
#endif
        public static RepeatCount GetRepeatCount(AvaloniaObject obj)
        {
            return (RepeatCount)obj.GetValue(RepeatCountProperty);
        }

        public static void SetRepeatCount(AvaloniaObject obj, RepeatCount value)
        {
            obj.SetValue(RepeatCountProperty, value);
        }

        public static readonly AvaloniaProperty RepeatCountProperty =
            AvaloniaProperty.RegisterAttached(
              "RepeatCount",
              typeof(RepeatCount),
              typeof(AnimationBehavior),
              new PropertyMetadata(
                default(RepeatCount),
                RepeatCountChanged));

        #endregion

        #region AutoStart

#if WPF
        [AttachedPropertyBrowsableForType(typeof(Image))]
#endif
        public static bool GetAutoStart(AvaloniaObject obj)
        {
            return (bool)obj.GetValue(AutoStartProperty);
        }

        public static void SetAutoStart(AvaloniaObject obj, bool value)
        {
            obj.SetValue(AutoStartProperty, value);
        }

        public static readonly AvaloniaProperty AutoStartProperty =
            AvaloniaProperty.RegisterAttached(
                "AutoStart",
                typeof(bool),
                typeof(AnimationBehavior),
                new PropertyMetadata(true));

        #endregion

        #region AnimateInDesignMode


        public static bool GetAnimateInDesignMode(AvaloniaObject obj)
        {
            return (bool)obj.GetValue(AnimateInDesignModeProperty);
        }

        public static void SetAnimateInDesignMode(AvaloniaObject obj, bool value)
        {
            obj.SetValue(AnimateInDesignModeProperty, value);
        }

        public static readonly AvaloniaProperty AnimateInDesignModeProperty =
            AvaloniaProperty.RegisterAttached(
                "AnimateInDesignMode",
                typeof(bool),
                typeof(AnimationBehavior),
                new PropertyMetadata(
                    false,
                    AnimateInDesignModeChanged));

        #endregion

        #region Animator

        public static Animator GetAnimator(AvaloniaObject obj)
        {
            return (Animator)obj.GetValue(AnimatorProperty);
        }

        private static void SetAnimator(AvaloniaObject obj, Animator value)
        {
            obj.SetValue(AnimatorProperty, value);
        }

        public static readonly AvaloniaProperty AnimatorProperty =
            AvaloniaProperty.RegisterAttached(
                "Animator",
                typeof(Animator),
                typeof(AnimationBehavior),
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

        public static void AddErrorHandler(AvaloniaObject d, AnimationErrorEventHandler handler)
        {
            (d as UIElement)?.AddHandler(ErrorEvent, handler);
        }

        public static void RemoveErrorHandler(AvaloniaObject d, AnimationErrorEventHandler handler)
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

        public static void AddDownloadProgressHandler(AvaloniaObject d, DownloadProgressEventHandler handler)
        {
            (d as UIElement)?.AddHandler(DownloadProgressEvent, handler);
        }

        public static void RemoveDownloadProgressHandler(AvaloniaObject d, DownloadProgressEventHandler handler)
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

        public static void AddLoadedHandler(AvaloniaObject d, RoutedEventHandler handler)
        {
            (d as UIElement)?.AddHandler(LoadedEvent, handler);
        }

        public static void RemoveLoadedHandler(AvaloniaObject d, RoutedEventHandler handler)
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
            sender.RaiseEvent(new EventArgs(LoadedEvent, sender));
#elif WINRT || SILVERLIGHT
            Loaded?.Invoke(sender, EventArgs.Empty);
#endif
        }




        #endregion

        #endregion

        #region Private attached properties

        // private static int GetSeqNum(AvaloniaObject obj)
        // {
        //     return (int)obj.GetValue(SeqNumProperty);
        // }

        // private static void SetSeqNum(AvaloniaObject obj, int value)
        // {
        //     obj.SetValue(SeqNumProperty, value);
        // }

        // private static readonly AvaloniaProperty SeqNumProperty =
        //     AvaloniaProperty.RegisterAttached("SeqNum", typeof(int), typeof(AnimationBehavior), new PropertyMetadata(0));

        private static readonly AttachedProperty<int> SeqNumProperty =
                    AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, int>("SeqNum");

        public static int GetSeqNum(Image target)
        {
            return target.GetValue(SeqNumProperty);
        }

        public static void SetSeqNum(Image target, int value)
        {
            target.SetValue(SeqNumProperty, value);
        }

        #endregion

        private static void SourceChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var image = e.Sender as Image;
            if (image == null)
                return;

            InitAnimation(image);
        }

        private static void RepeatCountChanged(AvaloniaObject o, AvaloniaPropertyChangedEventArgs e)
        {
            GetAnimator(o)?.OnRepeatCountChanged();
        }

        private static void AnimateInDesignModeChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            var image = d as Image;
            if (image == null)
                return;

            InitAnimation(image);
        }

        // private static bool CheckDesignMode(Image image, Uri sourceUri, Stream sourceStream)
        // {
        //     if (IsInDesignMode(image) && !GetAnimateInDesignMode(image))
        //     {
        //         try
        //         {
        //             if (sourceStream != null)
        //             {
        //                 SetStaticImage(image, sourceStream);
        //             }
        //             else if (sourceUri != null)
        //             {
        //                 var bmp = new Bitmap();
        //                 bmp.UriSource = ;
        //                 image.Source = bmp;
        //             }
        //         }
        //         catch
        //         {
        //             image.Source = null;
        //         }
        //         return false;
        //     }
        //     return true;
        // }

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
                    InitAnimationAsync(image, stream.AsBuffered(), GetRepeatCount(image), seqNum);
                    return;
                }

                var uri = GetAbsoluteUri(image);
                if (uri != null)
                {
                    InitAnimationAsync(image, uri, GetRepeatCount(image), seqNum);
                }
            }
            catch (Exception ex)
            {
                OnError(image, ex, AnimationErrorKind.Loading);
            }
        }

        private static void Image_Loaded(object sender, EventArgs e)
        {
            var image = (Image)sender;
            image.Loaded -= Image_Loaded;
            InitAnimation(image);
        }

        private static void Image_Unloaded(object sender, EventArgs e)
        {
            var image = (Image)sender;
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

        private static async void InitAnimationAsync(Image image, Uri sourceUri, RepeatCount RepeatCount, int seqNum)
        {
            if (!CheckDesignMode(image, sourceUri, null))
                return;

            try
            {
                var progress = new Progress<int>(percentage => OnDownloadProgress(image, percentage));
                var animator = await ImageAnimator.CreateAsync(sourceUri, RepeatCount, progress, image);
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
            catch (Exception ex)
            {
                OnError(image, ex, AnimationErrorKind.Loading);
            }
        }

        private static async void InitAnimationAsync(Image image, Stream stream, RepeatCount RepeatCount, int seqNum)
        {
            if (!CheckDesignMode(image, null, stream))
                return;

            try
            {
                var animator = await ImageAnimator.CreateAsync(stream, RepeatCount, image);
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
            catch (Exception ex)
            {
                OnError(image, ex, AnimationErrorKind.Loading);
            }
        }

        private static async Task SetAnimatorCoreAsync(Image image, Animator animator)
        {
            SetAnimator(image, animator);
            animator.Error += AnimatorError;
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

            animator.Error -= AnimatorError;
            animator.Dispose();
            SetAnimator(image, null);
        }

//         // ReSharper disable once UnusedParameter.Local (used in WPF)
//         private static bool IsInDesignMode(AvaloniaObject obj)
//         {
// #if WPF
//             return DesignerProperties.GetIsInDesignMode(obj);
// #elif WINRT
//             return DesignMode.DesignModeEnabled;
// #elif SILVERLIGHT
//             return DesignerProperties.IsInDesignTool;
// #endif
//         }

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
            var bmp = new Bitmap(stream);
            image.Source = bmp;
        }
    }
}
