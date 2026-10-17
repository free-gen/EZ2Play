using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using EZ2Play.App;
using Wpf.Ui.Controls;

namespace EZ2Play
{
    public partial class MainWindow : FluentWindow
    {
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

        private DispatcherTimer _backgroundRefreshTimer;
        private string _displayedBackgroundShortcutPath;
        private int _backgroundTransitionDirection;
        private bool _isMainScreenActive = false;
        private bool _wasActive;
        private DateTime _foregroundCandidateSince = DateTime.MinValue;
        private const int ForegroundActivationDelayMs = 600;

        private bool _isEmptyState;
        private bool _hotSwapLaunch;
        private bool _isExiting;

        private bool _displayLayoutRefreshScheduled;
        private double _lastUiScaleFactor = double.NaN;

        private SettingsOverlay _settingsOverlay;
        private ParserOverlay _parserOverlay;
        private SelectorCoordinator _selectorCoordinator;

        public bool IsGamepadConnected { get; private set; }

        public bool IsHotSwapLaunch() => _hotSwapLaunch;

        public Display GetDisplay() => _display;
        public AppConfig GetConfig() => _config;
        public Launcher GetLauncher() => _launcher;
        public Sound GetSound() => _sound;

        public void InvalidateBackgroundCache(string shortcutPath)
        {
            _backgroundController?.InvalidateBackgroundCache(shortcutPath);
        }

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

        public void ShowLoadingUI(bool show)
        {
            _uiRegistry.ShowLoading(show);
        }

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

        private void SubscribeEvents()
        {
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            SizeChanged += OnWindowSizeChanged;
            Loaded += (s, e) => UpdateUiScaleResources(ActualHeight > 0 ? ActualHeight : LayoutScaler.ReferenceHeight);

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            UpdateUiScaleResources(ActualHeight > 0 ? ActualHeight : LayoutScaler.ReferenceHeight);
        }

        private void OptimizeListBoxPerformance()
        {
            ItemsListBox.ManipulationBoundaryFeedback += (s, e) => e.Handled = true;
        }

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

        public void SetHintsMode(HintPanel.HintMode mode)
        {
            HintPanel.Mode = mode;
        }

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

        private void ScheduleBackgroundRefresh()
        {
            if (_isExiting || !_isMainScreenActive || _isEmptyState) return;

            _backgroundRefreshTimer.Stop();
            _backgroundRefreshTimer.Start();
        }

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

        private void InitializeUI()
        {
            Locals.ApplyLocalization(this);
            Opacity = 0.0;

            _uiRegistry.InitializeClock();
            _uiRegistry.LoadUserAvatar();
        }

        private void UpdateAppActivity(bool isActive)
        {
            if (_isExiting || !_isMainScreenActive || isActive == _wasActive)
                return;

            _wasActive = isActive;

            if (isActive)
                OnBecameActive();
            else
                OnBecameInactive();
        }

        private void OnBecameActive()
        {
            _sound.PlayBackgroundMusic(Sound.FadeDurationMs * 3);
            SystemProvider.HideCursor();
            _backgroundController.Show(true);

            _metadata.Stop();
            UpdatePlaytimeUI();

            if (_tabsController.IsLastPlayed)
            {
                _launcher.SortByLastPlayed();
            }
        }

        private void OnBecameInactive()
        {
            _sound.StopBackgroundMusicSafe(Sound.FadeDurationMs);
            _uiRegistry.ShowLoading(false);
            SystemProvider.ShowCursor();
            _backgroundController.Show(false);
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            _input.HandleKeyDown(e.Key);
        }

        private void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            _input.HandleKeyUp(e.Key);
        }

        private void SetupInputEvents()
        {
            _inputHandler.OnMoveSelection += direction =>
            {
                if (_isExiting)
                    return;

                _backgroundTransitionDirection = Math.Sign(direction);
                _launcher.MoveSelection(direction);
            };

            _inputHandler.OnLaunchSelected += () =>
            {
                if (_isExiting) return;

                _launcher.LaunchSelected();
            };

            _inputHandler.OnSwitchToGamelist += () =>
            {
                if (_isExiting) return;
                _tabsController.SwitchToGamelist();
            };

            _inputHandler.OnSwitchToLastPlayed += () =>
            {
                if (_isExiting) return;
                _tabsController.SwitchToLastPlayed();
            };

            _inputHandler.OnOpenSettings += () =>
            {
                if (_isExiting || !_isMainScreenActive || _isEmptyState) return;

                _settingsOverlay.Open();
            };

            _inputHandler.OnSettingsBack += () => _settingsOverlay.Back();
            _inputHandler.OnSettingsConfirm += () => _settingsOverlay.Confirm();

            _inputHandler.OnSettingsNavigate += (dir) => _settingsOverlay.Navigate(dir, true);
            _inputHandler.OnSettingsNavigateVertical += (dir) => _settingsOverlay.Navigate(dir, false);

            _inputHandler.OnOpenParser += () =>
            {
                if (_isExiting || !_isMainScreenActive) return;

                _parserOverlay.Open();
            };

            _inputHandler.OnParserBack += () => _parserOverlay.Back();
            _inputHandler.OnParserConfirm += () => _parserOverlay.Confirm();
            _inputHandler.OnParserSearch += () => _parserOverlay.Search();
            _inputHandler.OnParserNavigateHorizontal += dir => _parserOverlay.NavigateHorizontal(dir);
            _inputHandler.OnParserNavigateVertical += dir => _parserOverlay.NavigateVertical(dir);
            _inputHandler.OnParserSwitchTab += dir => _parserOverlay.SwitchAssetTab(dir);
        }

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

