using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using XamlAnimatedGif;
using XamlAnimatedGif.Decoding;
using XamlAnimatedGif.Decompression;
using System;

namespace TestApp.Wpf
{
    public partial class BasicTestsWindow
    {
        public BasicTestsWindow()
        {
            InitializeComponent();
        }

        private void BtnBrowse_OnClick(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "GIF files|*.gif" };
            if (dlg.ShowDialog() == true)
            {
                txtFileName.Text = dlg.FileName;
            }
        }

        private async void BtnTestLzw_OnClick(object sender, RoutedEventArgs e)
        {
            string fileName = txtFileName.Text;
            if (string.IsNullOrEmpty(fileName))
                return;

            btnTestLzw.IsEnabled = false;
            try
            {
                await DecompressAllFramesAsync(fileName);
            }
            finally
            {
                btnTestLzw.IsEnabled = true;
            }
        }

        private void BtnTestStream_OnClick(object sender, RoutedEventArgs e)
        {
            string fileName = txtFileName.Text;
            if (string.IsNullOrEmpty(fileName))
                return;

            var img = new Image { Stretch = Stretch.None };
            var wnd = new Window { Content = img };

            using (var fileStream = File.OpenRead(fileName))
            {
                AnimationBehavior.SetSourceStream(img, fileStream);
                wnd.ShowDialog();
                wnd.Close();
                AnimationBehavior.SetSourceStream(img, null);
            }
        }

        private static async Task DecompressAllFramesAsync(string path)
        {
            using (var fileStream = File.OpenRead(path))
            {
                var gif = await GifDataStream.ReadAsync(fileStream);
                for (int i = 0; i < gif.Frames.Count; i++)
                {
                    var frame = gif.Frames[i];
                    fileStream.Seek(frame.ImageData.CompressedDataStartOffset, SeekOrigin.Begin);
                    using (var ms = new MemoryStream())
                    {
                        await GifHelpers.CopyDataBlocksToStreamAsync(fileStream, ms);
                        using (var lzwStream = new LzwDecompressStream(ms.GetBuffer(), frame.ImageData.LzwMinimumCodeSize))
                        using (var indOutStream = File.OpenWrite($"{path}.{i}.ind"))
                        {
                            await lzwStream.CopyToAsync(indOutStream);
                        }
                    }
                }
            }
        }

        private async void BtnTestImageBrush_OnClick(object sender, RoutedEventArgs e)
        {
            string fileName = txtFileName.Text;
            if (string.IsNullOrEmpty(fileName))
                return;

            using (var anim = await Animator.CreateAsync(null, new Uri(fileName)))
            {
                anim.Play();
                var img = new ImageBrush(anim.Bitmap);
                var wnd = new Window { Background = img };
                wnd.ShowDialog();
            }
        }
    }
}
