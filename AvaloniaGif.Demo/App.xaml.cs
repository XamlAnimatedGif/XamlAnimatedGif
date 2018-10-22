using Avalonia.Markup.Xaml;
using Avalonia;

namespace AvaloniaGif.Demo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
