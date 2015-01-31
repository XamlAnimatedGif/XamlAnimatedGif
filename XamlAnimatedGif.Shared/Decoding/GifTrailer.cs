using System.Threading.Tasks;
#if !NET40
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace XamlAnimatedGif.Decoding
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

        internal static Task<GifTrailer> ReadAsync()
        {
            return TaskEx.FromResult(new GifTrailer());
        }
    }
}
