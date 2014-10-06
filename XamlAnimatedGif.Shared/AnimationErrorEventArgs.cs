using System;

namespace XamlAnimatedGif
{
    public class AnimationErrorEventArgs : EventArgs
    {
        private readonly Exception _exception;
        private readonly AnimationErrorKind _kind;

        public AnimationErrorEventArgs(Exception exception, AnimationErrorKind kind)
        {
            _exception = exception;
            _kind = kind;
        }

        public Exception Exception
        {
            get { return _exception; }
        }

        public AnimationErrorKind Kind
        {
            get { return _kind; }
        }
    }

    public enum AnimationErrorKind
    {
        Loading,
        Rendering
    }
}
