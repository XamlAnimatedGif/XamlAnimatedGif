using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using XamlAnimatedGif;

namespace TestApp.Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();
            _images = new ObservableCollection<string>
                      {
                          "images/working.gif",
                          "images/earth.gif",
                          "images/radar.gif",
                          "images/bomb.gif",
                          "images/bomb-once.gif",
                          "images/nonanimated.gif",
                          "images/monster.gif",
                          "pack://siteoforigin:,,,/images/siteoforigin.gif",
                          "images/partialfirstframe.gif",
                          //"http://i.imgur.com/rCK6xzh.gif"
                      };
            DataContext = this;
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog {Filter = "GIF images|*.gif"};
            if (dlg.ShowDialog() == true)
            {
                Images.Add(dlg.FileName);
                SelectedImage = dlg.FileName;
            }
        }

        private ObservableCollection<string> _images;
        public ObservableCollection<string> Images
        {
            get { return _images; }
            set
            {
                _images = value;
                OnPropertyChanged("Images");
            }
        }

        private string _selectedImage;
        public string SelectedImage
        {
            get { return _selectedImage; }
            set
            {
                _selectedImage = value;
                OnPropertyChanged("SelectedImage");
                Completed = false;
                Dispatcher.BeginInvoke(ImageChanged, DispatcherPriority.Background);
            }
        }

        private void ImageChanged()
        {
            if (_animator != null)
            {
                _animator.CurrentFrameChanged -= CurrentFrameChanged;
                _animator.AnimationCompleted -= AnimationCompleted;
            }

            _animator = AnimationBehavior.GetAnimator(img);

            if (_animator != null)
            {
                _animator.CurrentFrameChanged += CurrentFrameChanged;
                _animator.AnimationCompleted += AnimationCompleted;
                sldPosition.Value = 0;
                sldPosition.Maximum = _animator.FrameCount - 1;
                SetPlayPauseEnabled(_animator.IsPaused || _animator.IsComplete);
            }
        }

        private void CurrentFrameChanged(object sender, EventArgs e)
        {
            if (_animator != null)
            {
                sldPosition.Value = _animator.CurrentFrameIndex;
            }
        }

        private void AnimationCompleted(object sender, EventArgs e)
        {
            Completed = true;
            if (_animator != null)
                SetPlayPauseEnabled(_animator.IsPaused || _animator.IsComplete);
        }

        private bool _useDefaultRepeatBehavior = true;
        public bool UseDefaultRepeatBehavior
        {
            get { return _useDefaultRepeatBehavior; }
            set
            {
                _useDefaultRepeatBehavior = value;
                OnPropertyChanged("UseDefaultRepeatBehavior");
                if (value)
                    RepeatBehavior = default(RepeatBehavior);
            }
        }


        private bool _repeatForever;
        public bool RepeatForever
        {
            get { return _repeatForever; }
            set
            {
                _repeatForever = value;
                OnPropertyChanged("RepeatForever");
                if (value)
                    RepeatBehavior = RepeatBehavior.Forever;
            }
        }


        private bool _useSpecificRepeatCount;
        public bool UseSpecificRepeatCount
        {
            get { return _useSpecificRepeatCount; }
            set
            {
                _useSpecificRepeatCount = value;
                OnPropertyChanged("UseSpecificRepeatCount");
                if (value)
                    RepeatBehavior = new RepeatBehavior(RepeatCount);
            }
        }

        private int _repeatCount = 3;
        public int RepeatCount
        {
            get { return _repeatCount; }
            set
            {
                _repeatCount = value;
                OnPropertyChanged("RepeatCount");
                if (UseSpecificRepeatCount)
                    RepeatBehavior = new RepeatBehavior(value);
            }
        }

        private bool _completed;
        public bool Completed
        {
            get { return _completed; }
            set
            {
                _completed = value;
                OnPropertyChanged("Completed");
            }
        }

        private RepeatBehavior _repeatBehavior;
        public RepeatBehavior RepeatBehavior
        {
            get { return _repeatBehavior; }
            set
            {
                _repeatBehavior = value;
                OnPropertyChanged("RepeatBehavior");
                Completed = false;
                Dispatcher.BeginInvoke(ImageChanged, DispatcherPriority.Background);
            }
        }

        private bool _autoStart = true;
        public bool AutoStart
        {
            get { return _autoStart; }
            set
            {
                _autoStart = value;
                OnPropertyChanged("AutoStart");
            }
        }
        

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        private Animator _animator;

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (_animator != null)
                _animator.Pause();
            SetPlayPauseEnabled(true);
        }

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (_animator != null)
                _animator.Play();
            Completed = false;
            SetPlayPauseEnabled(false);
        }

        private void sldPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Not supported (yet?)

            //if (_animator != null)
            //{
            //    var currentFrame = _animator.CurrentFrameIndex;
            //    if (currentFrame >= 0 && currentFrame != (int)sldPosition.Value)
            //        _animator.GotoFrame((int)sldPosition.Value);
            //}
        }

        private void SetPlayPauseEnabled(bool isPaused)
        {
            btnPause.IsEnabled = !isPaused;
            btnPlay.IsEnabled = isPaused;
        }

        private void btnOpenUrl_Click(object sender, RoutedEventArgs e)
        {
            string url = Interaction.InputBox("Enter the URL of the image to load", "Enter URL");
            if (!string.IsNullOrEmpty(url))
            {
                Images.Add(url);
                SelectedImage = url;
            }
        }

        private void btnGC_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();
        }

        private void btnBasicTests_Click(object sender, RoutedEventArgs e)
        {
            new BasicTestsWindow().ShowDialog();
        }
    }
}
