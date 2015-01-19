using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources.Core;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Windows.Storage.Streams;

namespace XamlAnimatedGif
{
    partial class UriLoader
    {
        private static async Task<Stream> GetStreamFromUriCoreAsync(Uri uri)
        {
            switch (uri.Scheme)
            {
                case "ms-appx":
                case "ms-appdata":
                {
                    var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                    return await file.OpenStreamForReadAsync();
                }
                case "ms-resource":
                {
                    var rm = ResourceManager.Current;
                    var context = ResourceContext.GetForCurrentView();
                    var candidate = rm.MainResourceMap.GetValue(uri.LocalPath, context);
                    if (candidate != null && candidate.IsMatch)
                    {
                        var file = await candidate.GetValueAsFileAsync();
                        return await file.OpenStreamForReadAsync();
                    }
                    throw new Exception("Resource not found");
                }
                case "file":
                {
                    var file = await StorageFile.GetFileFromPathAsync(uri.LocalPath);
                    return await file.OpenStreamForReadAsync();
                }
            }

            throw new NotSupportedException("Only ms-appx:, ms-appdata:, ms-resource:, http:, https: and file: URIs are supported");
        }

        private static async Task<Stream> OpenTempFileStreamAsync(string fileName)
        {
            IStorageFile file;
            try
            {
                file = await ApplicationData.Current.TemporaryFolder.GetFileAsync(fileName);
            }
            catch (FileNotFoundException)
            {
                return null;
            }

            return await file.OpenStreamForReadAsync();
        }

        private static async Task<Stream> CreateTempFileStreamAsync(string fileName)
        {
            IStorageFile file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            return await file.OpenStreamForWriteAsync();
        }

        private static async Task DeleteTempFileAsync(string fileName)
        {
            try
            {
                var file = await ApplicationData.Current.TemporaryFolder.GetFileAsync(fileName);
                await file.DeleteAsync();
            }
            catch (FileNotFoundException)
            {
            }
        }

        private static string GetCacheFileName(Uri uri)
        {
            HashAlgorithmProvider sha1 = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha1);
            byte[] bytes = Encoding.UTF8.GetBytes(uri.AbsoluteUri);
            IBuffer bytesBuffer = CryptographicBuffer.CreateFromByteArray(bytes);
            IBuffer hashBuffer = sha1.HashData(bytesBuffer);
            return CryptographicBuffer.EncodeToHexString(hashBuffer);
        }
    }
}
