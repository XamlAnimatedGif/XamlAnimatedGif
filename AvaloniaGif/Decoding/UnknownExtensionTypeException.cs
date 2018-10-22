using System;
#if WPF
using System.Runtime.Serialization;
#endif

namespace XamlAnimatedGif.Decoding
{
#if WPF
    [Serializable]
#endif
    public class UnknownExtensionTypeException : GifDecoderException
    {
        internal UnknownExtensionTypeException(string message) : base(message) { }
        internal UnknownExtensionTypeException(string message, Exception inner) : base(message, inner) { }

#if WPF
        protected UnknownExtensionTypeException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        { }
#endif
    }
}