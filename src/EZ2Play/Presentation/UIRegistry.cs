
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EZ2Play.App
{
    /// <summary>
    /// Coordinates the main window's UI components and
    /// provides methods for updating their visual states.
    /// </summary>
    /// <remarks>
    /// Acts as a central access point for splash screen,
    /// notifications, clock, avatar, loading indicator,
    /// game information, and input hints.
    /// </remarks>
    public class UIRegistry
    {
        // Empty library and application exit messages.
        public TextBlock NoShortcutsMessage { get; set; }
        public TextBlock ExitMessageText { get; set; }

        // Notification system.
        public Notifications Notifications { get; private set; }

        // User information.
        public Image UserAvatar { get; set; }
        public TextBlock TimeLabel { get; set; }

        // Main screen and selected game information.
        public Grid MainScreenGrid { get; set; }
        public TextBlock SelectedGameTitle { get; set; }
        public Border GameCounterPill { get; set; }
        public TextBlock GameCounterText { get; set; }

        // Context-sensitive input hints.
        public HintPanel BottomHintPanel { get; set; }

        // Components initialized after the registry is created.
        private SplashScreen _splash;
        private Wpf.Ui.Controls.ProgressRing _loadingRing;

        public UIRegistry()
        {
            Notifications = new Notifications();
        }

        /// <summary>
        /// Initializes the splash screen using the supplied
        /// logo, overlay, and main screen elements.
        /// </summary>
        public void InitializeSplash(
            Image logo,
            Grid overlay,
            Grid mainScreen)
        {
            _splash = new SplashScreen(logo, overlay, mainScreen);
        }

        /// <summary>
        /// Connects the notification system to its visual
        /// elements and the application's sound manager.
        /// </summary>
        public void InitializeNotifications(
            Border NotificationPanel,
            TextBlock NotificationIcon,
            TextBlock NotificationText,
            Sound sound)
        {
            Notifications.Initialize(
                NotificationPanel,
                NotificationIcon,
                NotificationText,
                sound);
        }

        /// <summary>
        /// Displays the current time and starts receiving
        /// subsequent clock updates from SystemProvider.
        /// </summary>
        public void InitializeClock()
        {
            UpdateClockDisplay();

            SystemProvider.StartClock((time) =>
            {
                if (TimeLabel != null)
                    TimeLabel.Text = time;
            });
        }

        /// <summary>
        /// Immediately refreshes the clock label
        /// using the current system time.
        /// </summary>
        public void UpdateClockDisplay()
        {
            if (TimeLabel != null)
                TimeLabel.Text = SystemProvider.GetCurrentTime();
        }

        /// <summary>
        /// Loads the current Windows user's avatar
        /// and updates its visibility.
        /// </summary>
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

        /// <summary>
        /// Applies a circular clipping geometry to
        /// the user avatar based on its actual width.
        /// </summary>
        private void ClipAvatarToCircle()
        {
            var r = UserAvatar.ActualWidth / 2;

            if (r > 0)
                UserAvatar.Clip = new EllipseGeometry(
                    new Point(r, r),
                    r,
                    r);
        }

        /// <summary>
        /// Switches between the normal main screen
        /// and the empty library message.
        /// </summary>
        public void SetEmptyState(bool isEmpty)
        {
            if (MainScreenGrid != null)
            {
                MainScreenGrid.Visibility = isEmpty
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            if (BottomHintPanel != null)
            {
                BottomHintPanel.Visibility = isEmpty
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            if (NoShortcutsMessage != null)
            {
                NoShortcutsMessage.Visibility = isEmpty
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Hides the main interface and displays
        /// the application exit message with a fade-in animation.
        /// </summary>
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
                    EasingFunction = new CubicEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                };

                ExitMessageText.BeginAnimation(
                    UIElement.OpacityProperty,
                    animation);
            }
        }

        /// <summary>
        /// Displays the application splash screen
        /// using the configured startup animation.
        /// </summary>
        /// <param name="skipSplash">
        /// Indicates whether the splash animation should be skipped.
        /// </param>
        /// <param name="onAfterSplash">
        /// Callback invoked by SplashScreen after its sequence.
        /// </param>
        public void ShowWithAnimation(
            bool skipSplash,
            Action onAfterSplash)
        {
            _splash?.ShowWithAnimation(
                skipSplash,
                onAfterSplash);
        }

        /// <summary>
        /// Updates the selected game's playtime text
        /// and the visibility of its counter.
        /// </summary>
        public void UpdatePlaytimeDisplay(
            string text,
            bool visible)
        {
            if (GameCounterText != null)
                GameCounterText.Text = text;

            if (GameCounterPill != null)
            {
                GameCounterPill.Visibility = visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Registers the progress indicator used
        /// during game launching.
        /// </summary>
        public void InitializeLoadingRing(
            Wpf.Ui.Controls.ProgressRing ring)
        {
            _loadingRing = ring;
        }

        /// <summary>
        /// Shows or hides the game launch progress indicator.
        /// </summary>
        public void ShowLoading(bool show)
        {
            if (_loadingRing != null)
            {
                _loadingRing.Visibility = show
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Updates the input hints to match
        /// the currently active input device.
        /// </summary>
        public void RefreshHintIcons(bool isGamepad)
        {
            if (BottomHintPanel != null)
            {
                BottomHintPanel.Device = isGamepad
                    ? HintPanel.InputDevice.Gamepad
                    : HintPanel.InputDevice.Keyboard;
            }
        }

        /// <summary>
        /// Stops the system clock updates when
        /// the interface is being disposed.
        /// </summary>
        public void Dispose()
        {
            SystemProvider.StopClock();
        }
    }
}
