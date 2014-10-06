using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
#if WPF
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using System.IO.Packaging;
using System.Runtime.InteropServices;
using XamlAnimatedGif.Extensions;
#elif WINRT
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Media.Animation;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Resources.Core;
#endif

using XamlAnimatedGif.Decoding;
using XamlAnimatedGif.Decompression;

namespace XamlAnimatedGif
{
    public class Animator : DependencyObject, IDisposable
    {
        private readonly Stream _sourceStream;
        private readonly Uri _sourceUri;
        private readonly GifDataStream _metadata;
        private readonly Image _image;
        private readonly Dictionary<int, GifPalette> _palettes;
        private readonly WriteableBitmap _bitmap;
        private readonly int _stride;
        private readonly byte[] _previousBackBuffer;
        private readonly Storyboard _storyboard;
        

        #region Constructor and factory methods

        private Animator(Stream sourceStream, Uri sourceUri, GifDataStream metadata, RepeatBehavior repeatBehavior, Image image)
        {
            _sourceStream = sourceStream;
            _sourceUri = sourceUri;
            _metadata = metadata;
            _image = image;
            _palettes = CreatePalettes(metadata);
            _bitmap = CreateBitmap(metadata);
            var desc = metadata.Header.LogicalScreenDescriptor;
            _stride = 4 * ((desc.Width * 32 + 31) / 32);
            _previousBackBuffer = new byte[metadata.Header.LogicalScreenDescriptor.Height * _stride];
            _storyboard = CreateStoryboard(metadata, repeatBehavior);
        }

        internal static async Task<Animator> CreateAsync(Uri sourceUri, RepeatBehavior repeatBehavior = default(RepeatBehavior), Image image = null)
        {
            var stream = await GetStreamFromUriAsync(sourceUri);
            try
            {
                return await CreateAsync(stream, sourceUri, repeatBehavior, image);
            }
            catch
            {
                if (stream != null)
                    stream.Dispose();
                throw;
            }
        }

        internal static Task<Animator> CreateAsync(Stream sourceStream, RepeatBehavior repeatBehavior = default(RepeatBehavior), Image image = null)
        {
            return CreateAsync(sourceStream, null, repeatBehavior, image);
        }

        private static async Task<Animator> CreateAsync(Stream sourceStream, Uri sourceUri, RepeatBehavior repeatBehavior, Image image)
        {
#if WPF
            var stream = sourceStream.AsBuffered();
#else
            #warning TODO: buffer the stream
            var stream = sourceStream;
#endif
            var metadata = await GifDataStream.ReadAsync(stream);
            return new Animator(stream, sourceUri, metadata, repeatBehavior, image);
        }

        #endregion

        #region Animation

        public int FrameCount
        {
            get { return _metadata.Frames.Count; }
        }

        private bool _isStarted;

        public void Play()
        {
            if (_isStarted)
                _storyboard.Resume();
            else
                _storyboard.Begin();
            _isStarted = true;
#if WINRT
            _isPaused = false;
#endif
        }

#if WINRT
        private bool _isPaused;
#endif
        public void Pause()
        {
            if (_isStarted)
            {
                _storyboard.Pause();
#if WINRT
                _isPaused = true;
#endif
            }
        }

        public bool IsPaused
        {
            get
            {
                if (_isStarted)
                {
#if WPF
                    return _storyboard.GetIsPaused();
#elif WINRT
                    return _isPaused;
#endif
                }
                return true;
            }
        }

        public bool IsComplete
        {
            get
            {
                if (_isStarted)
                    return _storyboard.GetCurrentState() == ClockState.Filling;
                return false;
            }
        }

        public event EventHandler CurrentFrameChanged;

