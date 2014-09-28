using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace XamlAnimatedGif
{
    public static class AnimationBehavior
    {
        #region Public attached properties

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
                SourceUriChanged));

        public static Animator GetAnimator(DependencyObject obj)
        {
            return (Animator)obj.GetValue(AnimatorProperty);
        }

        private static void SetAnimator(DependencyObject obj, Animator value)
        {
            obj.SetValue(AnimatorProperty, value);
        }

        public static readonly DependencyProperty AnimatorProperty =
            DependencyProperty.RegisterAttached(
              "Animator",
              typeof(Animator),
              typeof(AnimationBehavior),
              new PropertyMetadata(null));


        #endregion

        private static void SourceUriChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var image = o as Image;
            if (image == null)
                return;

            var oldValue = (Uri)e.OldValue;
            var newValue = (Uri)e.NewValue;

            if (oldValue != null)
            {
                var animator = GetAnimator(image);
                if (animator != null)
                    animator.Dispose();
            }
            if (newValue != null)
            {
                // Init new animation
                InitAnimationAsync(image, newValue);
            }
        }

        private static async void InitAnimationAsync(Image image, Uri sourceUri)
        {
            try
            {
                var animator = await Animator.CreateAsync(sourceUri);
                SetAnimatorAndStart(image, animator);
            }
            catch
            {
                // TODO: call an error handler?
            }
        }

        private static async void InitAnimationAsync(Image image, Stream stream)
        {
            try
            {
                var animator = await Animator.CreateAsync(stream);
                SetAnimatorAndStart(image, animator);
            }
            catch
            {
                // TODO: call an error handler?
            }
        }

        private static void SetAnimatorAndStart(Image image, Animator animator)
        {
            SetAnimator(image, animator);
            image.Source = animator.Bitmap;
            animator.Play();
        }

    }
}
