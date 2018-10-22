#if WINRT
using System.Collections.Generic;
using System.Collections.ObjectModel;
#endif

namespace XamlAnimatedGif.Extensions
{
    static class CollectionExtensions
    {
#if WINRT
        public static ReadOnlyCollection<T> AsReadOnly<T>(this IList<T> list)
        {
            return new ReadOnlyCollection<T>(list);
        }
#endif
    }
}
