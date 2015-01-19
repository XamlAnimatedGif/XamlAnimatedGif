using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
#if WINDOWS_PHONE_APP
using System.Linq;
using Windows.ApplicationModel.Activation;
#endif

namespace TestApp.WinRT
{
    public sealed partial class GifTestPage : INotifyPropertyChanged
    {
        public GifTestPage()
        {
            this.InitializeComponent();
            _images = new ObservableCollection<string>
                      {
                          "http://media.giphy.com/media/nWn6ko2ygIeEU/giphy.gif",
                          "http://media.giphy.com/media/zW2pe7UscHiq4/giphy.gif",
                          "ms-appx:///images/working.gif",
                          "ms-appx:///images/earth.gif",
                          "ms-appx:///images/radar.gif",
                          "ms-appx:///images/bomb.gif",
                          "ms-appx:///images/bomb-once.gif",
                          "ms-appx:///images/nonanimated.gif",
                          "ms-appx:///images/monster.gif",
                          "ms-appx:///images/partialfirstframe.gif"
                      };
            DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        private ObservableCollection<string> _images;
        public ObservableCollection<string> Images
        {
            get { return _images; }
            set
            {
                _images = value;
                OnPropertyChanged();
            }
        }


        private string _selectedImage;
        public string SelectedImage
        {
            get { return _selectedImage; }
            set
            {
                _selectedImage = value;
                OnPropertyChanged();
            }
        }

#if WINDOWS_PHONE_APP
        private void BtnBrowse_OnClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".gif");
            picker.ContinuationData["context"] = "addGifImage";
            picker.PickSingleFileAndContinue();
        }

        public void PickFileContinuation(FileOpenPickerContinuationEventArgs args)
        {
            var file = args.Files.FirstOrDefault();
            if (file == null)
                return;

            StorageApplicationPermissions.FutureAccessList.Add(file);
            string uriString = new Uri(file.Path).AbsoluteUri;
            Images.Add(uriString);
            SelectedImage = uriString;
        }
#else
        private async void BtnBrowse_OnClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".gif");
            var file = await picker.PickSingleFileAsync();
            StorageApplicationPermissions.FutureAccessList.Add(file);
            string uriString = new Uri(file.Path).AbsoluteUri;
            Images.Add(uriString);
            SelectedImage = uriString;
        }
#endif
    }
}
