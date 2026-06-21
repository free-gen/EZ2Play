using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EZ2Play.App
{
    public class SplashScreen
    {
        private readonly Image _logoImage;
        private readonly Grid _splashOverlay;
        private readonly Grid _mainScreenGrid;

        public SplashScreen(Image logoImage, Grid splashOverlay, Grid mainScreenGrid)
        {
            _logoImage = logoImage;
            _splashOverlay = splashOverlay;
            _mainScreenGrid = mainScreenGrid;
        }

        // Запускает последовательность сплеш-экрана: лого → вызов onComplete
        public void RunSequence(Action onComplete)
        {
            if (_logoImage == null)
            {
                onComplete?.Invoke();
                return;
            }

            TryLoadLogoInto(_logoImage);

            void AnimateIn(UIElement el, int durationMs, Action after)
            {
                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                anim.Completed += (s, e) => after?.Invoke();
                el.BeginAnimation(UIElement.OpacityProperty, anim);
            }

            void AnimateOut(UIElement el, int durationMs, Action after)
            {
                var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(durationMs))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                anim.Completed += (s, e) => after?.Invoke();
                el.BeginAnimation(UIElement.OpacityProperty, anim);
            }

            void Delay(int ms, Action after)
            {
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
                t.Tick += (s, e) => { t.Stop(); after?.Invoke(); };
                t.Start();
            }

            Delay(500, () =>
                AnimateIn(_logoImage, 1000, () =>
                    Delay(500, () =>
                        AnimateOut(_logoImage, 500, () =>
                            Delay(1000, () => onComplete?.Invoke())))));
        }

        // Показывает окно с анимацией, опционально пропуская сплеш
        public void ShowWithAnimation(bool skipSplash, Action onAfterSplash)
        {
            if (_mainScreenGrid != null)
                _mainScreenGrid.Visibility = Visibility.Collapsed;
            
            if (_splashOverlay != null)
                _splashOverlay.Visibility = Visibility.Collapsed;

            var window = Application.Current?.MainWindow;
            if (window != null)
                window.Visibility = Visibility.Visible;

            if (skipSplash)
            {
                if (window != null)
                    window.Opacity = 1.0;
                onAfterSplash?.Invoke();
                return;
            }

            if (window != null)
                window.Opacity = 0.0;

            if (_splashOverlay != null)
                _splashOverlay.Visibility = Visibility.Visible;

            var animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(800),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, e) =>
            {
                RunSequence(() =>
                {
                    if (_splashOverlay != null)
                        _splashOverlay.Visibility = Visibility.Hidden;
                    onAfterSplash?.Invoke();
                });
            };

            if (window != null)
                window.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        // Пытается загрузить логотип в указанный Image элемент
        private static bool TryLoadLogoInto(Image image)
        {
            if (image == null) return false;

            try
            {
                var logoFromPack = PackLoader.LoadFromPack("Logo.png");
                if (logoFromPack != null)
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = logoFromPack;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    image.Source = bitmap;
                    RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                    return true;
                }
            }
            catch { }

            try
            {
                var uri = new Uri("pack://application:,,,/Assets/Logo.png", UriKind.Absolute);
                var bitmap = new BitmapImage(uri);
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                image.Source = bitmap;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}