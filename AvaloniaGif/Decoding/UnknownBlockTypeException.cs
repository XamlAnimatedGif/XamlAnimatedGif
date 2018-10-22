using System;
#if WPF
using System.Runtime.Serialization;
#endif

namespace XamlAnimatedGif.Decoding
{
#if WPF
    [Serializable]
#endif
    public class UnknownBlockTypeException : GifDecoderException
    {
        internal UnknownBlockTypeException(string message) : base(message) { }
        internal UnknownBlockTypeException(string message, Exception inner) : base(message, inner) { }

#if WPF
        protected UnknownBlockTypeException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        { }
#endif
    }
}