#region

using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using XamlAnimatedGif.Interfaces;

#endregion

namespace XamlAnimatedGif
{
    internal class NetworkHelper : INetworkHelper
    {
        private readonly ICacheHelper _cacheHelper;

        public NetworkHelper()
        {
            _cacheHelper = new CacheHelper();
        }

        public async Task<Stream> GetNetworkStreamAsync(Uri uri)
        {
            //generating temp file name by hashing the url
            var tempId = GetHash(uri.AbsoluteUri);
            var cacheStream = await _cacheHelper.GetAsync(tempId);

            if (cacheStream != null)
                return cacheStream;

            //no cache, continue with download
            using (var client = new WebClient())
            {
                //fails if the status is not a success one
                var bytes = await client.DownloadDataTaskAsync(uri);
                //using a memory stream, need a seekable one
                var mem = new MemoryStream(bytes);

                //cache the gif
                await _cacheHelper.SaveAsync(tempId, mem);

                return mem;
            }
        }

        private string GetHash(string s)
        {
            //create new instance of md5
            var sha1 = SHA1.Create();

            //convert the input text to array of bytes
            var hashData = sha1.ComputeHash(Encoding.Default.GetBytes(s));

            //create new instance of StringBuilder to save hashed data
            var returnValue = new StringBuilder();

            //loop for each byte and add it to StringBuilder
            foreach (var t in hashData)
            {
                returnValue.Append(t.ToString());
            }

            // return hexadecimal string
            return returnValue.ToString();
        }
    }
}