        private void InitializeCarouselSelectedItem()
        {
            CarouselAnimation.InitializeSelectedItem(ItemsListBox);
        }

        private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {
            if (ItemsListBox.ItemContainerGenerator.Status !=
                System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                return;
            }

            if (CarouselAnimation.RetryPendingSelection(ItemsListBox))
                return;

            InitializeCarouselSelectedItem();
        }

        private void UpdateUiScaleResources(double windowHeight)
        {
            double scale = LayoutScaler.GetScaleFactor(windowHeight);
            if (Math.Abs(_lastUiScaleFactor - scale) < 0.0001) return;

            LayoutScaler.ApplyUiScaleToDictionary(this.Resources, windowHeight);
            _lastUiScaleFactor = scale;
        }

        private void OnGamepadConnectionChanged(bool connected, string deviceName)
        {
            IsGamepadConnected = connected;
            _uiRegistry.RefreshHintIcons(connected);

            if (connected)
            {
                _uiRegistry.Notifications.HotPlug(0, 3, deviceName);
            }
        }

        private void ItemsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;

            if (listBox == ItemsListBox)
                _launcher.HandleSelectionChangedAndAnimate(listBox, e);

            UpdatePlaytimeUI();
        }

        private void StartPostSplash()
        {
            _launcher.LoadShortcuts();

            _isEmptyState = _launcher.Shortcuts.Length == 0;
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

        private void ShowStartupNotifications()
        {
            // Debug build notification
            _uiRegistry.Notifications.Debug(1, 3);

            // Xbox Game Bar notification
            bool gamebarInstalled = SystemProvider.IsXboxGameBarInstalled();

            if (_config.ShouldShowGamebarNotification(gamebarInstalled))
            {
                _uiRegistry.Notifications.GameBar(1, 8, gamebarInstalled);
                _config.MarkGamebarNotificationShown(gamebarInstalled);
            }

            // HotSwap notification
            if (_hotSwapLaunch && _config.ShouldShowHotSwapNotification(true))
            {
                _uiRegistry.Notifications.HotSwap(2, 8);
                _config.MarkHotSwapNotificationShown(true);
            }
        }

        public void ShowParserErrorNotification(string text)
        {
            _uiRegistry.Notifications.Error(text, 0, 3);
        }

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

                    if (_wasActive)
                        UpdateAppActivity(false);

                    return;
                }

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

        private void RefreshActiveSelector()
        {
            if (_isExiting) return;

            if (_settingsOverlay?.Visibility == Visibility.Visible)
                _settingsOverlay.RefreshSelector();
            else if (_parserOverlay?.Visibility == Visibility.Visible)
                _parserOverlay.RefreshSelector();
            else
                _selectorCoordinator?.UpdateMain();
        }

        private void ScheduleDisplayLayoutRefresh()
        {
            if (_displayLayoutRefreshScheduled || _isExiting) return;
            _displayLayoutRefreshScheduled = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_isExiting && ActualHeight > 0)
                {
                    UpdateUiScaleResources(ActualHeight);
                    ItemsListBox?.InvalidateMeasure();
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _displayLayoutRefreshScheduled = false;
                    if (_isExiting || ActualHeight <= 0) return;

                    InitializeCarouselSelectedItem();
                    RefreshActiveSelector();
                    _backgroundController?.RefreshPan();
                }), DispatcherPriority.Loaded);
            }), DispatcherPriority.Loaded);
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScheduleDisplayLayoutRefresh();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            if (ActualHeight > 0)
            {
                UpdateUiScaleResources(ActualHeight);
                Dispatcher.BeginInvoke(new Action(RefreshActiveSelector), DispatcherPriority.Loaded);
            }
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);

            _foregroundCandidateSince = DateTime.MinValue;
            UpdateAppActivity(false);
        }

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

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            ScheduleDisplayLayoutRefresh();
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_isEmptyState && e.Key == Key.Escape)
            {
                e.Handled = true;
                ExitApplication();
                return;
            }

            if (e.Key == Key.Tab)
            {
                e.Handled = true;
            }
        }
    }
}
