using System.Threading.Tasks;
using TaskEx = System.Threading.Tasks.Task;

namespace AvaloniaGif.Decoding
{
    internal class GifTrailer : GifBlock
    {
        internal const int TrailerByte = 0x3B;

        private GifTrailer()
        {
        }

        internal override GifBlockKind Kind
        {
            get { return GifBlockKind.Other; }
        }

        internal static GifTrailer Read()
        {
            return new GifTrailer();
        }
    }
}
