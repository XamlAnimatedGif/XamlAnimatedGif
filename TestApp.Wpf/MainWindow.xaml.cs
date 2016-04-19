using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using XamlAnimatedGif;

namespace TestApp.Wpf
{
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
                          "images/newton-cradle.gif",
                          "images/not-a-gif.png",
                          "images/optimized-full-code-table.gif",
                          "http://i.imgur.com/rCK6xzh.gif",
                          "http://media.giphy.com/media/zW2pe7UscHiq4/giphy.gif",
                          "http://media.giphy.com/media/nWn6ko2ygIeEU/giphy.gif"
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
            }
        }

        private void AnimationBehavior_OnLoaded(object sender, RoutedEventArgs e)
        {
            IsDownloading = false;

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

        private bool _isDownloading;
        public bool IsDownloading
        {
            get { return _isDownloading; }
            set
            {
                _isDownloading = value;
                OnPropertyChanged("IsDownloading");
            }
        }

        private int _downloadProgress;
        public int DownloadProgress
        {
            get { return _downloadProgress; }
            set
            {
                _downloadProgress = value;
                OnPropertyChanged("DownloadProgress");
            }
        }

        private bool _isDownloadProgressIndeterminate;
        public bool IsDownloadProgressIndeterminate
        {
            get { return _isDownloadProgressIndeterminate; }
            set
            {
                _isDownloadProgressIndeterminate = value;
                OnPropertyChanged("IsDownloadProgressIndeterminate");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private Animator _animator;

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            _animator?.Pause();
            SetPlayPauseEnabled(true);
        }

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            _animator?.Play();
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
            btnRewind.IsEnabled = true;
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
            GC.WaitForPendingFinalizers();
        }

        private void btnBasicTests_Click(object sender, RoutedEventArgs e)
        {
            new BasicTestsWindow().ShowDialog();
        }

        private void AnimationBehavior_OnError(DependencyObject d, AnimationErrorEventArgs e)
        {
            if (e.Kind == AnimationErrorKind.Loading)
                IsDownloading = false;

            MessageBox.Show($"An error occurred ({e.Kind}): {e.Exception}");
        }

        private void btnRewind_Click(object sender, RoutedEventArgs e)
        {
            if (_animator == null)
                return;
            _animator.Rewind();
            SetPlayPauseEnabled(_animator.IsPaused || _animator.IsComplete);
            Completed = false;
        }

        private void AnimationBehavior_OnDownloadProgress(DependencyObject d, DownloadProgressEventArgs e)
        {
            IsDownloading = true;
            if (e.Progress >= 0)
            {
                DownloadProgress = e.Progress;
                IsDownloadProgressIndeterminate = false;
            }
            else
            {
                IsDownloadProgressIndeterminate = true;
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            SelectedImage = null;
        }
    }
}
