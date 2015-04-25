using System;
#if WPF
using System.Runtime.Serialization;
#endif

namespace XamlAnimatedGif.Decoding
{
#if WPF
    [Serializable]
#endif
    public abstract class GifDecoderException : Exception
    {
        protected GifDecoderException(string message) : base(message) { }
        protected GifDecoderException(string message, Exception inner) : base(message, inner) { }

#if WPF
        protected GifDecoderException(
          SerializationInfo info,
          StreamingContext context)
            : base(info, context) { }
#endif
    }
}
