using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace CherryTomato
{
    internal sealed class App
    {
        private readonly Window window;
        private readonly Button pick, encrypt, decrypt, clear, openFolder;
        private readonly TextBlock fileName, empty, status, sizeLabel;
        private readonly Image preview;
        private string inputPath, outputPath;
        private bool busy;

        [STAThread]
        private static void Main()
        {
            var application = new Application();
            var ui = new App();
            application.Run(ui.window);
        }

        private App()
        {
            using (Stream layout = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml"))
                window = (Window)XamlReader.Load(layout);
            pick = (Button)window.FindName("Pick");
            encrypt = (Button)window.FindName("Encrypt");
            decrypt = (Button)window.FindName("Decrypt");
            clear = (Button)window.FindName("Clear");
            openFolder = (Button)window.FindName("OpenFolder");
            fileName = (TextBlock)window.FindName("FileName");
            empty = (TextBlock)window.FindName("Empty");
            status = (TextBlock)window.FindName("Status");
            sizeLabel = (TextBlock)window.FindName("SizeLabel");
            preview = (Image)window.FindName("Preview");
            pick.Click += delegate { PickImage(); };
            encrypt.Click += async delegate { await Run(true); };
            decrypt.Click += async delegate { await Run(false); };
            clear.Click += delegate { ClearPreview(); RefreshButtons(); };
            openFolder.Click += delegate { ShowOutput(); };
            window.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) {
                if (busy) { e.Cancel = true; status.Text = "正在处理并保存，完成后即可关闭"; }
            };
        }

        private void PickImage()
        {
            var dialog = new OpenFileDialog {
                Title = "选择要处理的图片",
                Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(window) != true) return;
            SetInput(dialog.FileName);
        }

        private void SetInput(string path)
        {
            inputPath = path;
            fileName.Text = Path.GetFileName(inputPath);
            fileName.ToolTip = inputPath;
            outputPath = null;
            ClearPreview();
            status.Text = "选择加密或解密";
            status.ToolTip = null;
            RefreshButtons();
        }

        private void ClearPreview()
        {
            preview.Source = null;
            empty.Visibility = Visibility.Visible;
            sizeLabel.Text = "";
        }

        private void RefreshButtons()
        {
            pick.IsEnabled = !busy;
            encrypt.IsEnabled = decrypt.IsEnabled = !busy && inputPath != null;
            clear.IsEnabled = !busy && preview.Source != null;
            openFolder.IsEnabled = !busy && outputPath != null;
        }

        private async Task Run(bool scramble)
        {
            if (busy || inputPath == null) return;
            busy = true;
            RefreshButtons();
            string action = scramble ? "加密" : "解密";
            status.Text = "正在" + action + "并保存…";
            status.ToolTip = null;
            try
            {
                string source = inputPath;
                var result = await Task.Run(() => {
                    BitmapSource bitmap = PixelScrambler.ProcessFile(source, scramble);
                    return Tuple.Create(bitmap, PixelScrambler.SaveResult(bitmap, source, scramble));
                });
                outputPath = result.Item2;
                preview.Source = result.Item1;
                empty.Visibility = Visibility.Collapsed;
                sizeLabel.Text = result.Item1.PixelWidth + " × " + result.Item1.PixelHeight;
                status.Text = "已保存：" + Path.GetFileName(outputPath);
                status.ToolTip = outputPath;
            }
            catch (Exception ex)
            {
                outputPath = null;
                ClearPreview();
                status.Text = action + "失败";
                MessageBox.Show(window, ex.Message, status.Text, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { busy = false; RefreshButtons(); }
        }

        private void ShowOutput()
        {
            if (outputPath == null) return;
            try
            {
                Process.Start("explorer.exe", "/select,\"" + outputPath + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(window, ex.Message, "无法打开输出文件夹", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
