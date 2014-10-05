using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace XamlAnimatedGif
{
    public static class AnimationBehavior
    {
        #region Public attached properties and events

        #region SourceUri

        [AttachedPropertyBrowsableForType(typeof(Image))]
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

        [AttachedPropertyBrowsableForType(typeof(Image))]
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
              new UIPropertyMetadata(
                default(RepeatBehavior),
                SourceChanged));

        #endregion

        #region AutoStart

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

        #endregion

        private static void SourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var image = o as Image;
            if (image == null)
                return;

            ClearAnimatorCore(image);

            var stream = GetSourceStream(image);
            if (stream != null)
            {
                InitAnimationAsync(image, stream, GetRepeatBehavior(image));
            }
            else
            {
                var uri = GetSourceUri(image);
                if (uri != null)
                {
                    InitAnimationAsync(image, uri, GetRepeatBehavior(image));
                }
            }
        }

        private static async void InitAnimationAsync(Image image, Uri sourceUri, RepeatBehavior repeatBehavior)
        {
            try
            {
                var animator = await Animator.CreateAsync(sourceUri, repeatBehavior);
                SetAnimatorCore(image, animator);
            }
            catch
            {
                // TODO: call an error handler?
            }
        }

        private static async void InitAnimationAsync(Image image, Stream stream, RepeatBehavior repeatBehavior)
        {
            try
            {
                var animator = await Animator.CreateAsync(stream, repeatBehavior);
                SetAnimatorCore(image, animator);
            }
            catch
            {
                // TODO: call an error handler?
            }
        }

        private static void SetAnimatorCore(Image image, Animator animator)
        {
            SetAnimator(image, animator);
            image.Source = animator.Bitmap;
            if (GetAutoStart(image))
                animator.Play();
            else
                animator.CurrentFrameIndex = 0;
        }

        private static void ClearAnimatorCore(Image image)
        {
            var animator = GetAnimator(image);
            if (animator == null)
                return;

            animator.Dispose();
            SetAnimator(image, null);
        }


    }
}
