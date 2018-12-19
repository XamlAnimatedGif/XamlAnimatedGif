using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia;

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
    }
}
