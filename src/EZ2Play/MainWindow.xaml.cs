using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using EZ2Play.App;
using Wpf.Ui.Controls;

namespace EZ2Play
{
    /// <summary>
    /// Owns the main application window, its components, initialization,
    /// startup sequence, and shutdown lifecycle.
    /// </summary>
    /// <remarks>
    /// Activity changes, input handling, and layout updates are implemented
    /// in MainWindow.Activity.cs, MainWindow.Input.cs, and MainWindow.Layout.cs.
    /// All partial declarations share the same window instance and fields.
    /// </remarks>
    public partial class MainWindow : FluentWindow
    {
        // Application components and their shared lifetime.
        private Sound _sound;
        private Input _input;
        private InputHandler _inputHandler;
        private Display _display;
        private UIRegistry _uiRegistry;
        private Launcher _launcher;
        private TabsController _tabsController;
        private BackgroundController _backgroundController;
        private GuideExitHandler _guideHandler;

        private DispatcherTimer _dispatcherPulseTimer;

        private ParticlesCanvas _particlesCanvas;

        private GameMetadata _metadata;
        private AppConfig _config;

        // Background refresh and foreground activity state.
        private DispatcherTimer _backgroundRefreshTimer;
        private string _displayedBackgroundShortcutPath;
        private int _backgroundTransitionDirection;
        private bool _isMainScreenActive = false;
        private bool _wasActive;

        private DateTime _foregroundCandidateSince = DateTime.MinValue;
        private DateTime _backgroundCandidateSince = DateTime.MinValue;

        private const int ForegroundActivationDelayMs = 600;
        private const int BackgroundDeactivationDelayMs = 150;

        private bool _isEmptyState;
        private bool _hotSwapLaunch;
        private bool _isExiting;

        // State used by MainWindow.Layout.cs.
        private bool _displayLayoutRefreshScheduled;
        private double _lastUiScaleFactor = double.NaN;

        // Overlays use the same selector coordinator.
        private SettingsOverlay _settingsOverlay;
        private ParserOverlay _parserOverlay;
        private SelectorCoordinator _selectorCoordinator;

        public bool IsGamepadConnected { get; private set; }

        public bool IsHotSwapLaunch() => _hotSwapLaunch;

        public Display GetDisplay() => _display;
        public AppConfig GetConfig() => _config;
        public Launcher GetLauncher() => _launcher;
        public Sound GetSound() => _sound;

        /// <summary>
        /// Invalidates cached background artwork for a shortcut.
        /// </summary>
        /// <param name="shortcutPath">
        /// Full path of the shortcut whose background cache is invalidated.
        /// </param>
        /// <remarks>
        /// Delegates cache invalidation to BackgroundController.
        /// Used after the parser saves updated background artwork.
        /// </remarks>
        public void InvalidateBackgroundCache(string shortcutPath)
        {
            _backgroundController?.InvalidateBackgroundCache(shortcutPath);
        }

        /// <summary>
        /// Refreshes the background for the currently selected shortcut.
        /// </summary>
        /// <remarks>
        /// Uses an animated transition when the main screen is active
        /// and the window is in the foreground. Otherwise, loads
        /// the background directly.
        ///
        /// Stores the displayed shortcut path and resets the
        /// transition direction after updating the background.
        /// </remarks>
        public void RefreshSelectedBackground()
        {
            if (_isExiting)
                return;

            if (_launcher == null || _launcher.Shortcuts.Length == 0 || _launcher.SelectedIndex < 0)
                return;

            var shortcut = _launcher.Shortcuts[_launcher.SelectedIndex];

            if (_isMainScreenActive && SystemProvider.IsForeground())
            {
                _backgroundController.TransitionTo(
                    shortcut.FullPath,
                    _backgroundTransitionDirection);
            }

            else
            {
                _backgroundController.Load(shortcut.FullPath);
            }

            _displayedBackgroundShortcutPath = shortcut.FullPath;
            _backgroundTransitionDirection = 0;
        }

        /// <summary>
        /// Shows or hides the main application's loading indicator.
        /// </summary>
        /// <param name="show">
        /// Whether the loading indicator should be visible.
        /// </param>
        public void ShowLoadingUI(bool show)
        {
            _uiRegistry.ShowLoading(show);
        }

