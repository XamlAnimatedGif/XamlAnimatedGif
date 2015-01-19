using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace XamlAnimatedGif
{
    partial class UriLoader
    {
        public Task<Stream> GetStreamFromUriAsync(Uri uri)
        {
            if (uri.Scheme == "http" || uri.Scheme == "https")
                return GetNetworkStreamAsync(uri);
            return GetStreamFromUriCoreAsync(uri);
        }

        private static async Task<Stream> GetNetworkStreamAsync(Uri uri)
        {
            string cacheFileName = GetCacheFileName(uri);
            var cacheStream = await OpenTempFileStreamAsync(cacheFileName);
            if (cacheStream == null)
            {
                await DownloadToCacheFileAsync(uri, cacheFileName);
            }
            return await OpenTempFileStreamAsync(cacheFileName);
        }

        private static async Task DownloadToCacheFileAsync(Uri uri, string fileName)
        {
            try
            {
                using (var client = new HttpClient())
                using (var responseStream = await client.GetStreamAsync(uri))
                using (var fileStream = await CreateTempFileStreamAsync(fileName))
                {
                    await responseStream.CopyToAsync(fileStream);
                }
            }
            catch
            {
                await DeleteTempFileAsync(fileName);
                throw;
            }
        }
    }
}
