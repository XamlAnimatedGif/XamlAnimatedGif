using System;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using XamlAnimatedGif.Decoding;
using XamlAnimatedGif.Decompression;
using XamlAnimatedGif.Extensions;

namespace XamlAnimatedGif
{
    public class Animator : DependencyObject, IDisposable
    {
        private readonly Stream _sourceStream;
        private readonly Uri _sourceUri;
        private readonly GifDataStream _metadata;
        private readonly WriteableBitmap _bitmap;
        private readonly Storyboard _storyboard;

        #region Constructor and factory methods

        private Animator(Stream sourceStream, Uri sourceUri, GifDataStream metadata)
        {
            _sourceStream = sourceStream;
            _sourceUri = sourceUri;
            _metadata = metadata;
            _bitmap = CreateBitmap(metadata);
            _storyboard = CreateStoryboard(metadata);
        }

        internal static async Task<Animator> CreateAsync(Uri sourceUri)
        {
            var stream = GetStreamFromUri(sourceUri);
            try
            {
                return await CreateAsync(stream, sourceUri);
            }
            catch
            {
                if (stream != null)
                    stream.Dispose();
                throw;
            }
        }

        internal static Task<Animator> CreateAsync(Stream sourceStream)
        {
            return CreateAsync(sourceStream, null);
        }

        internal static async Task<Animator> CreateAsync(Stream sourceStream, Uri sourceUri)
        {
            var stream = sourceStream.AsBuffered();
            var metadata = await GifDataStream.ReadAsync(stream);
            return new Animator(stream, sourceUri, metadata);
        }

        #endregion

        #region Animation

        public int FrameCount
        {
            get { return _metadata.Frames.Count; }
        }

        public void Play()
        {
            _storyboard.Begin();
            //_storyboard.Pause();
        }

        public void Stop()
        {
            _storyboard.Stop();
        }

        public void Pause()
        {
            _storyboard.Pause();
        }

        public void Resume()
        {
            _storyboard.Resume();
        }

        public bool IsPaused
        {
            get
            {
                return _storyboard.GetIsPaused();
            }
        }

        public bool IsComplete
        {
            get { return _storyboard.GetCurrentState() == ClockState.Filling; }
        }

        public event EventHandler CurrentFrameChanged;

        protected virtual void OnCurrentFrameChanged()
        {
            EventHandler handler = CurrentFrameChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        public int CurrentFrameIndex
        {
            get { return (int)GetValue(CurrentFrameIndexProperty); }
        }

        private static readonly DependencyProperty CurrentFrameIndexProperty =
            DependencyProperty.Register("CurrentFrameIndex", typeof(int), typeof(Animator), new PropertyMetadata(-1, CurrentFrameIndexChanged));

        private static void CurrentFrameIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var animator = d as Animator;
            if (animator == null)
                return;
            animator.OnCurrentFrameChanged();
            animator.RenderFrameAsync((int) e.NewValue);
        }

        private Storyboard CreateStoryboard(GifDataStream metadata)
        {
            var animation = new Int32AnimationUsingKeyFrames();
            var totalDuration = TimeSpan.Zero;
            for (int i = 0; i < metadata.Frames.Count; i++)
            {
                var frame = metadata.Frames[i];
                var keyFrame = new DiscreteInt32KeyFrame(i, totalDuration);
                animation.KeyFrames.Add(keyFrame);
                totalDuration += GetFrameDelay(frame);
            }

            animation.RepeatBehavior = GetRepeatBehavior(metadata);

            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(CurrentFrameIndexProperty));

            return new Storyboard
            {
                Children = {animation}
            };
        }

        #endregion

        #region Rendering

        private static WriteableBitmap CreateBitmap(GifDataStream metadata)
        {
            var palette = CreatePalette(metadata);
            var desc = metadata.Header.LogicalScreenDescriptor;
            var bitmap = new WriteableBitmap(desc.Width, desc.Height, 96, 96, PixelFormats.Indexed8, palette);
            return bitmap;
        }

