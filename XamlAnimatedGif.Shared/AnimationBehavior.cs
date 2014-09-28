using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace XamlAnimatedGif
{
    public static class AnimationBehavior
    {
        public static Uri GetSourceUri(DependencyObject obj)
        {
            return (Uri)obj.GetValue(SourceUriProperty);
        }

        public static void SetSourceUri(DependencyObject obj, Uri value)
        {
            obj.SetValue(SourceUriProperty, value);
        }

        // Using a DependencyProperty as the backing store for SourceUri.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SourceUriProperty =
            DependencyProperty.RegisterAttached(
              "SourceUri",
              typeof(Uri),
              typeof(AnimationBehavior),
              new UIPropertyMetadata(
                null,
                SourceUriChanged));

        private static void SourceUriChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var oldValue = (Uri)e.OldValue;
            var newValue = (Uri)e.NewValue;

            if (oldValue != null)
            {
                // Cleanup old animation
            }
            if (newValue != null)
            {
                // Init new animation
                InitAnimationAsync(newValue);
            }
        }

        private static void InitAnimationAsync(Uri sourceUri)
        {
            
        }
    }
}
