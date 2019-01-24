using System;

namespace XamlAnimatedGif.Decoding
{
    public class InvalidSignatureException : GifDecoderException
    {
        internal InvalidSignatureException(string message) : base(message) { }
        internal InvalidSignatureException(string message, Exception inner) : base(message, inner) { }
    }
}