using System;

namespace XamlAnimatedGif.Decoding
{
    public class UnknownBlockTypeException : GifDecoderException
    {
        internal UnknownBlockTypeException(string message) : base(message) { }
        internal UnknownBlockTypeException(string message, Exception inner) : base(message, inner) { }
    }
}