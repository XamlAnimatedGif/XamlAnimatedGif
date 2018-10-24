using System.Collections.ObjectModel;
using System;
using System.ComponentModel;

namespace AvaloniaGif.Demo
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string v)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(v));
        }

        public MainWindowViewModel()
        {
            this.AvailableGifs = new ObservableCollection<Uri>()
            {
                new Uri("resm:AvaloniaGif.Demo.Images.earth.gif"),
                new Uri("resm:AvaloniaGif.Demo.Images.bomb.gif"),
                new Uri("resm:AvaloniaGif.Demo.Images.monster.gif"),
                new Uri("resm:AvaloniaGif.Demo.Images.newton-cradle.gif"),
                new Uri("http://sprites.pokecheck.org/i/491.gif")
            };
        }

        public void DisplaySelectedGif()
        {
            
            CurrentGif = SelectedGif;
        }

        private ObservableCollection<Uri> _availableGifs;
        public ObservableCollection<Uri> AvailableGifs
        {
            get => _availableGifs;
            set
            {
                _availableGifs = value;
                OnPropertyChanged(nameof(AvailableGifs));
            }
        }

        private Uri _selectedGif;
        public Uri SelectedGif
        {
            get => _selectedGif;
            set
            {
                _selectedGif = value;
                OnPropertyChanged(nameof(SelectedGif));
            }
        }

        private Uri _currentGif;
        public Uri CurrentGif
        {
            get => _currentGif;
            set
            {
                _currentGif = value;
                OnPropertyChanged(nameof(CurrentGif));
            }
        }
    }
}
