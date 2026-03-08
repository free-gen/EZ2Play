using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Linq;

namespace EZ2Play.App
{
    public class UIState
    {
        private readonly FrameworkElement _window;
        private DispatcherTimer _clockTimer;
        private string _appDisplayName;

        public UIState(FrameworkElement window)
        {
            _window = window;
        }

        public void InitializeTopRightInfo()
        {
            _appDisplayName = AppInfo.GetProductName();
            UpdateTopRightText();

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => UpdateTopRightText();
            _clockTimer.Start();
        }

        public void SetupLayoutMode()
        {
            var horizontalGrid = _window.FindName("BaseModeGrid") as Grid;
                
            if (horizontalGrid != null)
                horizontalGrid.Visibility = Visibility.Visible;
        }

        public void UpdateTopRightText()
        {
            var appNameTb = _window.FindName("TopRightAppNameText") as TextBlock;
            var timeTb = _window.FindName("TopRightTimeText") as TextBlock;
            if (appNameTb == null || timeTb == null) return;
            
            var time = DateTime.Now.ToString("HH:mm");
            appNameTb.Text = $"{_appDisplayName} Launcher";
            timeTb.Text = time;
        }

        public void ShowExitOverlay()
        {
            var itemsListBox = _window.FindName("ItemsListBox") as ListBox;
            var bottomPanel = _window.FindName("BottomPanel") as Border;
            var topInfoPanel = _window.FindName("TopInfoPanel") as Grid;
            var selectedGameTitle = _window.FindName("SelectedGameTitle") as TextBlock;

            var gameSourceCard = _window.FindName("GameSourceCard") as Border;

            var noShortcutsMessage = _window.FindName("NoShortcutsMessage") as TextBlock;
            
            if (itemsListBox != null) itemsListBox.Visibility = Visibility.Collapsed;
            if (bottomPanel != null) bottomPanel.Visibility = Visibility.Collapsed;
            if (topInfoPanel != null) topInfoPanel.Visibility = Visibility.Collapsed;
            if (selectedGameTitle != null) selectedGameTitle.Visibility = Visibility.Collapsed;

            if (gameSourceCard != null) gameSourceCard.Visibility = Visibility.Collapsed;

            if (noShortcutsMessage != null) noShortcutsMessage.Visibility = Visibility.Collapsed;

            var companyName = AppInfo.GetCompanyName();
            var companyNameRun = _window.FindName("CompanyNameRun") as System.Windows.Documents.Run;
            if (companyNameRun != null)
            {
                companyNameRun.Text = companyName;
            }

            var versionRun = _window.FindName("VersionRun") as System.Windows.Documents.Run;
            if (versionRun != null)
            {
                versionRun.Text = AppInfo.GetVersion(shortFormat: false);
            }

            var exitMessageText = _window.FindName("ExitMessageText") as TextBlock;
            if (exitMessageText != null)
            {
                exitMessageText.Visibility = Visibility.Visible;
                
                var animation = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(800),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                exitMessageText.BeginAnimation(UIElement.OpacityProperty, animation);
            }
        }

        // Запускает последовательность сплеша внутри окна: лого → вызов onComplete.
        public void RunSplashSequence(Action onComplete)
        {
            var splashLogo = _window.FindName("SplashLogo") as Image;
            if (splashLogo == null) { onComplete?.Invoke(); return; }

            TryLoadLogoInto(splashLogo);

            void AnimateIn(UIElement el, int durationMs, Action after)
            {
                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                anim.Completed += (s, e) => after?.Invoke();
                el.BeginAnimation(UIElement.OpacityProperty, anim);
            }

            void AnimateOut(UIElement el, int durationMs, Action after)
            {
                var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(durationMs))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
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
                AnimateIn(splashLogo, 1000, () =>
                    Delay(500, () =>
                        AnimateOut(splashLogo, 500, () =>
                            Delay(1000, () => onComplete?.Invoke()))))); // последовательность задержек для плавного перехода
        }

        public void ShowWithAnimation(bool skipSplash, Action onAfterSplash)
        {
            var baseGrid = _window.FindName("BaseModeGrid") as Grid;
            var splashOverlay = _window.FindName("SplashOverlay") as Grid;

            // Сразу спрятать базовый UI и сплеш
            if (baseGrid != null) baseGrid.Visibility = Visibility.Collapsed;
            if (splashOverlay != null) splashOverlay.Visibility = Visibility.Collapsed;

            _window.Visibility = Visibility.Visible;

            if (skipSplash)
            {
                _window.Opacity = 1.0;
                onAfterSplash?.Invoke();
                return;
            }

            _window.Opacity = 0.0;
            if (splashOverlay != null) splashOverlay.Visibility = Visibility.Visible;

            var animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(800),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, e) =>
            {
                RunSplashSequence(() =>
                {
                    if (splashOverlay != null) splashOverlay.Visibility = Visibility.Hidden;
                    onAfterSplash?.Invoke();
                });
            };

            _window.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        private static bool TryLoadLogoInto(Image image)
        {
            if (image == null) return false;
            try
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
                if (File.Exists(path))
                {
                    image.Source = new BitmapImage(new Uri(path));
                    RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                    return true;
                }
            }
            catch { }

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                const string resourceName = "EZ2Play.Assets.logo.png";
                using (var stream = asm.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        image.Source = bitmap;
                        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.Fant);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        // Переключает иконки подсказок: геймпад (ButtonA/B/X) или клавиатура (KeyEnter/Escape/X).
        public void RefreshHintIcons(bool isGamepad)
        {
            var visGamepad = isGamepad ? Visibility.Visible : Visibility.Collapsed;
            var visKeyboard = isGamepad ? Visibility.Collapsed : Visibility.Visible;

            if (_window.FindName("LaunchIconGamepad") is FrameworkElement launchGp) launchGp.Visibility = visGamepad;
            if (_window.FindName("LaunchIconKeyboard") is FrameworkElement launchKb) launchKb.Visibility = visKeyboard;
            if (_window.FindName("ExitIconGamepad") is FrameworkElement exitGp) exitGp.Visibility = visGamepad;
            if (_window.FindName("ExitIconKeyboard") is FrameworkElement exitKb) exitKb.Visibility = visKeyboard;
            if (_window.FindName("ScreenSwapIconGamepad") is FrameworkElement swapGp) swapGp.Visibility = visGamepad;
            if (_window.FindName("ScreenSwapIconKeyboard") is FrameworkElement swapKb) swapKb.Visibility = visKeyboard;
        }

        public void Dispose()
        {
            try
            {
                _clockTimer?.Stop();
            }
            catch { }
        }
    }
}