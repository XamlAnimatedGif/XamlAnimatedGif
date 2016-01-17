using System;

namespace XamlAnimatedGif
{
    public interface IProgress<in T>
    {
        void Report(T value);
    }

    class Progress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public Progress(Action<T> handler)
        {
            _handler = handler;
        }

        public void Report(T value)
        {
            _handler?.Invoke(value);
        }
    }
}
