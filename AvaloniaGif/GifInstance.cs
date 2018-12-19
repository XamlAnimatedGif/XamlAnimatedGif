// using AvaloniaGif.Decoding;
// using System;
// using System.IO;
// using System.Threading.Tasks;
// using Avalonia;
// using Avalonia.Controls;
// using Avalonia.Animation;
// using System.Threading;

// namespace AvaloniaGif
// {
//     public class GifInstance : IDisposable
//     {
//         public Image Image { get; set; }
//         public GifRenderer Renderer { get; private set; }
//         public IClock Clock { get; private set; }

//         public int FrameCount { get; private set; }
//         public Stream Stream { get; private set; }
//         public IterationCount IterationCount { get; private set; }
//         public bool AutoStart { get; private set; } = true;
//         public Progress<int> Progress { get; private set; }
//         internal GifDataStream GifDataStream { get; private set; }
//         internal CancellationTokenSource _rendererSignal = new CancellationTokenSource();

//         TimeSpan _prevTime, _delta;
//         int CurrentFrame;
//         IDisposable sub1;
//         bool streamCanDispose, _isFirstRun;
//         int hashCode;

//         public void Dispose()
//         {
//             if (streamCanDispose)
//                 Stream?.Dispose();

//             sub1?.Dispose();
//             _rendererSignal?.Cancel();
//             Renderer?.Dispose();
//         }

//         public async void SetSource(object newValue)
//         {
//             // var sourceUri = newValue as Uri;
//             // var sourceStr = newValue as Stream;

//             // Stream stream;

//             // if (sourceUri != null)
//             // {
//             //     streamCanDispose = true;
//             //     this.Progress = new Progress<int>();
//             //     // stream = await new UriLoader().GetStreamFromUriAsync(sourceUri, Progress);
//             // }
//             // else if (sourceStr != null)
//             // {
//             //     stream = sourceStr;
//             // }
//             // else
//             // {
//             //     throw new InvalidDataException("Missing valid URI or Stream.");
//             // }

//             // Stream = stream;

//             // Image.DetachedFromVisualTree += delegate
//             // {
//             //     Dispose();
//             // };

//             // Run();
//         }

//         private async void Run()
//         {
//             if (!Stream.CanSeek)
//                 throw new ArgumentException("The stream is not seekable");

//             Stream.Seek(0, SeekOrigin.Begin);
//             GifDataStream = await GifDataStream.ReadAsync(Stream);
//             Renderer = new GifRenderer(Stream, GifDataStream);

//             this.Clock = Image.Clock ?? new Clock();
//             sub1 = Clock.Subscribe((x) => Step(x, _rendererSignal.Token));
//             this.FrameCount = Renderer.FrameCount;
//             Image.Source = Renderer.GifBitmap;

//         }

//         public async void Step(TimeSpan Time, CancellationToken token)
//         {
//             if (token.IsCancellationRequested) return;

//             if (!_isFirstRun)
//             {
//                 await Renderer.RenderFrameAsync(0, token);
//                 _isFirstRun = true;
//             }

//             _delta = Time - _prevTime;

//             if (_delta >= Renderer.GifFrameTimes[CurrentFrame])
//             {
//                 _prevTime = Time;
//                 await Renderer.RenderFrameAsync(CurrentFrame, token);
//                 CurrentFrame = (CurrentFrame + 1) % FrameCount;
//                 Image.InvalidateVisual();
//             }
//         }

//         public void IterationCountChanged(AvaloniaPropertyChangedEventArgs e)
//         {
//             var newVal = (IterationCount)e.NewValue;
//             this.IterationCount = newVal;
//         }

//         public void AutoStartChanged(AvaloniaPropertyChangedEventArgs e)
//         {
//             var newVal = (bool)e.NewValue;
//             this.AutoStart = newVal;
//         }
//     }
// }