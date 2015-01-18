using System;
using System.IO;
using System.Threading.Tasks;

namespace XamlAnimatedGif.Interfaces
{
    internal interface IUriLoader
    {
        Task<Stream> GetStreamFromUriAsync(Uri uri);
    }
}
