using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using AvaloniaGif.Extensions;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

using TaskEx = System.Threading.Tasks.Task;
using Avalonia;
using Avalonia.Platform;
using System.Threading;

namespace AvaloniaGif
{

    partial class UriLoader
    {
        public Task<Stream> GetStreamFromUriAsync(Uri uri, IProgress<double> progress, CancellationToken token)
        {
            if (uri.IsAbsoluteUri && (uri.Scheme == "http" || uri.Scheme == "https"))
                return GetNetworkStreamAsync(uri, progress, token);

            return GetStreamFromUriCoreAsync(uri);
        }

        private static async Task<Stream> GetNetworkStreamAsync(Uri uri, IProgress<double> progress, CancellationToken token)
        {
            string cacheFileName = GetCacheFileName(uri);
            var cacheStream = await OpenTempFileStreamAsync(cacheFileName);
            if (cacheStream == null)
            {
                await DownloadToCacheFileAsync(uri, cacheFileName, progress, token);
            }
            progress.Report(100);
            return await OpenTempFileStreamAsync(cacheFileName);
        }

        private static async Task DownloadToCacheFileAsync(Uri uri, string fileName, IProgress<double> progress, CancellationToken token)
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
                                        progress.Report(bytesCopied / length);
                                    else
                                        progress.Report(-1);
                                });
                        }
                        await responseStream.CopyToAsync(fileStream, absoluteProgress, cancellationToken:token);
                    }
                }
            }
            catch
            {
                await DeleteTempFileAsync(fileName);
                throw;
            }
        }

        private static Task<Stream> GetStreamFromUriCoreAsync(Uri uri)
        {
            if (uri.Scheme == "resm")
            {
                var assetLocator = AvaloniaLocator.Current.GetService<IAssetLoader>();
                
                var sri = assetLocator.Open(uri);

                if (sri != null)
                    return TaskEx.FromResult(sri);

                throw new FileNotFoundException("Cannot find file with the specified URI");
            }

            if (uri.Scheme == Uri.UriSchemeFile)
            {
                return TaskEx.FromResult<Stream>(File.OpenRead(uri.LocalPath));
            }

            throw new NotSupportedException("Only pack:, file:, http: and https: URIs are supported");
        }

        private static Task<Stream> OpenTempFileStreamAsync(string fileName)
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

        private static Task<Stream> CreateTempFileStreamAsync(string fileName)
        {
            string path = Path.Combine(Path.GetTempPath(), fileName);
            Stream stream = File.OpenWrite(path);
            stream.SetLength(0);
            return TaskEx.FromResult(stream);
        }

        private static Task DeleteTempFileAsync(string fileName)
        {
            if (File.Exists(fileName))
                File.Delete(fileName);
            return TaskEx.FromResult(fileName);
        }

        private static string GetCacheFileName(Uri uri)
        {
            using (var sha1 = SHA1.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(uri.AbsoluteUri);
                var hash = sha1.ComputeHash(bytes);
                return ToHex(hash);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            return bytes.Aggregate(
                new StringBuilder(),
                (sb, b) => sb.Append(b.ToString("X2")),
                sb => sb.ToString());
        }
    }
}