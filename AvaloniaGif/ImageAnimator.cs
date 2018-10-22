using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Controls;
using AvaloniaGif.Decoding;

namespace AvaloniaGif
{
    internal class ImageAnimator : Animator
    {
        private readonly Image _image;

        public ImageAnimator(Stream sourceStream, Uri sourceUri, GifDataStream metadata, RepeatCount RepeatCount, Image image) : base(sourceStream, sourceUri, metadata, RepeatCount)
        {
            _image = image;
            OnRepeatCountChanged(); // in case the value has changed during creation
        }

        protected override RepeatCount GetSpecifiedRepeatCount() => AnimationBehavior.GetRepeatCount(_image);

        protected override object ErrorSource => _image;

        public static Task<ImageAnimator> CreateAsync(Uri sourceUri, RepeatCount RepeatCount, IProgress<int> progress, Image image)
        {
            return CreateAsyncCore(
                sourceUri,
                progress,
                (stream, metadata) => new ImageAnimator(stream, sourceUri, metadata, RepeatCount, image));
        }

        public static Task<ImageAnimator> CreateAsync(Stream sourceStream, RepeatCount RepeatCount, Image image)
        {
            return CreateAsyncCore(
                sourceStream,
                metadata => new ImageAnimator(sourceStream, null, metadata, RepeatCount, image));
        }
    }
}