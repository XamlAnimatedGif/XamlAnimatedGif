using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources.Core;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using XamlAnimatedGif.Interfaces;

namespace XamlAnimatedGif
{
    internal class UriLoader : IUriLoader
    {
        private readonly INetworkHelper _networkHelper;
        public UriLoader()
        {
            _networkHelper = new NetworkHelper();
        }

        public async Task<Stream> GetStreamFromUriAsync(Uri uri)
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
                    var rm = ResourceManager.Current;
                    var context = ResourceContext.GetForCurrentView();
                    var candidate = rm.MainResourceMap.GetValue(uri.LocalPath, context);
                    if (candidate != null && candidate.IsMatch)
                    {
                        var file = await candidate.GetValueAsFileAsync();
                        return await file.OpenStreamForReadAsync();
                    }
                    throw new Exception("Resource not found");
                case "http":
                case "https":
                    return await _networkHelper.GetNetworkStreamAsync(uri);
                case "file":
                {
                    var file = await StorageFile.GetFileFromPathAsync(uri.LocalPath);
                    return await file.OpenStreamForReadAsync();
                }
            }

            throw new NotSupportedException("Only ms-appx:, ms-appdata:, ms-resource:, http:, https: and file: URIs are supported");
        }
    }
}
