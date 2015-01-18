using System;
using System.IO;
using System.Threading.Tasks;

namespace XamlAnimatedGif.Interfaces
{
    interface INetworkHelper
    {
        Task<Stream> GetNetworkStreamAsync(Uri uri);
    }
}
