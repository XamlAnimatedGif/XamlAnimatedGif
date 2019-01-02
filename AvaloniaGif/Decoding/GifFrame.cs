using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace AvaloniaGif.Decoding
{
    internal class GifFrame : GifBlock
    {
        internal const int ImageSeparator = 0x2C;

        public GifImageDescriptor Descriptor { get; private set; }
        public GifColor[] LocalColorTable { get; private set; }
        public IList<GifExtension> Extensions { get; private set; }
        public GifImageData ImageData { get; private set; }
        public GifGraphicControlExtension GraphicControl { get; set; }

        private GifFrame()
        {
        }

        internal override GifBlockKind Kind
        {
            get { return GifBlockKind.GraphicRendering; }
        }

        internal new static GifFrame Read(Stream stream, IEnumerable<GifExtension> controlExtensions)
        {
            var frame = new GifFrame();

            frame.ReadInternal(stream, controlExtensions);
            return frame;
        }

        private void ReadInternal(Stream stream, IEnumerable<GifExtension> controlExtensions)
        {
            // Note: at this point, the Image Separator (0x2C) has already been read

            Descriptor = GifImageDescriptor.Read(stream);
            if (Descriptor.HasLocalColorTable)
            {
                LocalColorTable =  GifHelpers.ReadColorTable(stream, Descriptor.LocalColorTableSize);
            }
            ImageData = GifImageData.Read(stream);
            Extensions = controlExtensions.ToList().AsReadOnly();
            GraphicControl = Extensions.OfType<GifGraphicControlExtension>().FirstOrDefault();
        }
    }
}