        /// <summary>
        /// Initializes the WPF window and application services.
        /// </summary>
        /// <remarks>
        /// The initialization order is significant and must remain unchanged.
        /// </remarks>
        public MainWindow(bool hotSwap = false)
        {
            InitializeComponent();
            _hotSwapLaunch = hotSwap;

            SubscribeEvents();
            OptimizeListBoxPerformance();
            InitializeComponents();
            InitializeUIRegistry();
            InitializeLauncher();
            InitializeTimers();
            InitializeUI();
        }

        /// <summary>
        /// Registers window input, resize, and layout events.
        /// </summary>
        /// <remarks>
        /// Applies the initial UI scale immediately and updates it
        /// again after Loaded, when the actual window size is available.
        /// </remarks>
        private void SubscribeEvents()
        {
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            SizeChanged += OnWindowSizeChanged;
            Loaded += (s, e) => UpdateUiScaleResources(ActualHeight > 0 ? ActualHeight : LayoutScaler.ReferenceHeight);

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            UpdateUiScaleResources(ActualHeight > 0 ? ActualHeight : LayoutScaler.ReferenceHeight);
        }

        /// <summary>
        /// Suppresses manipulation boundary feedback for the carousel.
        /// </summary>
        private void OptimizeListBoxPerformance()
        {
            ItemsListBox.ManipulationBoundaryFeedback += (s, e) => e.Handled = true;
        }

