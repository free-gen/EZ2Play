using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Reflection;

namespace EZ2Play.App
{
    public class SplashScreen : Window
    {
        [DllImport("user32.dll")]
        private static extern bool ShowCursor(bool bShow);

        private TextBlock _SplashAppName;
        private TextBlock _SplashAppSlogan;
        private System.Windows.Controls.Image _SplashLogoImage;
        private FontFamily _fontFamily = new FontFamily("Segoe UI");
        private bool _isAnimationComplete = false;

        public event EventHandler SplashCompleted;

        public SplashScreen()
        {
            InitializeWindow();
            CreateUI();
            
            Activated += (s, e) => HideCursor();
            Deactivated += (s, e) => ShowCursor();
        }

        private void InitializeWindow()
        {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
        }

        private void CreateUI()
        {
            var grid = new Grid();
            
            var backgroundBorder = new Border
            {
                Background = Brushes.Black
            };
            grid.Children.Add(backgroundBorder);

            var textContainer = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Создаем лого - либо изображение, либо текст
            if (TryLoadLogoImage())
            {
                textContainer.Children.Add(_SplashLogoImage);
            }
            else
            {
                _SplashAppName = new TextBlock
                {
                    Text = EZ2Play.Main.App.CustomLogo ?? EZ2Play.App.AppInfo.GetProductName(),
                    FontSize = 120,
                    FontFamily = _fontFamily,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0,
                };
                textContainer.Children.Add(_SplashAppName);
            }

            _SplashAppSlogan = new TextBlock
            {
                Text = EZ2Play.Main.App.CustomSlogan ?? EZ2Play.App.AppInfo.GetProductSlogan(),
                FontSize = 80,
                FontFamily = _fontFamily,
                FontWeight = FontWeights.Light,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0
            };

            textContainer.Children.Add(_SplashAppSlogan);
            grid.Children.Add(textContainer);

            Content = grid;
        }

        private bool TryLoadLogoImage()
        {
            try
            {
                string logoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    _SplashLogoImage = new System.Windows.Controls.Image
                    {
                        Source = new BitmapImage(new Uri(logoPath)),
                        MaxHeight = 200, // Ограничиваем размер изображения
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Opacity = 0
                    };
                    RenderOptions.SetBitmapScalingMode(_SplashLogoImage, BitmapScalingMode.HighQuality);
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Логирование ошибки, если нужно
                System.Diagnostics.Debug.WriteLine($"Error loading logo.png: {ex.Message}");
            }
            return false;
        }

        public void PlaySplashAnimation()
        {
            if (_isAnimationComplete) return;

            // Обновляем слоган перед показом анимации
            UpdateSlogan();
            
            Show();
            StartAnimationSequence();
        }

        private void UpdateSlogan()
        {
            if (_SplashAppSlogan != null)
            {
                var newSlogan = EZ2Play.Main.App.CustomSlogan ?? EZ2Play.App.AppInfo.GetProductSlogan();
                _SplashAppSlogan.Text = newSlogan;
            }
        }

        private async void StartAnimationSequence()
        {
            // Начальная задержка перед появлением первого текста
            // Пользователь видит только черный фон перед началом анимации
            await Task.Delay(500);

            // Анимация появления названия приложения (текст или изображение)
            // Элемент плавно появляется из прозрачного состояния до полной видимости
            var logoElement = _SplashLogoImage ?? (UIElement)_SplashAppName;
            AnimateTextAppearance(logoElement, 1.0, 1000, () =>
            {
                // Пауза после появления названия приложения
                // Пользователь видит элемент перед его исчезновением
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                timer.Tick += (s1, e1) =>
                {
                    timer.Stop();
                    // Анимация исчезновения названия приложения
                    // Элемент плавно исчезает до полной прозрачности
                    AnimateTextDisappearance(logoElement, 500, () =>
                    {
                        // Пауза между исчезновением названия приложения и появлением "Simple way to game"
                        // Экран остается пустым между текстами
                        var pauseTimer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(500)
                        };
                        pauseTimer.Tick += (s2, e2) =>
                        {
                            pauseTimer.Stop();
                            // Анимация появления "Simple way to game"
                            // Текст плавно появляется из прозрачного состояния до полной видимости
                            AnimateTextAppearance(_SplashAppSlogan, 1.0, 1000, () =>
                            {
                                // Пауза после появления "Simple way to game"
                                // Пользователь читает текст перед его исчезновением
                                var timer2 = new DispatcherTimer
                                {
                                    Interval = TimeSpan.FromMilliseconds(500)
                                };
                                timer2.Tick += (s3, e3) =>
                                {
                                    timer2.Stop();
                                    // Анимация исчезновения "Simple way to game"
                                    // Текст плавно исчезает до полной прозрачности
                                    AnimateTextDisappearance(_SplashAppSlogan, 500, () =>
                                    {
                                        _isAnimationComplete = true;
                                        SplashCompleted?.Invoke(this, EventArgs.Empty);
                                    });
                                };
                                timer2.Start();
                            });
                        };
                        pauseTimer.Start();
                    });
                };
                timer.Start();
            });
        }

        private void AnimateTextAppearance(UIElement element, double targetOpacity, int durationMs, Action onCompleted = null)
        {
            var animation = new DoubleAnimation
            {
                From = 0.0,
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            if (onCompleted != null)
            {
                animation.Completed += (s, e) => onCompleted();
            }

            element.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        private void AnimateTextDisappearance(UIElement element, int durationMs, Action onCompleted = null)
        {
            var animation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            if (onCompleted != null)
            {
                animation.Completed += (s, e) => onCompleted();
            }

            element.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        public void CloseSplash()
        {
            Close();
        }

        private void HideCursor()
        {
            ShowCursor(false);
        }

        private void ShowCursor()
        {
            ShowCursor(true);
        }

        private Color GetWindowsAccentColor()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
            {
                if (key != null)
                {
                    var accentColor = key.GetValue("AccentColor");
                    if (accentColor != null)
                    {
                        int colorValue = (int)accentColor;
                        return Color.FromArgb(
                            255,
                            (byte)(colorValue & 0xFF),
                            (byte)((colorValue >> 8) & 0xFF),
                            (byte)((colorValue >> 16) & 0xFF)
                        );
                    }
                }
            }
            
            return Colors.DodgerBlue;
        }
    }
} 