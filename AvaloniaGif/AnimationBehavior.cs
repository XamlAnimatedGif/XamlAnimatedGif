using AvaloniaGif.Decoding;
using System;
using System.IO;
using System.Threading.Tasks;
using AvaloniaGif.Extensions;
using Avalonia;
using Avalonia.Media.Imaging;

using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Portable.Xaml.Markup;
using System.Threading;

namespace AvaloniaGif
{
    public class AnimationBehavior
    {

        static AnimationBehavior()
        {
            SourceUriProperty.Changed.Subscribe(SourceChanged);
            SourceStreamProperty.Changed.Subscribe(SourceChanged);
            RepeatCountProperty.Changed.Subscribe(RepeatCountChanged);
        }

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

        public static readonly AttachedProperty<Stream> SourceStreamProperty =
                    AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, Stream>("SourceStream");


        public static Stream GetSourceStream(Image target)
        {
            return target.GetValue(SourceStreamProperty);
        }


        public static void SetSourceStream(Image target, Stream value)
        {
            target.SetValue(SourceStreamProperty, value);
        }

        public static readonly AttachedProperty<RepeatCount> RepeatCountProperty =
                    AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, RepeatCount>("RepeatCount", RepeatCount.Loop);

        public static RepeatCount GetRepeatCount(Image target)
        {
            return target.GetValue(RepeatCountProperty);
        }

        public static void SetRepeatCount(Image target, RepeatCount value)
        {
            target.SetValue(RepeatCountProperty, value);
        }

        public static readonly AttachedProperty<bool> AutoStartProperty =
                    AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, bool>("AutoStart", true);

        public static bool GetAutoStart(Image target)
        {
            return target.GetValue(AutoStartProperty);
        }

        public static void SetAutoStart(Image target, bool value)
        {
            target.SetValue(AutoStartProperty, value);
        }


        public static readonly AttachedProperty<Animator> AnimatorProperty =
                    AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, Animator>("Animator");

        public static Animator GetAnimator(Image target)
        {
            return target.GetValue(AnimatorProperty);
        }

        public static void SetAnimator(Image target, Animator value)
        {
            target.SetValue(AnimatorProperty, value);
        }

        public static RoutedEvent<DownloadProgressEventArgs> DownloadProgressEvent =
                    RoutedEvent.Register<DownloadProgressEventArgs>("DownloadProgress", RoutingStrategies.Bubble, typeof(AnimationBehavior));


        internal static void OnDownloadProgress(Image image, int downloadPercentage)
        {
            image.RaiseEvent(new DownloadProgressEventArgs(image, downloadPercentage));
        }

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

        public static readonly AttachedProperty<IDisposable> ClockSubscriptionProperty =
                    AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, IDisposable>("ClockSubscription");


        public static IDisposable GetClockSubscription(Image target)
        {
            return target.GetValue(ClockSubscriptionProperty);
        }


        public static void SetClockSubscription(Image target, IDisposable value)
        {
            target.SetValue(ClockSubscriptionProperty, value);
        }

        private static void SourceChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var image = e.Sender as Image;
            if (image == null)
                return;

            image.AttachedToLogicalTree += (i, o) =>
            {
                InitAnimation(image);
            };

            image.DetachedFromVisualTree += (i, o) =>
            {
                ClearAnimatorCore(image);
            };


        }

        private static void RepeatCountChanged(AvaloniaPropertyChangedEventArgs e)
        {
            GetAnimator(e.Sender as Image)?.OnRepeatCountChanged();
        }

        private static void InitAnimation(Image image)
        {
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
                throw;
                //  OnError(image, ex, AnimationErrorKind.Loading);
            }
        }

        private static Uri GetAbsoluteUri(Image image)
        {
            var uri = GetSourceUri(image);
            if (uri == null)
                return null;

            if (!uri.IsAbsoluteUri)
            {

                var baseUri = ((IUriContext)image).BaseUri;

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

        private static async void InitAnimationAsync(Image image, Uri sourceUri, RepeatCount RepeatCount, int seqNum)
        {


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
                image.Source = animator.Bitmap;
                //  OnLoaded(image);
            }
            catch (InvalidSignatureException)
            {
                await SetStaticImageAsync(image, sourceUri);
                // OnLoaded(image);
            }
            catch (Exception ex)
            {
                throw;
                // OnError(image, ex, AnimationErrorKind.Loading);
            }
        }

        private static async void InitAnimationAsync(Image image, Stream stream, RepeatCount RepeatCount, int seqNum)
        {
            // if (!CheckDesignMode(image, null, stream))
            //     return;

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
                image.Source = animator.Bitmap;
                //   OnLoaded(image);
            }
            catch (InvalidSignatureException)
            {
                SetStaticImage(image, stream);

                ///   OnLoaded(image);
            }
            catch (Exception ex)
            {
                throw;
                //  OnError(image, ex, AnimationErrorKind.Loading);
            }
        }

        private static async Task SetAnimatorCoreAsync(Image image, Animator animator)
        {
            SetAnimator(image, animator);
            //  animator.Error += AnimatorError;
            image.Source = animator.Bitmap;
            var k = new CancellationTokenSource();
            if (GetAutoStart(image))
            {
                var curTime = TimeSpan.FromMilliseconds(Environment.TickCount);

                var imageS = (image.Clock ?? new Clock())
                                .Subscribe(delegate
                                {
                                    GetAnimator(image)?.RunNext(curTime, k.Token, ()=>image.InvalidateVisual());
                                    ;
                                });

                SetClockSubscription(image, imageS);

                //animator.Play(k.Token);
            }
            else
                await animator.ShowFirstFrameAsync();
        }

        private static void ClearAnimatorCore(Image image)
        {
            var animator = GetAnimator(image);
            if (animator == null)
                return;

            //   animator.Error -= AnimatorError;
            GetClockSubscription(image)?.Dispose();
            animator.Dispose();
            SetAnimator(image, null);
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
                throw;
                //  OnError(image, ex, AnimationErrorKind.Loading);
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
                //OnError(image, ex, AnimationErrorKind.Loading);
            }
        }

        private static void SetStaticImageCore(Image image, Stream stream)
        {
            // stream.Seek(0, SeekOrigin.Begin);
            // var bmp = new Bitmap(stream);
            // image.Source = bmp;
        }
    }
}
