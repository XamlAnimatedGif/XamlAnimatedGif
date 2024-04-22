using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using XamlAnimatedGif.Decoding;

namespace XamlAnimatedGif
{
    internal class ImageAnimator : Animator
    {
        private readonly Image _image;

        public ImageAnimator(Stream sourceStream, Uri sourceUri, string tempPath, GifDataStream metadata, RepeatBehavior repeatBehavior,
            Image image) : this(sourceStream, sourceUri, tempPath, metadata, repeatBehavior, image, false)
        {
        }

        public ImageAnimator(Stream sourceStream, Uri sourceUri, string tempPath, GifDataStream metadata, RepeatBehavior repeatBehavior, Image image, bool cacheFrameDataInMemory) : base(sourceStream, sourceUri, metadata, repeatBehavior, cacheFrameDataInMemory)
        {
            _image = image;
            OnRepeatBehaviorChanged(); // in case the value has changed during creation
        }

        protected override RepeatBehavior GetSpecifiedRepeatBehavior() => AnimationBehavior.GetRepeatBehavior(_image);

        protected override object AnimationSource => _image;

        public static Task<ImageAnimator> CreateAsync(Uri sourceUri, string tempPath, RepeatBehavior repeatBehavior, IProgress<int> progress, Image image)
        {
            return CreateAsync(sourceUri, tempPath, repeatBehavior, progress, image, false);
        }

        public static Task<ImageAnimator> CreateAsync(Uri sourceUri, string tempPath, RepeatBehavior repeatBehavior, IProgress<int> progress, Image image, bool cacheFrameDataInMemory)
        {
            return CreateAsyncCore(
                sourceUri,
                tempPath,
                progress,
                (stream, metadata) => new ImageAnimator(stream, sourceUri, tempPath, metadata, repeatBehavior, image, cacheFrameDataInMemory));
        }

        public static Task<ImageAnimator> CreateAsync(Stream sourceStream, string tempPath, RepeatBehavior repeatBehavior, Image image)
        {
            return CreateAsync(sourceStream, tempPath, repeatBehavior, image);
        }
        public static Task<ImageAnimator> CreateAsync(Stream sourceStream, string tempPath, RepeatBehavior repeatBehavior, Image image, bool cacheFrameDataInMemory)
        {
            return CreateAsyncCore(
            sourceStream,
                metadata => new ImageAnimator(sourceStream, null, tempPath, metadata, repeatBehavior, image, cacheFrameDataInMemory));
        }
    }
}