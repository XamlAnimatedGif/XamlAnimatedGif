﻿using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
#if !NET40
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace XamlAnimatedGif
{
    partial class UriLoader
    {
        private static Task<Stream> GetStreamFromUriCoreAsync(Uri uri)
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