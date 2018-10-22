using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvaloniaGif
{ 
    public delegate void AnimationErrorEventHandler(AvaloniaObject d, AnimationErrorEventArgs e);

    public class AnimationErrorEventArgs : RoutedEventArgs
    {
        public AnimationErrorEventArgs(Control source, Exception exception, AnimationErrorKind kind)

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