        /// <summary>
        /// Creates the services and overlays owned by the main window.
        /// </summary>
        private void InitializeComponents()
        {
            _sound = new Sound();
            _display = new Display(this, _hotSwapLaunch, _sound);
            _input = new Input();
            _inputHandler = new InputHandler(_input);
            _guideHandler = new GuideExitHandler(_sound);
            _particlesCanvas = FindName("particles") as ParticlesCanvas;
            _config = new AppConfig();

            _selectorCoordinator = new SelectorCoordinator(
                SelectionSelector, ItemsListBox, MainScreenGrid, CarouselWrapper);

            _settingsOverlay = new SettingsOverlay(_inputHandler, this, _selectorCoordinator);
            _parserOverlay = new ParserOverlay(_inputHandler, this, _selectorCoordinator);

            _selectorCoordinator.SetOverlaySurfaces(_settingsOverlay, _parserOverlay);

            var overlayLayer = new Grid();
            overlayLayer.Children.Add(_settingsOverlay);
            overlayLayer.Children.Add(_parserOverlay);

            OverlayHost.Content = overlayLayer;

            _settingsOverlay.Visibility = Visibility.Collapsed;
            _parserOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Connects UIRegistry to named XAML elements and initializes
        /// the splash screen, notifications, loading indicator,
        /// and background controller.
        /// </summary>
        private void InitializeUIRegistry()
        {
            _uiRegistry = new UIRegistry
            {
                TimeLabel = FindName("TimeLabelText") as System.Windows.Controls.TextBlock,
                UserAvatar = FindName("UserAvatar") as System.Windows.Controls.Image,
                NoShortcutsMessage = FindName("NoShortcutsMessage") as System.Windows.Controls.TextBlock,
                SelectedGameTitle = FindName("SelectedGameTitle") as System.Windows.Controls.TextBlock,
                MainScreenGrid = FindName("MainScreenGrid") as System.Windows.Controls.Grid,
                ExitMessageText = FindName("ExitMessageText") as System.Windows.Controls.TextBlock,
                BottomHintPanel = FindName("HintPanel") as HintPanel,
                GameCounterText = FindName("GameCounterText") as System.Windows.Controls.TextBlock,
                GameCounterPill = FindName("GameCounterPill") as System.Windows.Controls.Border,
            };

            _uiRegistry.InitializeSplash(SplashLogo, SplashOverlay, MainScreenGrid);
            _uiRegistry.InitializeNotifications(NotificationPanel, NotificationIcon, NotificationText, _sound);
            _uiRegistry.InitializeLoadingRing(FindName("LoadingProgress") as Wpf.Ui.Controls.ProgressRing);
            _backgroundController = new BackgroundController(
                FindName("BackgroundViewport") as Grid,
                FindName("BackgroundPreviousImage") as System.Windows.Controls.Image,
                FindName("BackgroundImage") as System.Windows.Controls.Image,
                _particlesCanvas);
        }

        /// <summary>
        /// Creates the game launcher and tab controller.
        /// </summary>
        /// <remarks>
        /// Connects selection changes to deferred background refresh
        /// and initializes the selected carousel item.
        /// </remarks>
        private void InitializeLauncher()
        {
            _launcher = new Launcher(ItemsListBox, _uiRegistry.SelectedGameTitle, this, _sound);
            _metadata = _launcher.Playtime;

            _tabsController = new TabsController(
                FindName("TabGamelistText") as System.Windows.Controls.TextBlock,
                FindName("TabLastPlayedText") as System.Windows.Controls.TextBlock,
                FindName("CarouselWrapper") as Grid,
                Dispatcher,
                _launcher,
                _sound,
                () => ActualWidth);

            _launcher.SelectionChanged += _ => ScheduleBackgroundRefresh();

            InitializeCarouselSelectedItem();
        }

        /// <summary>
        /// Changes the context displayed by the shared hint panel.
        /// </summary>
        /// <param name="mode">
        /// Hint mode corresponding to the active application screen.
        /// </param>
        public void SetHintsMode(HintPanel.HintMode mode)
        {
            HintPanel.Mode = mode;
        }

        /// <summary>
        /// Initializes the deferred background refresh timer and
        /// subscribes to carousel item container generation.
        /// </summary>
        /// <remarks>
        /// The background refresh interval remains 500 milliseconds.
        /// </remarks>
        private void InitializeTimers()
        {
            _backgroundRefreshTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _backgroundRefreshTimer.Tick += BackgroundRefreshTimer_Tick;

            ItemsListBox.ItemContainerGenerator.StatusChanged +=
                ItemContainerGenerator_StatusChanged;
        }

        /// <summary>
        /// Schedules a background refresh after a selection change.
        /// </summary>
        /// <remarks>
        /// Restarts the existing timer so rapid selection changes
        /// do not immediately trigger individual background updates.
        /// </remarks>
        private void ScheduleBackgroundRefresh()
        {
            if (_isExiting || !_isMainScreenActive || _isEmptyState) return;

            _backgroundRefreshTimer.Stop();
            _backgroundRefreshTimer.Start();
        }

        /// <summary>
        /// Refreshes the selected background after the timer fires.
        /// </summary>
        /// <remarks>
        /// Skips inactive or empty application states and avoids
        /// refreshing an already displayed shortcut background.
        /// </remarks>
        private void BackgroundRefreshTimer_Tick(object sender, EventArgs e)
        {
            _backgroundRefreshTimer.Stop();

            if (_isExiting || !_isMainScreenActive || _isEmptyState) return;
            if (_launcher == null || _launcher.Shortcuts.Length == 0 || _launcher.SelectedIndex < 0) return;

            string selectedShortcutPath = _launcher.Shortcuts[_launcher.SelectedIndex].FullPath;

            if (string.Equals(selectedShortcutPath, _displayedBackgroundShortcutPath, StringComparison.OrdinalIgnoreCase))
                return;

            RefreshSelectedBackground();
        }

        /// <summary>
        /// Applies localization and initializes visible UI information.
        /// </summary>
        /// <remarks>
        /// The window starts fully transparent before the startup
        /// animation. Clock and account picture initialization follow.
        /// </remarks>
        private void InitializeUI()
        {
            Locals.ApplyLocalization(this);
            Opacity = 0.0;

            _uiRegistry.InitializeClock();
            _uiRegistry.LoadUserAvatar();
        }

        /// <summary>
        /// Updates the playtime presentation for the selected shortcut.
        /// </summary>
        private void UpdatePlaytimeUI()
        {
            if (_launcher.Shortcuts.Length == 0)
            {
                _uiRegistry.UpdatePlaytimeDisplay("", false);
                return;
            }

            var shortcut = _launcher.Shortcuts[_launcher.SelectedIndex];
            string gameId = shortcut.FullPath;

            int seconds = _metadata.GetSeconds(gameId);

            if (seconds == 0)
            {
                _uiRegistry.UpdatePlaytimeDisplay("", false);
            }

            else
            {
                var (value, isHours) = _metadata.GetFormattedValue(gameId);
                string text = Locals.GetFormattedTime(value, isHours);

                _uiRegistry.UpdatePlaytimeDisplay(text, true);
            }
        }

        /// <summary>
        /// Handles carousel selection changes and refreshes playtime.
        /// </summary>
        /// <remarks>
        /// Launcher owns selection animation while MainWindow
        /// remains responsible for updating playtime information.
        /// </remarks>
        private void ItemsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;

            if (listBox == ItemsListBox)
                _launcher.HandleSelectionChangedAndAnimate(listBox, e);

            UpdatePlaytimeUI();
        }

