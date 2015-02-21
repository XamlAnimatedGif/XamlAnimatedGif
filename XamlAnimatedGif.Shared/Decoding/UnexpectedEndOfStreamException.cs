using System;
#if WPF
using System.Runtime.Serialization;
#endif

namespace XamlAnimatedGif.Decoding
{
#if WPF
    [Serializable]
#endif
    public class UnexpectedEndOfStreamException : GifDecoderException
    {
        internal UnexpectedEndOfStreamException(string message) : base(message) { }
        internal UnexpectedEndOfStreamException(string message, Exception inner) : base(message, inner) { }

#if WPF
        protected UnexpectedEndOfStreamException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        { }
#endif
    }
}