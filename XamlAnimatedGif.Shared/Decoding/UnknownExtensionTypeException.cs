using System; 

namespace XamlAnimatedGif.Decoding
{ 
    public class UnknownExtensionTypeException : GifDecoderException
    {
        internal UnknownExtensionTypeException(string message) : base(message) { }
        internal UnknownExtensionTypeException(string message, Exception inner) : base(message, inner) { }
    }
}