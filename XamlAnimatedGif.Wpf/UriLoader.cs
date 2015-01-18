using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Resources;
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
            if (uri.Scheme == "http" || uri.Scheme == "https")
                return await _networkHelper.GetNetworkStreamAsync(uri);

            if (uri.Scheme == PackUriHelper.UriSchemePack)
            {
                var sri = uri.Authority == "siteoforigin:,,," ? Application.GetRemoteStream(uri) : Application.GetResourceStream(uri);

                if (sri != null)
                    return sri.Stream;

                throw new FileNotFoundException("Cannot find file with the specified URI");
            }

            if (uri.Scheme == Uri.UriSchemeFile)
            {
                return File.OpenRead(uri.LocalPath);
            }

            throw new NotSupportedException("Only pack: and file: URIs are supported");
        }
    }
}
