using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace XamlAnimatedGif.Demo
{
    /// <summary>
    /// Interaction logic for InputBox.xaml
    /// </summary>
    public partial class InputBox : Window, INotifyPropertyChanged
    {
        public InputBox()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void OKButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool Set<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, newValue))
                return false;

            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        private string _prompt;
        public string Prompt
        {
            get => _prompt;
            set => Set(ref _prompt, value);
        }

        private string _text;
        public string Text
        {
            get => _text;
            set => Set(ref _text, value);
        }

        public static string Show(string prompt, string title, string initialValue = null)
        {
            var inputBox = new InputBox
            {
                Title = title,
                Prompt = prompt,
                Text = initialValue
            };

            if (inputBox.ShowDialog() is true)
            {
                return inputBox.Text;
            }

            return null;
        }
    }
}
