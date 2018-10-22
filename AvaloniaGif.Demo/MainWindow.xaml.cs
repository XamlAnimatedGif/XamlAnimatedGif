using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia;

using Avalonia.Logging.Serilog;

namespace AvaloniaGif.Demo
{
    /// <summary>
    /// Interaction logic for DesignTestWindow.xaml
    /// </summary>
    public class MainWindow : Window
    {
        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);
        }


        static void Main(string[] args)
        {
            BuildAvaloniaApp().Start<MainWindow>();
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .UseReactiveUI()
                .LogToDebug();
    }
}
