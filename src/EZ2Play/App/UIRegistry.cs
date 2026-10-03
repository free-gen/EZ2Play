using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace EZ2Play.App
{
    public class UIRegistry
    {
        public Image SplashLogo { get; set; }
        public Grid SplashOverlay { get; set; }
        public TextBlock NoShortcutsMessage { get; set; }
        public TextBlock ExitMessageText { get; set; }


        public Border NotificationPanel { get; set; }
        public TextBlock NotificationIcon { get; set; }
        public TextBlock NotificationText { get; set; }
        public Notifications Notifications { get; private set; }

        public Image UserAvatar { get; set; }
        public TextBlock TimeLabel { get; set; }

        public Grid MainScreenGrid { get; set; }
        public TextBlock SelectedGameTitle { get; set; }
        public Border GameCounterPill { get; set; }
        public TextBlock GameCounterText { get; set; }

        public HintPanel BottomHintPanel { get; set; }

        private SplashScreen _splash;
        private Wpf.Ui.Controls.ProgressRing _loadingRing;

        public UIRegistry()
        {
            Notifications = new Notifications();
        }

        public void InitializeSplash(Image logo, Grid overlay, Grid mainScreen)
        {
            _splash = new SplashScreen(logo, overlay, mainScreen);
        }

        public void InitializeNotifications(Border NotificationPanel, TextBlock NotificationIcon, TextBlock NotificationText, Sound sound)
        {
            Notifications.Initialize(NotificationPanel, NotificationIcon, NotificationText, sound);
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

            if (GameCounterPill != null)
                GameCounterPill.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
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

        public void Dispose()
        {
            SystemProvider.StopClock();
        }
    }
}