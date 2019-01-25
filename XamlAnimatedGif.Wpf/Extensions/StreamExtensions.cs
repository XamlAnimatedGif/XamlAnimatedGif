using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XamlAnimatedGif.Extensions
{
    public static class StreamExtensions
    {
        public static Stream AsBuffered(this Stream stream)
        {
            var bs = stream as BufferedStream;
            if (bs != null)
                return bs;
            return new BufferedStream(stream);
        }
    }
}
