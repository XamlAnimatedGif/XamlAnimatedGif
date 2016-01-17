using System.Windows;

namespace XamlAnimatedGif
{
#if WPF
    public delegate void DownloadProgressEventHandler(DependencyObject d, DownloadProgressEventArgs e);

    public class DownloadProgressEventArgs : RoutedEventArgs
#elif WINRT
    public class DownloadProgressEventArgs : System.EventArgs
#endif
    {
        public int Progress { get; set; }

#if WPF
        public DownloadProgressEventArgs(object source, int progress) : base(AnimationBehavior.DownloadProgressEvent, source)
#elif WINRT
        public DownloadProgressEventArgs(int progress)
#endif
        {
            Progress = progress;
        }
    }
}