        private static BitmapPalette CreatePalette(GifDataStream metadata)
        {
            var desc = metadata.Header.LogicalScreenDescriptor;
            if (desc.HasGlobalColorTable)
            {
                var colors = metadata.GlobalColorTable.Select(gc => Color.FromRgb(gc.R, gc.G, gc.B));

                // TODO: implement the case where frames have a local color table

                var palette = new BitmapPalette(colors.ToList());
                return palette;
            }
            // TODO: implement the case where there are only local color tables
            throw new NotSupportedException("Images without a global color table are not supported");
        }

        private async void RenderFrameAsync(int frameIndex)
        {
            try
            {
                await RenderFrameCoreAsync(frameIndex);
            }
            catch
            {
                // TODO: call error handler?
            }
        }

        private async Task RenderFrameCoreAsync(int frameIndex)
        {
            Debug.WriteLine("Entering RenderFrameCoreAsync({0})", frameIndex);
            var frame = _metadata.Frames[frameIndex];
            var desc = frame.Descriptor;
            using (var indexStream = GetIndexStream(frame))
            {
                _bitmap.Lock();
                try
                {
                    int stride = desc.Width;
                    byte[] lineBuffer = new byte[stride];
                    for (int y = 0; y < desc.Height; y++)
                    {
                        int read = await indexStream.ReadAsync(lineBuffer, 0, stride);
                        if (read != stride)
                            throw new EndOfStreamException();
                        int offset = (desc.Top + y) * _bitmap.BackBufferStride + desc.Left;
                        Marshal.Copy(lineBuffer, 0, _bitmap.BackBuffer + offset, stride);
                    }
                    var rect = new Int32Rect(desc.Left, desc.Top, desc.Width, desc.Height);
                    _bitmap.AddDirtyRect(rect);
                }
                finally
                {
                    _bitmap.Unlock();
                }
                Debug.WriteLine("Leaving RenderFrameCoreAsync({0})", frameIndex);
            }
        }

        private Stream GetIndexStream(GifFrame frame)
        {
            var data = frame.ImageData;
            _sourceStream.Seek(data.CompressedDataStartOffset, SeekOrigin.Begin);
            var dataBlockStream = new GifDataBlockStream(_sourceStream, true);
            var lzwStream = new LzwDecompressStream(dataBlockStream, data.LzwMinimumCodeSize);
            return lzwStream;
        }

        internal BitmapSource Bitmap
        {
            get { return _bitmap; }
        }

        #endregion

        #region Helper methods

        private static Stream GetStreamFromUri(Uri uri)
        {
            if (uri.Scheme == PackUriHelper.UriSchemePack)
            {
                StreamResourceInfo sri;
                if (uri.Authority == "siteoforigin:,,,")
                    sri = Application.GetRemoteStream(uri);
                else
                    sri = Application.GetResourceStream(uri);

                if (sri != null)
                    return sri.Stream;
            }
            else if (uri.Scheme == Uri.UriSchemeFile)
            {
                return File.OpenRead(uri.LocalPath);
            }
            else
            {
                throw new NotSupportedException("Only pack:// and file:// URIs are supported");
            }
            return null;
        }

        private static TimeSpan GetFrameDelay(GifFrame frame)
        {
            var gce = frame.GraphicControl;
            if (gce != null)
            {
                if (gce.Delay != 0)
                    return TimeSpan.FromMilliseconds(gce.Delay);
            }
            return TimeSpan.FromMilliseconds(100);
        }

        private RepeatBehavior GetRepeatBehavior(GifDataStream metadata)
        {
            if (metadata.RepeatCount == 0)
                return RepeatBehavior.Forever;
            return new RepeatBehavior(metadata.RepeatCount);
        }

        #endregion

        #region Finalizer and Dispose

        ~Animator()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _sourceStream.Dispose();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}
