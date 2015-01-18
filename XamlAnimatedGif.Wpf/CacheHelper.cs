#region

using System.IO;
using System.Threading.Tasks;
using XamlAnimatedGif.Interfaces;

#endregion

namespace XamlAnimatedGif
{
    public class CacheHelper : ICacheHelper
    {
        public async Task SaveAsync(string id, Stream stream)
        {
            var fileName = Path.Combine(Path.GetTempPath(), id);
            try
            {
                using (var tempStream = new FileStream(fileName, FileMode.Create))
                {
                    await stream.CopyToAsync(tempStream);
                }
            }
            catch
            {
                File.Delete(fileName);
            }
        }

        public async Task<Stream> GetAsync(string id)
        {
            var fileName = Path.Combine(Path.GetTempPath(), id);
            try
            {
                //wrapping filestream
                var stream = await Task.FromResult(new FileStream(fileName, FileMode.Open));

                //return the cache image
                if (stream.Length > 0)
                    return stream;
            }
            catch (FileNotFoundException)
            {
            }
            return null;
        }
    }
}