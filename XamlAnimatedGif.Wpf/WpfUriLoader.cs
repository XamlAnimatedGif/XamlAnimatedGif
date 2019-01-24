using System;
using System.IO;
using System.IO.Packaging;
using System.Threading.Tasks;
using System.Windows; 
using TaskEx = System.Threading.Tasks.Task;

namespace XamlAnimatedGif
{
    internal class WpfUriLoader : UriLoader
    {
        public override Task<Stream> GetStreamFromUriCoreAsync(Uri uri)
        {
            if (uri.Scheme == PackUriHelper.UriSchemePack)
            {
                var sri = uri.Authority == "siteoforigin:,,,"
                    ? Application.GetRemoteStream(uri)
                    : Application.GetResourceStream(uri);

                if (sri != null)
                    return TaskEx.FromResult(sri.Stream);

                throw new FileNotFoundException("Cannot find file with the specified URI");
            }

            if (uri.Scheme == Uri.UriSchemeFile)
            {
                return TaskEx.FromResult<Stream>(File.OpenRead(uri.LocalPath));
            }

            throw new NotSupportedException("Only pack:, file:, http: and https: URIs are supported");
        }
    }
}