        /// <summary>
        /// Completes initialization after the splash screen and starts
        /// application input, activity monitoring, and the main selector.
        /// </summary>
        private void StartPostSplash()
        {
            _launcher.LoadShortcuts();

            _isEmptyState = _launcher.Shortcuts.Length == 0;

            // Select input routing based on library availability.
            _inputHandler.SetMode(
                _isEmptyState
                    ? InputHandler.InputMode.Empty
                    : InputHandler.InputMode.Main);

            _uiRegistry.SetEmptyState(_isEmptyState);

            if (!_isEmptyState)
            {
                RefreshSelectedBackground();
                ShowMainScreenWithAnimation();
                ShowStartupNotifications();
            }

            StartApplication();

            _isMainScreenActive = true;
            _selectorCoordinator.ShowMain();

            UpdateAppActivity(SystemProvider.IsForeground());
        }

        /// <summary>
        /// Displays the main screen using the original fade-in animation.
        /// </summary>
        /// <remarks>
        /// The animation lasts 300 milliseconds with cubic ease-out.
        /// Background visibility is updated after animation completion.
        /// </remarks>
        private void ShowMainScreenWithAnimation()
        {
            var baseGrid = FindName("MainScreenGrid") as System.Windows.Controls.Grid;

            if (baseGrid == null) return;

            baseGrid.Visibility = Visibility.Visible;
            baseGrid.Opacity = 0;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            fadeIn.Completed += (s, args) => _backgroundController.Show(true);
            baseGrid.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        /// <summary>
        /// Displays startup notifications for Debug, Xbox Game Bar,
        /// and HotSwap when their conditions are satisfied.
        /// </summary>
        /// <remarks>
        /// The Game Bar warning is shown whenever the package
        /// is detected during application startup.
        ///
        /// AppConfig continues to track HotSwap notifications.
        /// </remarks>
        private void ShowStartupNotifications()
        {
            // Debug build notification
            _uiRegistry.Notifications.Debug(1, 3);

            // Warn about possible Guide conflicts only when Game Bar is installed.
            if (SystemProvider.IsXboxGameBarInstalled())
            {
                _uiRegistry.Notifications.GameBarWarning(1, 8);
            }

            // HotSwap notification
            if (_hotSwapLaunch && _config.ShouldShowHotSwapNotification(true))
            {
                _uiRegistry.Notifications.HotSwap(2, 8);
                _config.MarkHotSwapNotificationShown(true);
            }
        }

        /// <summary>
        /// Displays an error notification requested by ParserOverlay.
        /// </summary>
        /// <param name="text">
        /// Error message to display.
        /// </param>
        public void ShowParserErrorNotification(string text)
        {
            _uiRegistry.Notifications.Error(text, 0, 3);
        }

        /// <summary>
        /// Starts foreground activity polling and connects the input handlers.
        /// </summary>
        /// <remarks>
        /// Foreground activation requires a 600 ms confirmation.
        /// Foreground loss requires a 150 ms confirmation to
        /// prevent transient focus changes from resetting the UI.
        /// </remarks>
        private void StartApplication()
        {
            SystemProvider.SetDisplaySleepBlocked(true);

            _dispatcherPulseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.Zero
            };

            _dispatcherPulseTimer.Tick += (s, e) =>
            {
                bool isForeground = SystemProvider.IsForeground();

                if (_isExiting || !_isMainScreenActive)
                    return;

                if (!isForeground)
                {
                    _foregroundCandidateSince = DateTime.MinValue;

                    if (!_wasActive)
                        return;

                    DateTime backgroundNow = DateTime.UtcNow;

                    // Ignore brief foreground interruptions.
                    if (_backgroundCandidateSince == DateTime.MinValue)
                    {
                        _backgroundCandidateSince = backgroundNow;
                        return;
                    }

                    // Suspend the application only after the
                    // foreground loss has been confirmed.
                    if ((backgroundNow - _backgroundCandidateSince).TotalMilliseconds
                        >= BackgroundDeactivationDelayMs)
                    {
                        UpdateAppActivity(false);
                    }

                    return;
                }

                // Cancel a pending deactivation when focus returns.
                _backgroundCandidateSince = DateTime.MinValue;

                if (_wasActive)
                    return;

                DateTime now = DateTime.UtcNow;

                if (_foregroundCandidateSince == DateTime.MinValue)
                {
                    _foregroundCandidateSince = now;
                    return;
                }

                if ((now - _foregroundCandidateSince).TotalMilliseconds >= ForegroundActivationDelayMs)
                {
                    UpdateAppActivity(true);
                }
            };

            _dispatcherPulseTimer.Start();

            SetupInputEvents();

            KeyDown += MainWindow_KeyDown;
            KeyUp += MainWindow_KeyUp;

            _input.OnGamepadConnectionChanged += OnGamepadConnectionChanged;
            IsGamepadConnected = _input.IsGamepadConnected;
            _uiRegistry.RefreshHintIcons(IsGamepadConnected);
        }

