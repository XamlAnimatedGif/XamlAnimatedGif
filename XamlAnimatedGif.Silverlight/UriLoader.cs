using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace XamlAnimatedGif
{
    partial class UriLoader
    {
        private static Task<Stream> GetStreamFromUriCoreAsync(Uri uri)
        {
            var sri = Application.GetResourceStream(uri);

            if (sri != null)
                return TaskEx.FromResult(sri.Stream);

            throw new FileNotFoundException("Cannot find file with the specified URI");
        }

        private static Task<Stream> OpenTempFileStreamAsync(string fileName)
        {
            var store = IsolatedStorageFile.GetUserStoreForApplication();
            Stream stream = null;
            try
            {
                string path = Path.Combine("Temp", fileName);
                stream = store.OpenFile(path, FileMode.Open, FileAccess.Read);
            }
            catch (IsolatedStorageException)
            {
            }
            return TaskEx.FromResult(stream);
        }

        private static Task<Stream> CreateTempFileStreamAsync(string fileName)
        {
            var store = IsolatedStorageFile.GetUserStoreForApplication();
            store.CreateDirectory("Temp");
            string path = Path.Combine("Temp", fileName);
            Stream stream = store.CreateFile(path);
            stream.SetLength(0);
            return TaskEx.FromResult(stream);
        }

        private static Task DeleteTempFileAsync(string fileName)
        {
            var store = IsolatedStorageFile.GetUserStoreForApplication();
            if (store.FileExists(fileName))
                store.DeleteFile(fileName);
            return TaskEx.FromResult(fileName);
        }

        private static string GetCacheFileName(Uri uri)
        {
            using (var sha1 = new SHA1Managed())
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