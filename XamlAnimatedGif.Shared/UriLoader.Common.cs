using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace XamlAnimatedGif
{
    partial class UriLoader
    {
        public Task<Stream> GetStreamFromUriAsync(Uri uri, Action<int> progress = null)
        {
            if (uri.Scheme == "http" || uri.Scheme == "https")
                return GetNetworkStreamAsync(uri, progress);
            return GetStreamFromUriCoreAsync(uri);
        }

        private static async Task<Stream> GetNetworkStreamAsync(Uri uri, Action<int> progress = null)
        {
            string cacheFileName = GetCacheFileName(uri);
            var cacheStream = await OpenTempFileStreamAsync(cacheFileName);
            if (cacheStream == null)
            {
                await DownloadToCacheFileAsync(uri, cacheFileName, progress);
            }
            return await OpenTempFileStreamAsync(cacheFileName);
        }

        private static async Task DownloadToCacheFileAsync(Uri uri, string fileName, Action<int> progress)
        {
            try
            {
                using (var client = new Windows.Web.Http.HttpClient())
                {
                    var requestAsync = client.GetAsync(uri);
                    requestAsync.Progress += (i, p) =>
                    {
                        if (null != progress)
                        {
                            if (p.TotalBytesToReceive != 0 && p.TotalBytesToReceive != null)
                            {
                                progress((int)((p.BytesReceived * 100) / p.TotalBytesToReceive));
                            }
                        }
                    };
                    using (var message = await requestAsync)
                    {
                        using (var fileStream = await CreateTempFileStreamAsync(fileName))
                        {
                            using (var responseStream = await message.Content.ReadAsInputStreamAsync())
                            {
                                await responseStream.AsStreamForRead().CopyToAsync(fileStream);
                            }
                        }
                    }
                }
            }
            catch
            {
                await DeleteTempFileAsync(fileName);
                throw;
            }
        }

        private static async Task DownloadToCacheFileAsync(Uri uri, string fileName)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    using (var responseStream = await client.GetStreamAsync(uri))
                    {
                        using (var fileStream = await CreateTempFileStreamAsync(fileName))
                        {
                            await responseStream.CopyToAsync(fileStream);
                        }
                    }
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
