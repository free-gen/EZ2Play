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
    // --------------- Главное окно приложения ---------------

    public partial class MainWindow : FluentWindow
    {
        // --------------- Поля ---------------

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
        private bool _wasActive;
        private bool _hotSwapLaunch;
        private bool _isExiting;

        private enum TabType { Gamelist, LastPlayed }
        private TabType _currentTab = TabType.Gamelist;

        private SettingsOverlay _settingsOverlay;

        // --------------- Публичные свойства ---------------

        public bool IsGamepadConnected { get; private set; }

        public bool IsHotSwapLaunch() => _hotSwapLaunch;

        public Display GetDisplay() => _display;
        public AppConfig GetConfig() => _config;

        public void ShowLoadingUI(bool show)
        {
            _uiRegistry.ShowLoading(show);
        }

        // --------------- Конструктор ---------------

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

            // Применить сохранённые настройки дисплея сразу (работает криво без применения масштабирования)
            // if (_config.ForceDisplayEnabled)
            // {
            //     string target = _config.ForceDisplayIndex == 1 ? "/external" : "/internal";
            //     _display.RunDisplaySwitch(target);
            //     _display.SetCurrentDisplayIndex(_config.ForceDisplayIndex);
            // }
        }

        // --------------- Инициализация ---------------

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
            OverlayHost.Content = _settingsOverlay;
            _settingsOverlay.Visibility = Visibility.Collapsed;
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
                BottomHintPanel = FindName("BottomPanel") as HintPanel,
                NotificationPanel = FindName("NotificationPanel") as System.Windows.Controls.Border,
                NotificationText = FindName("NotificationText") as System.Windows.Controls.TextBlock,
                BackgroundImage = FindName("BackgroundImage") as System.Windows.Controls.Image,
                GameCounterText = FindName("GameCounterText") as System.Windows.Controls.TextBlock,
                GameCounterCard = FindName("GameCounterCard") as System.Windows.Controls.Border,
                ItemsListBox = ItemsListBox
            };

            _uiRegistry.InitializeSplash(SplashLogo, SplashOverlay, MainScreenGrid);
            _uiRegistry.InitializeNotifications(NotificationPanel, NotificationText);
            _uiRegistry.CarouselWrapper = FindName("CarouselWrapper") as System.Windows.Controls.Grid;
            _uiRegistry.LoadBackgroundImage();
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
            BottomPanel.Mode = mode;
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

        // --------------- Управление активностью ---------------

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

        // --------------- Ввод с клавиатуры ---------------

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
                _settingsOverlay.Open();
            };

            _inputHandler.OnSettingsBack += () => _settingsOverlay.Close();
            _inputHandler.OnSettingsConfirm += () => _settingsOverlay.Confirm();

            _inputHandler.OnSettingsNavigate += (dir) => _settingsOverlay.Navigate(dir, true);
            _inputHandler.OnSettingsNavigateVertical += (dir) => _settingsOverlay.Navigate(dir, false);
        }

        // --------------- Вкладки ---------------

        private async void SwitchToGamelist()
        {
            if (_currentTab == TabType.Gamelist) return;
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

        private async void SwitchToLastPlayed()
        {
            if (_currentTab == TabType.LastPlayed) return;
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

        // --------------- Обновление UI счетчика ---------------

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

        // --------------- UI управление ---------------

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

        // --------------- Обработчики событий от компонентов ---------------

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

        // --------------- Пост-сплеш инициализация ---------------

        private void StartPostSplash()
        {
            _launcher.LoadShortcuts();
            
            bool isEmpty = _launcher.Shortcuts.Length == 0;
            _uiRegistry.SetEmptyState(isEmpty);

            if (!isEmpty)
            {
                ShowMainScreenWithAnimation();
                ShowStartupNotifications();
            }

            StartApplication();
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
            // Debug notification
            // _uiRegistry.Notifications.Debug(0, 30);

            // Уведомление XboxGameBar
            bool gamebarInstalled = SystemProvider.IsXboxGameBarInstalled();
            if (_config.ShouldShowGamebarNotification(gamebarInstalled))
            {
                _uiRegistry.Notifications.GameBar(1, 5, gamebarInstalled);
                _config.MarkGamebarNotificationShown(gamebarInstalled);
            }

            // Уведомление HotSwap
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
            _activityTimer.Start();
            SetupInputEvents();
            
            KeyDown += MainWindow_KeyDown;
            KeyUp += MainWindow_KeyUp;
            
            _input.OnGamepadConnectionChanged += OnGamepadConnectionChanged;
            IsGamepadConnected = _input.IsGamepadConnected;
            _uiRegistry.RefreshHintIcons(IsGamepadConnected);
        }

        // --------------- Управление приложением ---------------

        public void ExitApplication()
        {
            _isExiting = true;
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
            });
        }

        // --------------- Размеры и масштабирование ---------------

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

        // --------------- Жизненный цикл окна ---------------

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
            
            _input?.Dispose();
            _guideHandler?.Dispose();
            _sound?.Dispose();
            _uiRegistry?.Dispose();
            _display?.Dispose();
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
            catch { }
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

        // --------------- Блокировка ввода ---------------

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
            }
        }
    }
}