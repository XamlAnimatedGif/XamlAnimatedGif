using System.Windows;

namespace XamlAnimatedGif
{
    public delegate void AnimationCompletedEventHandler(DependencyObject d, AnimationCompletedEventArgs e);

    public class AnimationCompletedEventArgs : RoutedEventArgs
    {
        public AnimationCompletedEventArgs(RoutedEvent e, object source)
            : base(e, source)
        {

        }
    }
}
