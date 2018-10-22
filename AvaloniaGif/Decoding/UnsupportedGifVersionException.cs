using System;
#if WPF
using System.Runtime.Serialization;
#endif

namespace XamlAnimatedGif.Decoding
{
#if WPF
    [Serializable]
#endif
    public class UnsupportedGifVersionException : GifDecoderException
    {
        internal UnsupportedGifVersionException(string message) : base(message) { }
        internal UnsupportedGifVersionException(string message, Exception inner) : base(message, inner) { }

#if WPF
        protected UnsupportedGifVersionException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        { }
#endif
    }
}