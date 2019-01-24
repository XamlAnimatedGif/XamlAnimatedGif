using System; 

namespace XamlAnimatedGif.Decoding
{ 
    public class InvalidBlockSizeException : GifDecoderException
    {
        internal InvalidBlockSizeException(string message) : base(message) { }
        internal InvalidBlockSizeException(string message, Exception inner) : base(message, inner) { }

 
    }
}