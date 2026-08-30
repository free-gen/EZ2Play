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
        private GuideExitHandler _guideHandler;

        private ParticlesCanvas _particlesCanvas;

        private GameMetadata _metadata;
        private AppConfig _config;

        private DispatcherTimer _activityTimer;
        private bool _isMainScreenActive = false;
        private bool _wasActive;
        private bool _isEmptyState;
        private bool _hotSwapLaunch;
        private bool _isExiting;
        private bool _isTabSwitching;

        private double _backgroundPanOverflow;
        private double _backgroundPanPosition;
        private double _backgroundPanDirection = 1;
        private DateTime _backgroundPanLastFrame;

        private enum TabType { Gamelist, LastPlayed }
        private TabType _currentTab = TabType.Gamelist;

        private SettingsOverlay _settingsOverlay;
        private ParserOverlay _parserOverlay;

        public bool IsGamepadConnected { get; private set; }

        public bool IsHotSwapLaunch() => _hotSwapLaunch;

        public Display GetDisplay() => _display;
        public AppConfig GetConfig() => _config;
        public Launcher GetLauncher() => _launcher;
        public Sound GetSound() => _sound;

        public void RefreshSelectedBackground()
        {
            if (_launcher == null || _launcher.Shortcuts.Length == 0 || _launcher.SelectedIndex < 0)
                return;

            var shortcut = _launcher.Shortcuts[_launcher.SelectedIndex];

            CompositionTarget.Rendering -= BackgroundPan_Rendering;
            BackgroundTranslate.X = 0;
            _backgroundPanOverflow = 0;

            bool hasBackground = _uiRegistry.LoadBackgroundForShortcut(shortcut.FullPath);

            if (hasBackground)
                TestBackgroundPan();

            if (_isMainScreenActive)
                _uiRegistry.ShowBackground(true);
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

            _settingsOverlay = new SettingsOverlay(_inputHandler, this);
            _inputHandler.RegisterSettingsOverlay(_settingsOverlay);

            _parserOverlay = new ParserOverlay(_inputHandler, this);

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
                TabGamelistText = FindName("TabGamelistText") as System.Windows.Controls.TextBlock,
                TabLastPlayedText = FindName("TabLastPlayedText") as System.Windows.Controls.TextBlock,
                TimeLabel = FindName("TimeLabelText") as System.Windows.Controls.TextBlock,
                UserAvatar = FindName("UserAvatar") as System.Windows.Controls.Image,
                TopPanel = FindName("TopPanel") as System.Windows.Controls.Grid,
                NoShortcutsMessage = FindName("NoShortcutsMessage") as System.Windows.Controls.TextBlock,
                SelectedGameTitle = FindName("SelectedGameTitle") as System.Windows.Controls.TextBlock,
                GameSourceCard = FindName("GameSourceCard") as System.Windows.Controls.Border,
                SplashLogo = FindName("SplashLogo") as System.Windows.Controls.Image,
                SplashOverlay = FindName("SplashOverlay") as System.Windows.Controls.Grid,
                MainScreenGrid = FindName("MainScreenGrid") as System.Windows.Controls.Grid,
                ExitMessageText = FindName("ExitMessageText") as System.Windows.Controls.TextBlock,
                BottomHintPanel = FindName("HintPanel") as HintPanel,
                NotificationPanel = FindName("NotificationPanel") as System.Windows.Controls.Border,
                NotificationIcon = FindName("NotificationIcon") as System.Windows.Controls.TextBlock,
                NotificationText = FindName("NotificationText") as System.Windows.Controls.TextBlock,
                BackgroundImage = FindName("BackgroundImage") as System.Windows.Controls.Image,
                GameCounterText = FindName("GameCounterText") as System.Windows.Controls.TextBlock,
                GameCounterCard = FindName("GameCounterCard") as System.Windows.Controls.Border,
                ItemsListBox = ItemsListBox
            };

            _uiRegistry.InitializeSplash(SplashLogo, SplashOverlay, MainScreenGrid);
            _uiRegistry.InitializeNotifications(NotificationPanel, NotificationIcon, NotificationText);
            _uiRegistry.CarouselWrapper = FindName("CarouselWrapper") as System.Windows.Controls.Grid;
            _uiRegistry.SetParticlesCanvas(_particlesCanvas);
            _uiRegistry.InitializeLoadingRing(FindName("LoadingProgress") as Wpf.Ui.Controls.ProgressRing);
        }

        private void InitializeLauncher()
        {
            _launcher = new Launcher(ItemsListBox, _uiRegistry.SelectedGameTitle, this, _sound);
            _metadata = _launcher.Playtime;
            // _config = new AppConfig();

            InitializeCarouselSelectedItem();
        }

        public void SetHintsMode(HintPanel.HintMode mode)
        {
            HintPanel.Mode = mode;
        }

        private void InitializeTimers()
        {
            _activityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _activityTimer.Tick += CheckAppActivity;

            ItemsListBox.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
        }

        private void InitializeUI()
        {
            Locals.ApplyLocalization(this);
            Opacity = 0.0;

            _uiRegistry.InitializeClock();
            _uiRegistry.LoadUserAvatar();
        }

        private void CheckAppActivity(object sender, EventArgs e)
        {
            bool isActive = SystemProvider.IsForeground();

            if (isActive && !_wasActive)
            {
                OnBecameActive();
            }

            else if (!isActive && _wasActive)
            {
                OnBecameInactive();
            }

            _wasActive = isActive;
        }

        private void OnBecameActive()
        {
            _sound.PlayBackgroundMusic(Sound.FadeDurationMs * 3);
            SystemProvider.HideCursor();
            _uiRegistry.ShowBackground(true);

            _metadata.Stop();
            UpdatePlaytimeUI();

            if (_currentTab == TabType.LastPlayed)
            {
                _launcher.SortByLastPlayed();
            }
        }

        private void OnBecameInactive()
        {
            _sound.StopBackgroundMusicSafe(Sound.FadeDurationMs);
            _uiRegistry.ShowLoading(false);
            SystemProvider.ShowCursor();
            _uiRegistry.ShowBackground(false);
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
            _inputHandler.OnMoveSelection += _launcher.MoveSelection;
            // _inputHandler.OnLaunchSelected += _launcher.LaunchSelected;

            _inputHandler.OnLaunchSelected += () =>
            {
                if (_isExiting) return;

                _launcher.LaunchSelected();
            };

            _inputHandler.OnSwitchToGamelist += SwitchToGamelist;
            _inputHandler.OnSwitchToLastPlayed += SwitchToLastPlayed;

            _inputHandler.OnOpenSettings += async () =>
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

        private async void SwitchToGamelist()
        {
            if (_isTabSwitching || _currentTab == TabType.Gamelist) return;

            _sound?.PlayMoveSound();
            _isTabSwitching = true;

            try
            {
                _currentTab = TabType.Gamelist;

                TabsAnimation.AnimateTabText(_uiRegistry.TabGamelistText, true);
                TabsAnimation.AnimateTabText(_uiRegistry.TabLastPlayedText, false);

                await TabsAnimation.AnimateCarouselSwitch(
                    _uiRegistry.CarouselWrapper,
                    Dispatcher,
                    ActualWidth,
                    () => _launcher.SortDefault(),
                    -1);
            }

            finally
            {
                _isTabSwitching = false;
            }
        }

        private async void SwitchToLastPlayed()
        {
            if (_isTabSwitching || _currentTab == TabType.LastPlayed) return;

            _sound?.PlayMoveSound();
            _isTabSwitching = true;

            try
            {
                _currentTab = TabType.LastPlayed;

                TabsAnimation.AnimateTabText(_uiRegistry.TabLastPlayedText, true);
                TabsAnimation.AnimateTabText(_uiRegistry.TabGamelistText, false);

                await TabsAnimation.AnimateCarouselSwitch(
                    _uiRegistry.CarouselWrapper,
                    Dispatcher,
                    ActualWidth,
                    () => _launcher.SortByLastPlayed(),
                    1);
            }

            finally
            {
                _isTabSwitching = false;
            }
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
            if (ItemsListBox.ItemContainerGenerator.Status ==
                System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                InitializeCarouselSelectedItem();
            }
        }

        private void UpdateUiScaleResources(double windowHeight)
        {
            LayoutScaler.ApplyUiScaleToDictionary(this.Resources, windowHeight);
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
            {
                _launcher.HandleSelectionChangedAndAnimate(listBox, e);
            }

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

            fadeIn.Completed += (s, args) => _uiRegistry.ShowBackground(true);
            baseGrid.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void ShowStartupNotifications()
        {
            // Debug build notification
            _uiRegistry.Notifications.Debug(0, 5);

            // Xbox Game Bar notification
            bool gamebarInstalled = SystemProvider.IsXboxGameBarInstalled();

            if (_config.ShouldShowGamebarNotification(gamebarInstalled))
            {
                _uiRegistry.Notifications.GameBar(1, 5, gamebarInstalled);
                _config.MarkGamebarNotificationShown(gamebarInstalled);
            }

            // HotSwap notification
            if (_config.ShouldShowHotSwapNotification(_hotSwapLaunch))
            {
                if (_hotSwapLaunch)
                {
                    _uiRegistry.Notifications.HotSwap(2, 8);
                }

                _config.MarkHotSwapNotificationShown(_hotSwapLaunch);
            }
        }

        private void StartApplication()
        {
            SystemProvider.SetDisplaySleepBlocked(true);

            _activityTimer.Start();
            SetupInputEvents();

            KeyDown += MainWindow_KeyDown;
            KeyUp += MainWindow_KeyUp;

            _input.OnGamepadConnectionChanged += OnGamepadConnectionChanged;
            IsGamepadConnected = _input.IsGamepadConnected;
            _uiRegistry.RefreshHintIcons(IsGamepadConnected);
        }

        public void ExitApplication()
        {
            _isExiting = true;
            _isMainScreenActive = false;

            _display?.HandleHotswapOnExit();

            _sound.PlayBackSound();
            _sound.StopBackgroundMusicSafe(Sound.FadeDurationMs);

            _uiRegistry.ShowBackground(false);
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

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            double h = ActualHeight;

            if (h <= 0) return;

            UpdateUiScaleResources(h);
            ItemsListBox.InvalidateMeasure();
            UpdateLayout();
            InitializeCarouselSelectedItem();
            ItemsListBox.Items.Refresh();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            if (ActualHeight > 0)
            {
                UpdateUiScaleResources(ActualHeight);
                ItemsListBox?.Items.Refresh();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _isExiting = false;

            _parserOverlay?.Dispose();
            _input?.Dispose();
            _guideHandler?.Dispose();
            _sound?.Dispose();
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

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ActualHeight > 0)
                {
                    UpdateUiScaleResources(ActualHeight);
                    ItemsListBox?.Items.Refresh();
                }
            }));
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

        private void TestBackgroundPan()
        {
            if (!(BackgroundImage.Source is ImageSource source)) return;
            if (ActualWidth <= 0 || ActualHeight <= 0 || source.Width <= 0 || source.Height <= 0) return;

            double renderedHeight = ActualHeight;
            double renderedWidth = source.Width * (renderedHeight / source.Height);

            BackgroundImage.Width = renderedWidth;
            BackgroundImage.Height = renderedHeight;

            _backgroundPanOverflow = Math.Max(0, renderedWidth - ActualWidth);

            CompositionTarget.Rendering -= BackgroundPan_Rendering;

            if (_backgroundPanOverflow <= 0)
            {
                BackgroundTranslate.X = 0;
                return;
            }

            double startPosition = 0.15;

            _backgroundPanPosition = _backgroundPanOverflow * startPosition;
            _backgroundPanDirection = 1;
            _backgroundPanLastFrame = DateTime.UtcNow;

            BackgroundTranslate.X = -_backgroundPanPosition;

            CompositionTarget.Rendering += BackgroundPan_Rendering;
        }

        private void BackgroundPan_Rendering(object sender, EventArgs e)
        {
            if (_backgroundPanOverflow <= 0) return;

            DateTime now = DateTime.UtcNow;
            double delta = (now - _backgroundPanLastFrame).TotalSeconds;
            _backgroundPanLastFrame = now;

            if (delta <= 0 || delta > 0.1) return;

            const double speed = 5.0;
            const double edgeZone = 60.0;

            double distanceToEdge = _backgroundPanDirection > 0
                ? _backgroundPanOverflow - _backgroundPanPosition
                : _backgroundPanPosition;

            double edgeFactor = Math.Min(1.0, Math.Max(0.08, distanceToEdge / edgeZone));

            _backgroundPanPosition += speed * edgeFactor * _backgroundPanDirection * delta;

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
    }
}