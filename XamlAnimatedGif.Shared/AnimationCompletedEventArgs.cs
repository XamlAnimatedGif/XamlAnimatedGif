#if WPF || SILVERLIGHT
using System.Windows;
#elif WINRT
using Windows.UI.Xaml;
#endif

namespace XamlAnimatedGif
{
#if WPF
    public delegate void AnimationCompletedEventHandler(DependencyObject d, AnimationCompletedEventArgs e);

    public class AnimationCompletedEventArgs : RoutedEventArgs
    {
        public AnimationCompletedEventArgs(object source)
            : base(AnimationBehavior.AnimationCompletedEvent, source)
        {
#elif WINRT || SILVERLIGHT
    public class AnimationCompletedEventArgs : EventArgs
    {
        public AnimationCompletedEventArgs(object source)
        {
            Source = source;
#endif
        }

#if WINRT || SILVERLIGHT
        public object Source { get; }
#endif
    }
}
