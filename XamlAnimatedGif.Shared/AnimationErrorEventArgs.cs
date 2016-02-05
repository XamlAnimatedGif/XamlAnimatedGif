using System;
#if WPF || SILVERLIGHT
using System.Windows;
#elif WINRT
using Windows.UI.Xaml;
#endif

namespace XamlAnimatedGif
{
#if WPF
    public delegate void AnimationErrorEventHandler(DependencyObject d, AnimationErrorEventArgs e);

    public class AnimationErrorEventArgs : RoutedEventArgs
    {
        public AnimationErrorEventArgs(object source, Exception exception, AnimationErrorKind kind)
            : base(AnimationBehavior.ErrorEvent, source)
#elif WINRT || SILVERLIGHT
    public class AnimationErrorEventArgs : EventArgs
    {
        public AnimationErrorEventArgs(Exception exception, AnimationErrorKind kind)
#endif
        {
            Exception = exception;
            Kind = kind;
        }

        public Exception Exception { get; }

        public AnimationErrorKind Kind { get; }
    }

    public enum AnimationErrorKind
    {
        Loading,
        Rendering
    }
}
