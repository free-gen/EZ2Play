using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace EZ2Play.App
{
    public class Background
    {
        private ImageSource _customBackgroundImage;
        private bool _useCustomBackground;

        public Background()
        {
            _useCustomBackground = EZ2Play.Main.App.UseCustomBackground;
            LoadCustomBackgroundImage();
        }

        public void UpdateBackground(ImageBrush backgroundBrush, Border listBoxBlurredBackground, 
            ImageBrush blurredListBackgroundBrush, ShortcutInfo[] shortcuts, int selectedIndex)
        {
            // Если используется пользовательский фон, устанавливаем его
            if (_useCustomBackground && _customBackgroundImage != null)
            {
                backgroundBrush.ImageSource = _customBackgroundImage;
                
                // Показываем размытый фон под ListBox и устанавливаем тот же источник
                if (listBoxBlurredBackground != null && blurredListBackgroundBrush != null)
                {
                    blurredListBackgroundBrush.ImageSource = _customBackgroundImage;
                    listBoxBlurredBackground.Visibility = Visibility.Visible;
                }
            }
            // Иначе используем динамический фон от иконки ярлыка
            else if (selectedIndex >= 0 && selectedIndex < shortcuts.Length)
            {
                var selectedShortcut = shortcuts[selectedIndex];
                if (selectedShortcut.Icon != null)
                {
                    var blurredImage = CreateBlurredImage(selectedShortcut.Icon);
                    backgroundBrush.ImageSource = blurredImage;
                }
                
                // Скрываем размытый фон под ListBox для динамического фона
                if (listBoxBlurredBackground != null)
                {
                    listBoxBlurredBackground.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // Если нет выбранного ярлыка, но есть кастомный фон, используем его
                if (_useCustomBackground && _customBackgroundImage != null)
                {
                    backgroundBrush.ImageSource = _customBackgroundImage;
                    
                    // Показываем размытый фон под ListBox
                    if (listBoxBlurredBackground != null && blurredListBackgroundBrush != null)
                    {
                        blurredListBackgroundBrush.ImageSource = _customBackgroundImage;
                        listBoxBlurredBackground.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    // Скрываем размытый фон если нет фона
                    if (listBoxBlurredBackground != null)
                    {
                        listBoxBlurredBackground.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private ImageSource CreateBlurredImage(ImageSource originalImage)
        {
            var renderTarget = new RenderTargetBitmap(
                (int)originalImage.Width,
                (int)originalImage.Height,
                96, 96, PixelFormats.Pbgra32);

            var visual = new System.Windows.Controls.Image
            {
                Source = originalImage,
                Effect = new BlurEffect { Radius = 20 }
            };

            visual.Measure(new Size(originalImage.Width, originalImage.Height));
            visual.Arrange(new Rect(0, 0, originalImage.Width, originalImage.Height));

            renderTarget.Render(visual);
            return renderTarget;
        }

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
    }
} 