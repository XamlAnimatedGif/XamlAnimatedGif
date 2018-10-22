using System;
using Avalonia;  

namespace AvaloniaGif
{
    public delegate void AnimationErrorEventHandler(AvaloniaObject d, AnimationErrorEventArgs e);

    public class AnimationErrorEventArgs : EventArgs
    {
        public AnimationErrorEventArgs(object source, Exception exception, AnimationErrorKind kind)
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
