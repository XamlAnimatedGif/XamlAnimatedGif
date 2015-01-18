#region

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
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
            var tempId = GetHash(HashAlgorithmNames.Sha1, uri.AbsoluteUri);
            var cacheStream = await _cacheHelper.GetAsync(tempId);

            if (cacheStream != null)
                return cacheStream;

            //no cache, continue with download
            using (var client = new HttpClient())
            {
                //fails if the status is not a success one
                var stream = await client.GetStreamAsync(uri);
                //using a memory stream, need a seekable one
                var mem = new MemoryStream();
                await stream.CopyToAsync(mem);
                stream.Dispose();
                mem.Position = 0;

                //cache the gif
                await _cacheHelper.SaveAsync(tempId, mem);

                return mem;
            }
        }

        private string GetHash(string algoritm, string s)
        {
            var alg = HashAlgorithmProvider.OpenAlgorithm(algoritm);
            var buff = CryptographicBuffer.ConvertStringToBinary(s, BinaryStringEncoding.Utf8);
            var hashed = alg.HashData(buff);
            var res = CryptographicBuffer.EncodeToHexString(hashed);
            return res;
        }
    }
}