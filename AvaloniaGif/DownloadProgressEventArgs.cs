
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvaloniaGif
{

    public delegate void DownloadProgressEventHandler(AvaloniaObject d, DownloadProgressEventArgs e);

    public class DownloadProgressEventArgs : RoutedEventArgs
    {
        public int Progress { get; set; }

        public DownloadProgressEventArgs(Control source, int progress) : base(AnimationBehavior.DownloadProgressEvent, source)
        {
            Progress = progress;
        }
    }
}
