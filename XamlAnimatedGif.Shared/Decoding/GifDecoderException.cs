using System;

namespace XamlAnimatedGif.Decoding
{
#if WPF
    [Serializable]
#endif
    internal class GifDecoderException : Exception
    {
        internal GifDecoderException() { }
        internal GifDecoderException(string message) : base(message) { }
        internal GifDecoderException(string message, Exception inner) : base(message, inner) { }

#if WPF
        protected GifDecoderException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
#endif
    }
}
