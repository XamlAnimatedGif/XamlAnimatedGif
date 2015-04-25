using System;
#if WPF
using System.Windows;
#elif WINRT
using Windows.UI.Xaml;
#endif

namespace XamlAnimatedGif
{
    public delegate void AnimationErrorEventHandler(DependencyObject d, AnimationErrorEventArgs e);

#if WPF
    public class AnimationErrorEventArgs : RoutedEventArgs
    {
        public AnimationErrorEventArgs(object source, Exception exception, AnimationErrorKind kind)
            : base(AnimationBehavior.ErrorEvent, source)
#elif WINRT
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
