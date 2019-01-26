using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using XamlAnimatedGif.Extensions;
using TaskEx = System.Threading.Tasks.Task;


namespace XamlAnimatedGif
{
    public abstract class UriLoader
    {
        public abstract Task<Stream> GetStreamFromUriCoreAsync(Uri uri);

        public Task<Stream> GetStreamFromUriAsync(Uri uri, IProgress<int> progress)
        {
            if (uri.IsAbsoluteUri && (uri.Scheme == "http" || uri.Scheme == "https"))
                return GetNetworkStreamAsync(uri, progress);
            return GetStreamFromUriCoreAsync(uri);
        }

        public static async Task<Stream> GetNetworkStreamAsync(Uri uri, IProgress<int> progress)
        {
            string cacheFileName = GetCacheFileName(uri);
            var cacheStream = await OpenTempFileStreamAsync(cacheFileName);
            if (cacheStream == null)
            {
                await DownloadToCacheFileAsync(uri, cacheFileName, progress);
            }
            progress.Report(100);
            return await OpenTempFileStreamAsync(cacheFileName);
        }

        public static async Task DownloadToCacheFileAsync(Uri uri, string fileName, IProgress<int> progress)
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
                                        progress.Report((int)(100 * bytesCopied / length));
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

        public static Task<Stream> OpenTempFileStreamAsync(string fileName)
        {
            string path = Path.Combine(Path.GetTempPath(), fileName);
            Stream stream = null;
            try
            {
                stream = File.OpenRead(path);
            }
            catch (FileNotFoundException)
            {
            }
            return TaskEx.FromResult(stream);
        }

        public static Task<Stream> CreateTempFileStreamAsync(string fileName)
        {
            string path = Path.Combine(Path.GetTempPath(), fileName);
            Stream stream = File.OpenWrite(path);
            stream.SetLength(0);
            return TaskEx.FromResult(stream);
        }

        public static Task DeleteTempFileAsync(string fileName)
        {
            if (File.Exists(fileName))
                File.Delete(fileName);
            return TaskEx.FromResult(fileName);
        }

        public static string GetCacheFileName(Uri uri)
        {
            using (var sha1 = SHA1.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(uri.AbsoluteUri);
                var hash = sha1.ComputeHash(bytes);
                return ToHex(hash);
            }
        }

        public static string ToHex(byte[] bytes)
        {
            return bytes.Aggregate(
                new StringBuilder(),
                (sb, b) => sb.Append(b.ToString("X2")),
                sb => sb.ToString());
        }
    }
}