        protected virtual void OnCurrentFrameChanged()
        {
            EventHandler handler = CurrentFrameChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        public event EventHandler AnimationCompleted;

        protected virtual void OnAnimationCompleted()
        {
            EventHandler handler = AnimationCompleted;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        public int CurrentFrameIndex
        {
            get { return (int)GetValue(CurrentFrameIndexProperty); }
            internal set { SetValue(CurrentFrameIndexProperty, value); }
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

        private Storyboard CreateStoryboard(GifDataStream metadata, RepeatBehavior repeatBehavior)
        {
#if WPF
            var animation = new Int32AnimationUsingKeyFrames();
#elif WINRT
            var animation = new ObjectAnimationUsingKeyFrames {EnableDependentAnimation = true};
#endif
            var totalDuration = TimeSpan.Zero;
            for (int i = 0; i < metadata.Frames.Count; i++)
            {
                var frame = metadata.Frames[i];
#if WPF 
                var keyFrame = new DiscreteInt32KeyFrame(i, totalDuration);
#elif WINRT
                var keyFrame = new DiscreteObjectKeyFrame {Value = i, KeyTime = totalDuration};
#endif
                animation.KeyFrames.Add(keyFrame);
                totalDuration += GetFrameDelay(frame);
            }

            animation.RepeatBehavior =
                repeatBehavior == default(RepeatBehavior)
                    ? GetRepeatBehavior(metadata)
                    : repeatBehavior;

            Storyboard.SetTarget(animation, this);
#if WPF
            Storyboard.SetTargetProperty(animation, new PropertyPath(CurrentFrameIndexProperty));
#elif WINRT
            Storyboard.SetTargetProperty(animation, "CurrentFrameIndex");
#else
            #error Not implemented
#endif


            var sb = new Storyboard
            {
                Children = {animation}
            };

            sb.Completed += (sender, e) => OnAnimationCompleted();

            return sb;
        }

        #endregion

        #region Rendering

        private WriteableBitmap CreateBitmap(GifDataStream metadata)
        {
            var desc = metadata.Header.LogicalScreenDescriptor;
#if WPF
            var bitmap = new WriteableBitmap(desc.Width, desc.Height, 96, 96, PixelFormats.Bgra32, null);
#elif WINRT
            var bitmap = new WriteableBitmap(desc.Width, desc.Height);
#else
            #error Not implemented
#endif
            return bitmap;
        }

        private Dictionary<int, GifPalette> CreatePalettes(GifDataStream metadata)
        {
            var palettes = new Dictionary<int, GifPalette>();
            Color[] globalColorTable = null;
            if (metadata.Header.LogicalScreenDescriptor.HasGlobalColorTable)
            {
                globalColorTable =
                    metadata.GlobalColorTable
                        .Select(gc => Color.FromArgb(0xFF, gc.R, gc.G, gc.B))
                        .ToArray();
            }

            for (int i = 0; i < metadata.Frames.Count; i++)
            {
                var frame = metadata.Frames[i];
                var colorTable = globalColorTable;
                if (frame.Descriptor.HasLocalColorTable)
                {
                    colorTable =
                        frame.LocalColorTable
                            .Select(gc => Color.FromArgb(0xFF, gc.R, gc.G, gc.B))
                            .ToArray();
                }

                int? transparencyIndex = null;
                var gce = frame.GraphicControl;
                if (gce != null && gce.HasTransparency)
                {
                    transparencyIndex = gce.TransparencyIndex;
                }

                palettes[i] = new GifPalette(transparencyIndex, colorTable);
            }

            return palettes;
        }

        internal Task RenderingTask { get; private set; }

        private static readonly Task _completedTask = Task.FromResult(0);
        private async void RenderFrameAsync(int frameIndex)
        {
            try
            {
                var task = RenderingTask = RenderFrameCoreAsync(frameIndex);
                await task;
            }
            catch(Exception ex)
            {
                object sender = (object) _image ?? this;
                AnimationBehavior.OnError(sender, ex, AnimationErrorKind.Rendering);
            }
            finally
            {
                RenderingTask = _completedTask;
            }
        }

        private GifFrame _previousFrame;
        private async Task RenderFrameCoreAsync(int frameIndex)
        {
            if (frameIndex < 0)
                return;

            var frame = _metadata.Frames[frameIndex];
            var desc = frame.Descriptor;
            using (var indexStream = GetIndexStream(frame))
            {
#if WPF
                _bitmap.Lock();
                try
                {
#endif
                    DisposePreviousFrame(frame);

                    int bufferLength = 4 * desc.Width;
                    byte[] indexBuffer = new byte[desc.Width];
                    byte[] lineBuffer = new byte[bufferLength];

                    var palette = _palettes[frameIndex];
                    int transparencyIndex = palette.TransparencyIndex ?? -1;
                    for (int y = 0; y < desc.Height; y++)
                    {
                        int read = await indexStream.ReadAsync(indexBuffer, 0, desc.Width);
                        if (read != desc.Width)
                            throw new EndOfStreamException();

                        int offset = (desc.Top + y) * _stride + desc.Left * 4;

                        if (transparencyIndex > 0)
                        {
                            CopyFromBitmap(lineBuffer, _bitmap, offset, bufferLength);
                        }

                        for (int x = 0; x < desc.Width; x++)
                        {
                            byte index = indexBuffer[x];
                            int i = 4 * x;
                            if (index != transparencyIndex)
                            {
                                WriteColor(lineBuffer, palette[index], i);
                            }
                        }
                        CopyToBitmap(lineBuffer, _bitmap, offset, bufferLength);
                    }
#if WPF
                    var rect = new Int32Rect(desc.Left, desc.Top, desc.Width, desc.Height);
                    _bitmap.AddDirtyRect(rect);
                }
                finally
                {
                    _bitmap.Unlock();
                }
#elif WINRT
                _bitmap.Invalidate();
#endif
                _previousFrame = frame;
            }
        }

        private static void CopyToBitmap(byte[] buffer, WriteableBitmap bitmap, int offset, int length)
        {
#if WPF
            Marshal.Copy(buffer, 0, bitmap.BackBuffer + offset, length);
#elif WINRT
            buffer.CopyTo(0, bitmap.PixelBuffer, (uint)offset, length);
#else
            #error Not implemented
#endif
        }

        private static void CopyFromBitmap(byte[] buffer, WriteableBitmap bitmap, int offset, int length)
        {
#if WPF
            Marshal.Copy(bitmap.BackBuffer + offset, buffer, 0, length);
#elif WINRT

            bitmap.PixelBuffer.CopyTo((uint)offset, buffer, 0, length);
#else
            #error Not implemented
#endif
        }

        private static void WriteColor(byte[] lineBuffer, Color color, int startIndex)
        {
            lineBuffer[startIndex] = color.B;
            lineBuffer[startIndex + 1] = color.G;
            lineBuffer[startIndex + 2] = color.R;
            lineBuffer[startIndex + 3] = color.A;
        }

        private void DisposePreviousFrame(GifFrame currentFrame)
        {
            if (_previousFrame != null)
            {
                var pgce = _previousFrame.GraphicControl;
                if (pgce != null)
                {
                    switch (pgce.DisposalMethod)
                    {
                        case GifFrameDisposalMethod.None:
                        case GifFrameDisposalMethod.DoNotDispose:
                            {
                                // Leave previous frame in place
                                break;
                            }
                        case GifFrameDisposalMethod.RestoreBackground:
                            {
                                ClearArea(_previousFrame.Descriptor);
                                break;
                            }
                        case GifFrameDisposalMethod.RestorePrevious:
                            {
                                CopyToBitmap(_previousBackBuffer, _bitmap, 0, _previousBackBuffer.Length);
#if WPF
                                var desc = _metadata.Header.LogicalScreenDescriptor;
                                var rect = new Int32Rect(0, 0, desc.Width, desc.Height);
                                _bitmap.AddDirtyRect(rect);
#endif
                                break;
                            }
                        default:
                            {
                                throw new ArgumentOutOfRangeException();
                            }
                    }
                }
            }

            var gce = currentFrame.GraphicControl;
            if (gce != null && gce.DisposalMethod == GifFrameDisposalMethod.RestorePrevious)
            {
                CopyFromBitmap(_previousBackBuffer, _bitmap, 0, _previousBackBuffer.Length);
            }
        }

        private void ClearArea(IGifRect rect)
        {
            int bufferLength = 4 * rect.Width;
            byte[] lineBuffer = new byte[bufferLength];
            for (int y = 0; y < rect.Height; y++)
            {
                int offset = (rect.Top + y) * _stride + 4 * rect.Left;
                CopyToBitmap(lineBuffer, _bitmap, offset, bufferLength);
            }
#if WPF
            _bitmap.AddDirtyRect(new Int32Rect(rect.Left, rect.Top, rect.Width, rect.Height));
#endif
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

#if WPF

        private static Task<Stream> GetStreamFromUriAsync(Uri uri)
        {
            if (uri.Scheme == PackUriHelper.UriSchemePack)
            {
                StreamResourceInfo sri;
                if (uri.Authority == "siteoforigin:,,,")
                    sri = Application.GetRemoteStream(uri);
                else
                    sri = Application.GetResourceStream(uri);

                if (sri != null)
                    return Task.FromResult(sri.Stream);

                throw new FileNotFoundException("Cannot find file with the specified URI");
            }
            
            if (uri.Scheme == Uri.UriSchemeFile)
            {
                return Task.FromResult<Stream>(File.OpenRead(uri.LocalPath));
            }

            throw new NotSupportedException("Only pack: and file: URIs are supported");
        }
#elif WINRT
        private static async Task<Stream> GetStreamFromUriAsync(Uri uri)
        {
            if (uri.Scheme == "ms-appx" || uri.Scheme == "ms-appdata")
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                return await file.OpenStreamForReadAsync();
            }
            if (uri.Scheme == "ms-resource")
            {
                var candidate = ResourceManager.Current.MainResourceMap.GetValue(uri.LocalPath);
                if (candidate != null && candidate.IsMatch)
                {
                    var file = await candidate.GetValueAsFileAsync();
                    return await file.OpenStreamForReadAsync();
                }
                throw new Exception("Resource not found");
            }
            if (uri.Scheme == "file")
            {
                var file = await StorageFile.GetFileFromPathAsync(uri.LocalPath);
                return await file.OpenStreamForReadAsync();
            }
            throw new NotSupportedException("Only ms-appx:, ms-appdata:, ms-resource: and file: URIs are supported");
        }
#endif

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
                    _storyboard.Stop();
                    _storyboard.Children.Clear();
                    _sourceStream.Dispose();
                }
                _disposed = true;
            }
        }

        #endregion

        public override string ToString()
        {
            string s = _sourceUri != null ? _sourceUri.ToString() : _sourceStream.ToString();
            return "GIF: " + s;
        }

        class GifPalette
        {
            private readonly int? _transparencyIndex;
            private readonly Color[] _colors;

            public GifPalette(int? transparencyIndex, Color[] colors)
            {
                _transparencyIndex = transparencyIndex;
                _colors = colors;
            }

            public int? TransparencyIndex
            {
                get { return _transparencyIndex; }
            }

            public Color this[int i]
            {
                get { return _colors[i]; }
            }
        }
    }
}