        /// <summary>
        /// Begins the original shutdown sequence.
        /// </summary>
        /// <remarks>
        /// The delayed Close runs after 2000 ms on the window dispatcher.
        /// </remarks>
        public void ExitApplication()
        {
            _dispatcherPulseTimer?.Stop();

            if (_isExiting)
                return;

            _isExiting = true;
            _isMainScreenActive = false;
            _backgroundRefreshTimer?.Stop();

            _display?.HandleHotswapOnExit();

            _sound.PlayBackSound();
            _sound.StopBackgroundMusicSafe(Sound.FadeDurationMs);

            _backgroundController.Show(false);
            _uiRegistry.ShowExitOverlay();

            Task.Delay(2000).ContinueWith(_ => Dispatcher.Invoke(Close));
        }

        /// <summary>
        /// Displays the application through its startup animation.
        /// </summary>
        /// <param name="skipSplash">
        /// Whether to skip the normal splash screen.
        /// </param>
        /// <remarks>
        /// Initializes the main screen and activates the window after
        /// the startup animation callback.
        ///
        /// System keyboard warm-up is scheduled separately using
        /// DispatcherPriority.ApplicationIdle.
        /// </remarks>
        public void ShowWithAnimation(bool skipSplash = false)
        {
            ShowInTaskbar = true;
            SystemProvider.HideCursor();

            _uiRegistry.ShowWithAnimation(skipSplash, () =>
            {
                StartPostSplash();
                Activate();

                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        SystemProvider.WarmUpSystemKeyboard();
                    }),
                    DispatcherPriority.ApplicationIdle);
            });
        }

        /// <summary>
        /// Releases window-owned services in their original order.
        /// </summary>
        /// <remarks>
        /// Stops activity and background timers, disposes the selector
        /// and application components, then restores Windows display
        /// sleep and cursor behavior.
        ///
        /// The disposal sequence and the call to base.OnClosed
        /// must remain unchanged.
        /// </remarks>
        protected override void OnClosed(EventArgs e)
        {
            _dispatcherPulseTimer?.Stop();

            _isExiting = false;

            _backgroundRefreshTimer?.Stop();
            _selectorCoordinator?.Dispose();

            _parserOverlay?.Dispose();
            _input?.Dispose();
            _guideHandler?.Dispose();
            _sound?.Dispose();
            _backgroundController?.Dispose();
            _uiRegistry?.Dispose();

            _display?.Dispose();

            SystemProvider.SetDisplaySleepBlocked(false);
            SystemProvider.ShowCursor();

            base.OnClosed(e);
        }

        /// <summary>
        /// Registers the native window handle and Windows message hook.
        /// </summary>
        /// <remarks>
        /// Makes the HWND available to SystemProvider and attaches
        /// Display.WndProc to the WPF HwndSource.
        ///
        /// The original exception handling is preserved because
        /// registration of the message hook is performed inside
        /// the existing guarded block.
        /// </remarks>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            SystemProvider.SetMainWindowHandle(new WindowInteropHelper(this).Handle);

            try
            {
                var source = (HwndSource)PresentationSource.FromVisual(this);
                source?.AddHook(_display.WndProc);
            }

            catch
            {
            }
        }
    }
}
