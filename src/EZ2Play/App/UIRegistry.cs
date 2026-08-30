using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace EZ2Play.App
{
    public class UIRegistry
    {
        public Image SplashLogo { get; set; }
        public Grid SplashOverlay { get; set; }
        public TextBlock NoShortcutsMessage { get; set; }
        public TextBlock ExitMessageText { get; set; }

        public Grid TopPanel { get; set; }

        public TextBlock TabGamelistText { get; set; }
        public TextBlock TabLastPlayedText { get; set; }

        public Border NotificationPanel { get; set; }
        public TextBlock NotificationIcon { get; set; }
        public TextBlock NotificationText { get; set; }
        public Notifications Notifications { get; private set; }

        public Image UserAvatar { get; set; }
        public TextBlock TimeLabel { get; set; }

        public Border GameSourceCard { get; set; }
        public Grid MainScreenGrid { get; set; }
        public TextBlock SelectedGameTitle { get; set; }
        public Border GameCounterCard { get; set; }
        public TextBlock GameCounterText { get; set; }
        public ListBox ItemsListBox { get; set; }
        public Grid CarouselWrapper { get; set; }
        public Grid BackgroundViewport { get; set; }
        public Image BackgroundPreviousImage { get; set; }
        public Image BackgroundImage { get; set; }

        public HintPanel BottomHintPanel { get; set; }

        private bool UseImageBackground => BackgroundImage?.Source != null;
        private SplashScreen _splash;
        private ParticlesCanvas _particlesCanvas;
        private Wpf.Ui.Controls.ProgressRing _loadingRing;

        private const double BackgroundStartPosition = 0.15;
        private const double BackgroundPanSpeed = 5.0;
        private const double BackgroundPanEdgeZone = 60.0;
        private const double BackgroundTransitionDuration = 0.25;

        private double _backgroundPanOverflow;
        private double _backgroundPanPosition;
        private double _backgroundPanDirection = 1;
        private TimeSpan _backgroundPanLastRenderTime;

        private TranslateTransform BackgroundTranslate => BackgroundImage?.RenderTransform as TranslateTransform;
        private TranslateTransform BackgroundPreviousTranslate => BackgroundPreviousImage?.RenderTransform as TranslateTransform;

        public UIRegistry()
        {
            Notifications = new Notifications(this);
        }

        public void InitializeSplash(Image logo, Grid overlay, Grid mainScreen)
        {
            _splash = new SplashScreen(logo, overlay, mainScreen);
        }

        public void InitializeNotifications(Border NotificationPanel, TextBlock NotificationIcon, TextBlock NotificationText)
        {
            Notifications.Initialize(NotificationPanel, NotificationIcon, NotificationText);
        }

        public void InitializeClock()
        {
            UpdateClockDisplay();

            SystemProvider.StartClock((time) =>
            {
                if (TimeLabel != null)
                    TimeLabel.Text = time;
            });
        }

        public void UpdateClockDisplay()
        {
            if (TimeLabel != null)
                TimeLabel.Text = SystemProvider.GetCurrentTime();
        }

        public void LoadUserAvatar()
        {
            if (UserAvatar == null) return;

            var avatar = SystemProvider.GetUserAvatar();

            if (avatar == null)
            {
                UserAvatar.Visibility = Visibility.Collapsed;
                return;
            }

            UserAvatar.Source = avatar;
            UserAvatar.Visibility = Visibility.Visible;

            // Keep the avatar clipped to a circle when its size changes.
            UserAvatar.Loaded += (s, e) => ClipAvatarToCircle();
            UserAvatar.SizeChanged += (s, e) => ClipAvatarToCircle();
        }

        private void ClipAvatarToCircle()
        {
            var r = UserAvatar.ActualWidth / 2;

            if (r > 0)
                UserAvatar.Clip = new EllipseGeometry(new Point(r, r), r, r);
        }

        public void SetEmptyState(bool isEmpty)
        {
            if (MainScreenGrid != null)
                MainScreenGrid.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;

            if (BottomHintPanel != null)
                BottomHintPanel.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;

            if (NoShortcutsMessage != null)
                NoShortcutsMessage.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ShowExitOverlay()
        {
            if (MainScreenGrid != null)
                MainScreenGrid.Visibility = Visibility.Collapsed;

            if (NoShortcutsMessage != null)
                NoShortcutsMessage.Visibility = Visibility.Collapsed;

            if (BottomHintPanel != null)
                BottomHintPanel.Visibility = Visibility.Collapsed;

            if (ExitMessageText != null)
            {
                ExitMessageText.Visibility = Visibility.Visible;

                var animation = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(800),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                ExitMessageText.BeginAnimation(UIElement.OpacityProperty, animation);
            }
        }

        public void RunSplashSequence(Action onComplete)
        {
            _splash?.RunSequence(onComplete);
        }

        public void ShowWithAnimation(bool skipSplash, Action onAfterSplash)
        {
            _splash?.ShowWithAnimation(skipSplash, onAfterSplash);
        }

        public void UpdatePlaytimeDisplay(string text, bool visible)
        {
            if (GameCounterText != null)
                GameCounterText.Text = text;

            if (GameCounterCard != null)
                GameCounterCard.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public void InitializeLoadingRing(Wpf.Ui.Controls.ProgressRing ring)
        {
            _loadingRing = ring;
        }

        public void ShowLoading(bool show)
        {
            if (_loadingRing != null)
                _loadingRing.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        public void RefreshHintIcons(bool isGamepad)
        {
            if (BottomHintPanel != null)
            {
                BottomHintPanel.Device = isGamepad
                    ? HintPanel.InputDevice.Gamepad
                    : HintPanel.InputDevice.Keyboard;
            }
        }

        public bool LoadBackgroundForShortcut(string shortcutPath)
        {
            if (BackgroundImage == null) return false;

            StopBackgroundPan();

            BackgroundImage.BeginAnimation(UIElement.OpacityProperty, null);
            BackgroundImage.Source = null;
            BackgroundImage.Visibility = Visibility.Collapsed;
            BackgroundImage.Opacity = 0;

            ClearPreviousBackground();

            var bitmap = LoadBackgroundBitmap(shortcutPath);

            if (bitmap == null)
                return false;

            BackgroundImage.Source = bitmap;

            RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.HighQuality);

            RefreshBackgroundPan();

            return true;
        }

        private BitmapImage LoadBackgroundBitmap(string shortcutPath)
        {
            try
            {
                string backgroundPath = IconExtractor.GetCustomBackgroundPath(shortcutPath);

                if (string.IsNullOrWhiteSpace(backgroundPath) || !File.Exists(backgroundPath))
                    return null;

                var bitmap = new BitmapImage();

                using (var stream = new FileStream(backgroundPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }

                bitmap.Freeze();

                return bitmap;
            }

            catch
            {
                return null;
            }
        }

        public void TransitionBackgroundForShortcut(string shortcutPath)
        {
            if (BackgroundImage == null) return;

            var nextBitmap = LoadBackgroundBitmap(shortcutPath);

            bool hasCurrentBackground = BackgroundImage.Source != null;
            bool hasNextBackground = nextBitmap != null;

            if (hasCurrentBackground && hasNextBackground)
            {
                CrossfadeBackground(nextBitmap);
                return;
            }

            if (hasCurrentBackground)
            {
                FadeBackgroundToParticles();
                return;
            }

            if (hasNextBackground)
            {
                FadeParticlesToBackground(nextBitmap);
                return;
            }

            ClearPreviousBackground();
            _particlesCanvas?.SetParticlesVisible(true, true, BackgroundTransitionDuration);
        }

        private void CrossfadeBackground(BitmapImage nextBitmap)
        {
            if (BackgroundPreviousImage == null)
            {
                LoadBackgroundForShortcutFromBitmap(nextBitmap);
                ShowBackground(true);
                return;
            }

            double previousOpacity = BackgroundImage.Opacity;
            double previousX = BackgroundTranslate?.X ?? 0;

            BackgroundImage.BeginAnimation(UIElement.OpacityProperty, null);

            BackgroundPreviousImage.BeginAnimation(UIElement.OpacityProperty, null);
            BackgroundPreviousImage.Source = BackgroundImage.Source;
            BackgroundPreviousImage.Width = BackgroundImage.Width;
            BackgroundPreviousImage.Height = BackgroundImage.Height;
            BackgroundPreviousImage.Visibility = Visibility.Visible;
            BackgroundPreviousImage.Opacity = previousOpacity > 0 ? previousOpacity : 0.7;

            if (BackgroundPreviousTranslate != null)
                BackgroundPreviousTranslate.X = previousX;

            StopBackgroundPan();

            BackgroundImage.Source = nextBitmap;
            BackgroundImage.Visibility = Visibility.Visible;
            BackgroundImage.Opacity = 0;

            RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.HighQuality);

            RefreshBackgroundPan();

            _particlesCanvas?.SetParticlesVisible(false, true, BackgroundTransitionDuration);

            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(BackgroundTransitionDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 0.7,
                Duration = TimeSpan.FromSeconds(BackgroundTransitionDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            fadeOut.Completed += (s, e) => ClearPreviousBackground();

            BackgroundPreviousImage.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            BackgroundImage.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void FadeBackgroundToParticles()
        {
            double currentOpacity = BackgroundImage.Opacity;

            BackgroundImage.BeginAnimation(UIElement.OpacityProperty, null);
            BackgroundImage.Opacity = currentOpacity > 0 ? currentOpacity : 0.7;

            _particlesCanvas?.SetParticlesVisible(true, true, BackgroundTransitionDuration);

            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(BackgroundTransitionDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            fadeOut.Completed += (s, e) =>
            {
                StopBackgroundPan();

                BackgroundImage.BeginAnimation(UIElement.OpacityProperty, null);
                BackgroundImage.Source = null;
                BackgroundImage.Visibility = Visibility.Collapsed;
                BackgroundImage.Opacity = 0;
            };

            BackgroundImage.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void FadeParticlesToBackground(BitmapImage nextBitmap)
        {
            StopBackgroundPan();
            ClearPreviousBackground();

            BackgroundImage.BeginAnimation(UIElement.OpacityProperty, null);
            BackgroundImage.Source = nextBitmap;
            BackgroundImage.Visibility = Visibility.Visible;
            BackgroundImage.Opacity = 0;

            RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.HighQuality);

            RefreshBackgroundPan();

            _particlesCanvas?.SetParticlesVisible(false, true, BackgroundTransitionDuration);

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 0.7,
                Duration = TimeSpan.FromSeconds(BackgroundTransitionDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            BackgroundImage.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void LoadBackgroundForShortcutFromBitmap(BitmapImage bitmap)
        {
            StopBackgroundPan();

            BackgroundImage.BeginAnimation(UIElement.OpacityProperty, null);
            BackgroundImage.Source = bitmap;
            BackgroundImage.Visibility = Visibility.Collapsed;
            BackgroundImage.Opacity = 0;

            RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.HighQuality);

            RefreshBackgroundPan();
        }

        private void ClearPreviousBackground()
        {
            if (BackgroundPreviousImage == null) return;

            BackgroundPreviousImage.BeginAnimation(UIElement.OpacityProperty, null);
            BackgroundPreviousImage.Source = null;
            BackgroundPreviousImage.Visibility = Visibility.Collapsed;
            BackgroundPreviousImage.Opacity = 0;

            if (BackgroundPreviousTranslate != null)
                BackgroundPreviousTranslate.X = 0;
        }

        public void RefreshBackgroundPan()
        {
            StopBackgroundPan();

            if (!(BackgroundImage?.Source is BitmapSource source) || BackgroundViewport == null || BackgroundTranslate == null)
                return;

            BackgroundViewport.UpdateLayout();

            double viewportWidth = BackgroundViewport.ActualWidth;
            double viewportHeight = BackgroundViewport.ActualHeight;

            if (viewportWidth <= 0 || viewportHeight <= 0 || source.PixelWidth <= 0 || source.PixelHeight <= 0)
                return;

            double aspect = (double)source.PixelWidth / source.PixelHeight;
            double renderedHeight = viewportHeight;
            double renderedWidth = renderedHeight * aspect;

            BackgroundImage.Width = renderedWidth;
            BackgroundImage.Height = renderedHeight;

            _backgroundPanOverflow = Math.Max(0, renderedWidth - viewportWidth);

            if (_backgroundPanOverflow <= 0)
            {
                BackgroundTranslate.X = 0;
                return;
            }

            _backgroundPanPosition = _backgroundPanOverflow * BackgroundStartPosition;
            _backgroundPanDirection = 1;
            _backgroundPanLastRenderTime = TimeSpan.Zero;

            BackgroundTranslate.X = -_backgroundPanPosition;

            CompositionTarget.Rendering += BackgroundPan_Rendering;
        }

        private void StopBackgroundPan()
        {
            CompositionTarget.Rendering -= BackgroundPan_Rendering;

            _backgroundPanOverflow = 0;
            _backgroundPanLastRenderTime = TimeSpan.Zero;

            if (BackgroundTranslate != null)
                BackgroundTranslate.X = 0;
        }

        private void BackgroundPan_Rendering(object sender, EventArgs e)
        {
            if (_backgroundPanOverflow <= 0 || BackgroundTranslate == null) return;
            if (!(e is RenderingEventArgs renderingArgs)) return;

            TimeSpan renderTime = renderingArgs.RenderingTime;

            if (_backgroundPanLastRenderTime == TimeSpan.Zero)
            {
                _backgroundPanLastRenderTime = renderTime;
                return;
            }

            double delta = (renderTime - _backgroundPanLastRenderTime).TotalSeconds;
            _backgroundPanLastRenderTime = renderTime;

            if (delta <= 0 || delta > 0.1) return;

            double distanceToEdge = _backgroundPanDirection > 0
                ? _backgroundPanOverflow - _backgroundPanPosition
                : _backgroundPanPosition;

            double edgeFactor = Math.Min(1.0, Math.Max(0.08, distanceToEdge / BackgroundPanEdgeZone));

            _backgroundPanPosition += BackgroundPanSpeed * edgeFactor * _backgroundPanDirection * delta;

            if (_backgroundPanPosition >= _backgroundPanOverflow)
            {
                _backgroundPanPosition = _backgroundPanOverflow;
                _backgroundPanDirection = -1;
            }
            else if (_backgroundPanPosition <= 0)
            {
                _backgroundPanPosition = 0;
                _backgroundPanDirection = 1;
            }

            BackgroundTranslate.X = -_backgroundPanPosition;
        }

        public void ShowBackground(bool visible)
        {
            if (UseImageBackground)
            {
                if (visible)
                    _particlesCanvas?.SetParticlesVisible(false, true, 0.2);

                var bgAnim = new DoubleAnimation
                {
                    To = visible ? 0.7 : 0,
                    Duration = TimeSpan.FromSeconds(0.2),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                bgAnim.Completed += (s, e) =>
                {
                    if (!visible)
                        BackgroundImage.Visibility = Visibility.Collapsed;
                };

                BackgroundImage.Visibility = Visibility.Visible;
                BackgroundImage.BeginAnimation(UIElement.OpacityProperty, bgAnim);
            }

            else
            {
                if (BackgroundImage != null)
                {
                    BackgroundImage.BeginAnimation(UIElement.OpacityProperty, null);
                    BackgroundImage.Visibility = Visibility.Collapsed;
                    BackgroundImage.Opacity = 0;
                }

                _particlesCanvas?.SetParticlesVisible(visible, true, 0.2);
            }
        }

        public void SetParticlesCanvas(ParticlesCanvas canvas)
        {
            _particlesCanvas = canvas;
        }

        public void Dispose()
        {
            StopBackgroundPan();
            SystemProvider.StopClock();
        }
    }
}