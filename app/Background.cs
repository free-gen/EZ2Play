using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace EZ2Play.App
{
    public class Background
    {
        private ImageSource _customBackgroundImage;
        private ImageSource _desktopBackgroundImage;
        private bool _useCustomBackground;

        public Background()
        {
            _useCustomBackground = EZ2Play.Main.App.UseCustomBackground;

            LoadCustomBackgroundImage();
            LoadDesktopBackground();
        }

        /// Обновляем фон
        public void UpdateBackground(ImageBrush backgroundBrush, Border listBoxBlurredBackground)
        {
            // Если пользовательский фон включен и загружен — используем его
            if (_useCustomBackground && _customBackgroundImage != null)
            {
                backgroundBrush.ImageSource = _customBackgroundImage;

                if (listBoxBlurredBackground != null)
                {
                    // Показываем размытый фон под ListBox
                    var blurredBrush = new ImageBrush(_customBackgroundImage)
                    {
                        Stretch = backgroundBrush.Stretch
                    };
                    listBoxBlurredBackground.Background = blurredBrush;
                    listBoxBlurredBackground.Visibility = Visibility.Visible;
                }
            }
            else if (_desktopBackgroundImage != null)
            {
                // Иначе используем размытый фон рабочего стола
                backgroundBrush.ImageSource = _desktopBackgroundImage;

                if (listBoxBlurredBackground != null)
                {
                    var blurredBrush = new ImageBrush(_desktopBackgroundImage)
                    {
                        Stretch = backgroundBrush.Stretch
                    };
                    listBoxBlurredBackground.Background = blurredBrush;
                    listBoxBlurredBackground.Visibility = Visibility.Visible;
                }
            }
            else
            {
                // Если нет ничего — скрываем размытый фон
                if (listBoxBlurredBackground != null)
                    listBoxBlurredBackground.Visibility = Visibility.Collapsed;
            }
        }

        /// Загружаем пользовательский фон (bg.jpg/bg.png)
        private void LoadCustomBackgroundImage()
        {
            if (!_useCustomBackground)
                return;

            string[] possibleFiles = { "bg.jpg", "bg.png" };
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;

            foreach (string fileName in possibleFiles)
            {
                string fullPath = Path.Combine(appDirectory, fileName);
                if (File.Exists(fullPath))
                {
                    _customBackgroundImage = new BitmapImage(new Uri(fullPath));
                    break;
                }
            }
        }

        /// Получаем и размываем фон рабочего стола
        private void LoadDesktopBackground()
        {
            try
            {
                // Получаем путь к рабочему столу из реестра
                string desktopPath = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Control Panel\Desktop", 
                    "WallPaper", 
                    null) as string;

                if (!string.IsNullOrEmpty(desktopPath) && File.Exists(desktopPath))
                {
                    var bitmap = new BitmapImage(new Uri(desktopPath));
                    _desktopBackgroundImage = CreateBlurredImage(bitmap);
                }
            }
            catch
            {
                _desktopBackgroundImage = null;
            }
        }

        /// Создает размытое изображение
       private ImageSource CreateBlurredImage(ImageSource originalImage)
        {
            int targetWidth = 400;
            int targetHeight = (int)(originalImage.Height / originalImage.Width * targetWidth);

            var scaledBitmap = new TransformedBitmap((BitmapSource)originalImage,
                new ScaleTransform((double)targetWidth / originalImage.Width, (double)targetHeight / originalImage.Height));

            var renderTarget = new RenderTargetBitmap(
                targetWidth, targetHeight,
                96, 96, PixelFormats.Pbgra32);

            var visual = new System.Windows.Controls.Image
            {
                Source = scaledBitmap,
                Effect = new BlurEffect { Radius = 10 }
            };

            visual.Measure(new Size(targetWidth, targetHeight));
            visual.Arrange(new Rect(0, 0, targetWidth, targetHeight));

            renderTarget.Render(visual);

            // Растягиваем до размера окна через ImageBrush (не BitmapSource)
            return renderTarget;
        }
    }
}
