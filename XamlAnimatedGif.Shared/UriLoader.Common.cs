using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using XamlAnimatedGif.Extensions;

namespace XamlAnimatedGif
{
    partial class UriLoader
    {
        public Task<Stream> GetStreamFromUriAsync(Uri uri, IProgress<int> progress)
        {
            if (uri.Scheme == "http" || uri.Scheme == "https")
                return GetNetworkStreamAsync(uri, progress);
            return GetStreamFromUriCoreAsync(uri);
        }

        private static async Task<Stream> GetNetworkStreamAsync(Uri uri, IProgress<int> progress)
        {
            string cacheFileName = GetCacheFileName(uri);
            var cacheStream = await OpenTempFileStreamAsync(cacheFileName);
            if (cacheStream == null)
            {
                await DownloadToCacheFileAsync(uri, cacheFileName, progress);
            }
            return await OpenTempFileStreamAsync(cacheFileName);
        }

        private static async Task DownloadToCacheFileAsync(Uri uri, string fileName, IProgress<int> progress)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, uri);
                    var response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    long length = response.Content.Headers.ContentLength ?? 0;
                    using (var responseStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = await CreateTempFileStreamAsync(fileName))
                    {
                        IProgress<long> absoluteProgress = null;
                        if (progress != null)
                        {
                            absoluteProgress =
                                new Progress<long>(bytesCopied =>
                                {
                                    if (length > 0)
                                        progress.Report((int) (100*bytesCopied/length));
                                    else
                                        progress.Report(-1);
                                });
                        }
                        await responseStream.CopyToAsync(fileStream, absoluteProgress);
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
