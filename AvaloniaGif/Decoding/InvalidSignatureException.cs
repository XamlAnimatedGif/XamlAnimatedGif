using System;
#if WPF
using System.Runtime.Serialization;
#endif

namespace XamlAnimatedGif.Decoding
{
#if WPF
    [Serializable]
#endif
    public class InvalidSignatureException : GifDecoderException
    {
        internal InvalidSignatureException(string message) : base(message) { }
        internal InvalidSignatureException(string message, Exception inner) : base(message, inner) { }

#if WPF
        protected InvalidSignatureException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        { }
#endif
    }
}