using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        private readonly Image _image;
        private readonly Dictionary<int, GifPalette> _palettes;
        private readonly WriteableBitmap _bitmap;
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
            _previousBackBuffer = new byte[metadata.Header.LogicalScreenDescriptor.Height * _bitmap.BackBufferStride];
            _storyboard = CreateStoryboard(metadata, repeatBehavior);
        }

        internal static async Task<Animator> CreateAsync(Uri sourceUri, RepeatBehavior repeatBehavior = default(RepeatBehavior), Image image = null)
        {
            var stream = GetStreamFromUri(sourceUri);
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
            var stream = sourceStream.AsBuffered();
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
        }

        public void Pause()
        {
            if (_isStarted)
                _storyboard.Pause();
        }

        public bool IsPaused
        {
            get
            {
                if (_isStarted)
                    return _storyboard.GetIsPaused();
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
            var animation = new Int32AnimationUsingKeyFrames();
            var totalDuration = TimeSpan.Zero;
            for (int i = 0; i < metadata.Frames.Count; i++)
            {
                var frame = metadata.Frames[i];
                var keyFrame = new DiscreteInt32KeyFrame(i, totalDuration);
                animation.KeyFrames.Add(keyFrame);
                totalDuration += GetFrameDelay(frame);
            }

            animation.RepeatBehavior =
                repeatBehavior == default(RepeatBehavior)
                    ? GetRepeatBehavior(metadata)
                    : repeatBehavior;

            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(CurrentFrameIndexProperty));


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
            var bitmap = new WriteableBitmap(desc.Width, desc.Height, 96, 96, PixelFormats.Bgra32, null);
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
                        .Select(gc => Color.FromRgb(gc.R, gc.G, gc.B))
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
                            .Select(gc => Color.FromRgb(gc.R, gc.G, gc.B))
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
            Debug.WriteLine("Entering RenderFrameCoreAsync({0})", frameIndex);

            if (frameIndex < 0)
                return;

            var frame = _metadata.Frames[frameIndex];
            var desc = frame.Descriptor;
            using (var indexStream = GetIndexStream(frame))
            {
                _bitmap.Lock();
                try
                {
                    DisposePreviousFrame(frame);

                    int stride = _bitmap.BackBufferStride;
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

                        int offset = (desc.Top + y) * stride + desc.Left * 4;

                        if (transparencyIndex > 0)
                        {
                            Marshal.Copy(_bitmap.BackBuffer + offset, lineBuffer, 0, bufferLength);
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
                        Marshal.Copy(lineBuffer, 0, _bitmap.BackBuffer + offset, bufferLength);
                    }
                    var rect = new Int32Rect(desc.Left, desc.Top, desc.Width, desc.Height);
                    _bitmap.AddDirtyRect(rect);
                    _previousFrame = frame;
                }
                finally
                {
                    _bitmap.Unlock();
                }
                Debug.WriteLine("Leaving RenderFrameCoreAsync({0})", frameIndex);
            }
        }

        private void WriteColor(byte[] lineBuffer, Color color, int startIndex)
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
                                ClearArea(_bitmap, _previousFrame.Descriptor);
                                break;
                            }
                        case GifFrameDisposalMethod.RestorePrevious:
                            {
                                Marshal.Copy(_previousBackBuffer, 0, _bitmap.BackBuffer, _previousBackBuffer.Length);
                                var desc = _metadata.Header.LogicalScreenDescriptor;
                                var rect = new Int32Rect(0, 0, desc.Width, desc.Height);
                                _bitmap.AddDirtyRect(rect);
                                break;
                            }
                        default:
                            {
                                throw new ArgumentOutOfRangeException();
                            }
                    }
                }

                var gce = currentFrame.GraphicControl;
                if (gce != null && gce.DisposalMethod == GifFrameDisposalMethod.RestorePrevious)
                {
                    Marshal.Copy(_bitmap.BackBuffer, _previousBackBuffer, 0, _previousBackBuffer.Length);
                }
            }
        }

        private static void ClearArea(WriteableBitmap bitmap, IGifRect rect)
        {
            int stride = bitmap.BackBufferStride;
            int bufferLength = 4 * rect.Width;
            byte[] lineBuffer = new byte[bufferLength];
            for (int y = 0; y < rect.Height; y++)
            {
                int offset = (rect.Top + y) * stride + 4 * rect.Left;
                Marshal.Copy(lineBuffer, 0, bitmap.BackBuffer + offset, bufferLength);
            }
            bitmap.AddDirtyRect(new Int32Rect(rect.Left, rect.Top, rect.Width, rect.Height));
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
