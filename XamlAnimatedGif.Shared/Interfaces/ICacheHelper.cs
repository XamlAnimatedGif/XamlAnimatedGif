#region

using System.IO;
using System.Threading.Tasks;

#endregion

namespace XamlAnimatedGif.Interfaces
{
    internal interface ICacheHelper
    {
        Task SaveAsync(string id, Stream stream);

        Task<Stream> GetAsync(string id);
    }
}