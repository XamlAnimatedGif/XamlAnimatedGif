using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using XamlAnimatedGif.Interfaces;

namespace XamlAnimatedGif
{
    internal class CacheHelper : ICacheHelper
    {
        private readonly StorageFolder _folder = ApplicationData.Current.TemporaryFolder;

        public async Task SaveAsync(string id, Stream stream)
        {
            var tempFile = await _folder.CreateFileAsync(id, CreationCollisionOption.ReplaceExisting);
            try
            {
                using (var tempStream = await tempFile.OpenStreamForWriteAsync())
                {
                    await stream.CopyToAsync(tempStream);
                }
            }
            catch
            {
                await tempFile.DeleteAsync();
            }
        }

        public async Task<Stream> GetAsync(string id)
        {
            try
            {
                var tempFile = await _folder.GetFileAsync(id);

                //return the cache image
                var stream = await tempFile.OpenStreamForReadAsync();

                if (stream.Length > 0)
                    return stream;
            }
            catch (FileNotFoundException) { }
            return null;
        }
    }
}
