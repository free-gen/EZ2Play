using System;
using System.Runtime.InteropServices;
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
        // --------------- Поля класса ---------------

        private Sound _audioManager;
        private Input _input;
        private Display _display;
        private UIState _uiState;
        private Launcher _launcher;
        private GuideExitHandler _guideHandler;

        private ParticlesCanvas _particlesCanvas;

        private DispatcherTimer _activityTimer;
        private bool _wasActive;
        private bool _wasHotSwapLaunch;

        // --------------- Публичные свойства ---------------

        public bool IsGamepadConnected { get; private set; }

        // --------------- Native импорты ---------------

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        // --------------- Конструктор ---------------

        // Инициализирует главное окно и все компоненты
        public MainWindow(bool hotSwap = false)
        {
            InitializeComponent();

            // Подписка на события окна
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            SizeChanged += OnWindowSizeChanged;
            Loaded += (s, e) => UpdateUiScaleResources(ActualHeight > 0 ? ActualHeight : LayoutScaler.ReferenceHeight);
            UpdateUiScaleResources(ActualHeight > 0 ? ActualHeight : LayoutScaler.ReferenceHeight);

            // Оптимизация ListBox
            OptimizeListBoxPerformance();

            // Инициализация компонентов
            _audioManager = new Sound();
            _wasHotSwapLaunch = hotSwap;
            _display = new Display(this, hotSwap, _audioManager);
            _input = new Input();
            _guideHandler = new GuideExitHandler(_audioManager);

            _particlesCanvas = FindName("particles") as ParticlesCanvas;

            // Инициализация UIState с прямыми ссылками на UI-элементы
            _uiState = new UIState
            {
                TopLeftAppName = FindName("TopLeftAppNameText") as System.Windows.Controls.TextBlock,
                TopRightTime = FindName("TopRightTimeText") as System.Windows.Controls.TextBlock,
                UserAvatar = FindName("UserAvatar") as System.Windows.Controls.Image,
                BottomPanel = FindName("BottomPanel") as System.Windows.Controls.Border,
                TopInfoPanel = FindName("TopInfoPanel") as System.Windows.Controls.Grid,
                NoShortcutsMessage = FindName("NoShortcutsMessage") as System.Windows.Controls.TextBlock,
                SelectedGameTitle = FindName("SelectedGameTitle") as System.Windows.Controls.TextBlock,
                GameSourceCard = FindName("GameSourceCard") as System.Windows.Controls.Border,
                VersionLabel = FindName("VersionLabel") as System.Windows.Controls.TextBlock,
                SplashLogo = FindName("SplashLogo") as System.Windows.Controls.Image,
                SplashOverlay = FindName("SplashOverlay") as System.Windows.Controls.Grid,
                MainScreenGrid = FindName("MainScreenGrid") as System.Windows.Controls.Grid,
                ExitMessageText = FindName("ExitMessageText") as System.Windows.Controls.TextBlock,
                CompanyNameRun = FindName("CompanyNameRun") as System.Windows.Documents.Run,
                LaunchIconXinput = FindName("LaunchIconXinput") as System.Windows.FrameworkElement,
                LaunchIconKeyboard = FindName("LaunchIconKeyboard") as System.Windows.FrameworkElement,
                ExitIconGamepad = FindName("ExitIconGamepad") as System.Windows.FrameworkElement,
                ExitIconKeyboard = FindName("ExitIconKeyboard") as System.Windows.FrameworkElement,
                ScreenSwapIconGamepad = FindName("ScreenSwapIconGamepad") as System.Windows.FrameworkElement,
                ScreenSwapIconKeyboard = FindName("ScreenSwapIconKeyboard") as System.Windows.FrameworkElement,
                SystemMessage = FindName("SystemMessage") as System.Windows.Controls.Border,
                SystemMessageText = FindName("SystemMessageText") as System.Windows.Controls.TextBlock,
                BackgroundImage = FindName("BackgroundImage") as System.Windows.Controls.Image,
                ItemsListBox = ItemsListBox
            };

            // Управление фоном
            bool hasImage = _uiState.LoadBackgroundImage();
            _uiState.SetParticlesCanvas(_particlesCanvas);

            // Инициализация Launcher с прямыми ссылками
            _launcher = new Launcher(ItemsListBox, _uiState.SelectedGameTitle, this, _audioManager);

            // Настройка панели переключения дисплея
            SetupDisplayTogglePanel();

            // Таймер активности
            _activityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            ItemsListBox.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
            _activityTimer.Tick += CheckAppActivity;

            // Инициализация карусели
            InitializeCarouselSelectedItem();

            // Локализация
            Locals.ApplyLocalization(this);

            // Начальная прозрачность
            Opacity = 0.0;

            // Инициализация UI
            _uiState.InitializeToInfoPanel();
            _uiState.UserImage();
        }

        // --------------- Настройка компонентов ---------------

        // Настраивает панель переключения дисплея
        private void SetupDisplayTogglePanel()
        {
            if (_uiState.BottomPanel?.Child is System.Windows.Controls.StackPanel mainStackPanel && mainStackPanel.Children.Count >= 3)
            {
                var displayTogglePanel = mainStackPanel.Children[2] as System.Windows.Controls.StackPanel;
                if (displayTogglePanel != null)
                {
                    _display.SetDisplayTogglePanel(displayTogglePanel);
                }
            }
        }

        // --------------- Управление активностью ---------------

        // Проверяет активность приложения и управляет музыкой/курсором
        private void CheckAppActivity(object sender, EventArgs e)
        {
            bool isActive = IsReallyForeground();

            if (isActive && !_wasActive)
            {
                _audioManager.PlayBackgroundMusic(Sound.FadeDurationMs * 3);
                HideCursor();
                
                _uiState.ShowBackground(true);
            }
            else if (!isActive && _wasActive)
            {
                _audioManager.StopBackgroundMusicSafe(Sound.FadeDurationMs);
                ShowLoading(false);
                ShowCursor();
                
                _uiState.ShowBackground(false);
            }

            _wasActive = isActive;
        }

        // --------------- Обработка ввода ---------------

        // Обработчик нажатия клавиш
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            _input.HandleKeyDown(e.Key);
        }

        // Обработчик отпускания клавиш
        private void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            _input.HandleKeyUp(e.Key);
        }

        // Настраивает события ввода
        private void SetupInputEvents()
        {
            _input.OnMoveSelection += _launcher.MoveSelection;
            _input.OnLaunchSelected += _launcher.LaunchSelected;
            _input.OnExitApplication += ExitApplication;

            if (_display.HasMultipleDisplays)
            {
                _input.OnToggleDisplay += _display.ToggleDisplay;
            }
        }

        // --------------- Управление UI ---------------

        // Показывает или скрывает индикатор загрузки
        public void ShowLoading(bool show)
        {
            var ring = FindName("LoadingProgress") as Wpf.Ui.Controls.ProgressRing;
            if (ring != null)
            {
                ring.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // --------------- Пост-сплеш инициализация ---------------

        // Запускает музыку и управление после завершения сплеша
        private void StartPostSplash()
        {   
            _launcher.LoadShortcuts();
            
            bool isEmpty = _launcher.Shortcuts.Length == 0;
            _uiState.SetEmptyState(isEmpty);

            // Показываем MainScreenGrid только если есть ярлыки
            if (!isEmpty)
            {
                var baseGrid = FindName("MainScreenGrid") as System.Windows.Controls.Grid;
                if (baseGrid != null)
                {
                    baseGrid.Visibility = Visibility.Visible;
                    baseGrid.Opacity = 0;

                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };

                    fadeIn.Completed += (s, args) =>
                    {
                        _uiState.ShowBackground(true);
                    };

                    baseGrid.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                    // Debug Notification
                    // _uiState.Notification.Debug(1, 10);

                    if (_wasHotSwapLaunch)
                    {
                        _uiState.Notification.HotSwap(1, 10);
                    }
                }
            }

            _activityTimer.Start();
            SetupInputEvents();
            KeyDown += MainWindow_KeyDown;
            KeyUp += MainWindow_KeyUp;
            _input.OnGamepadConnectionChanged += OnGamepadConnectionChanged;
            IsGamepadConnected = _input.IsGamepadConnected;
            _uiState.RefreshHintIcons(IsGamepadConnected);
        }

        // --------------- Обработчики событий ---------------

        // Обработчик изменения подключения геймпада
        private void OnGamepadConnectionChanged(bool connected, string deviceName)
        {
            IsGamepadConnected = connected;
            _uiState.RefreshHintIcons(connected);

            // Если геймпад подключился, показываем уведомление с его именем
            if (connected)
            {
                _uiState.Notification.HotPlug(0, 5, deviceName);
            }
        }

        // Обработчик изменения выбора в ListBox
        private void ItemsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as System.Windows.Controls.ListBox;
            if (listBox == ItemsListBox)
                _launcher.HandleSelectionChangedAndAnimate(listBox, e);
        }

        // Оптимизирует производительность ListBox
        private void OptimizeListBoxPerformance()
        {
            ItemsListBox.ManipulationBoundaryFeedback += (s, e) => e.Handled = true;
        }

        // Инициализирует выбранный элемент карусели
        private void InitializeCarouselSelectedItem()
        {
            CarouselAnimation.InitializeSelectedItem(ItemsListBox);
        }

        // Обработчик завершения генерации контейнеров ListBox
        private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {
            if (ItemsListBox.ItemContainerGenerator.Status ==
                System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                InitializeCarouselSelectedItem();
        }

        // Обработчик изменения размера окна
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

        // Обновляет ресурсы масштабирования UI
        private void UpdateUiScaleResources(double windowHeight)
        {
            LayoutScaler.ApplyUiScaleToDictionary(this.Resources, windowHeight);
        }

        // --------------- Управление курсором ---------------

        // Скрывает курсор
        private void HideCursor()
        {
            Mouse.OverrideCursor = Cursors.None;
        }

        // Показывает курсор
        private void ShowCursor()
        {
            Mouse.OverrideCursor = null;
        }

        // --------------- Состояние окна ---------------

        // Проверяет, является ли окно активным foreground-окном
        private bool IsReallyForeground()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var foreground = GetForegroundWindow();
            return hwnd == foreground;
        }

        // --------------- Управление приложением ---------------

        // Выход из приложения с анимацией
        private void ExitApplication()
        {
            _audioManager.PlayBackSound();
            _audioManager.StopBackgroundMusicSafe(Sound.FadeDurationMs);
            
            _uiState.ShowBackground(false);

            _input.OnExitApplication -= ExitApplication;
            _uiState.ShowExitOverlay();

            Task.Delay(2000).ContinueWith(_ =>
            {
                Dispatcher.Invoke(Close);
            });
        }

        // Показывает окно с анимацией
        public void ShowWithAnimation(bool skipSplash = false)
        {
            ShowInTaskbar = true;
            HideCursor();

            _uiState.ShowWithAnimation(skipSplash, () =>
            {
                StartPostSplash();
                Activate();
            });
        }

        // --------------- Переопределения жизненного цикла окна ---------------

        // Обработчик активации окна
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (ActualHeight > 0)
            {
                UpdateUiScaleResources(ActualHeight);
                ItemsListBox?.Items.Refresh();
            }
        }

        // Обработчик закрытия окна
        protected override void OnClosed(EventArgs e)
        {
            _display?.HandleHotswapOnExit();
            _input?.Dispose();
            _guideHandler?.Dispose();
            _audioManager?.Dispose();
            _uiState?.Dispose();
            _display?.Dispose();
            ShowCursor();
            base.OnClosed(e);
        }

        // Инициализация источника окна (hook для WndProc)
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                var source = (HwndSource)PresentationSource.FromVisual(this);
                source?.AddHook(_display.WndProc);
            }
            catch { }
        }

        // Обработчик изменения DPI
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

        // Блокирует нажатие Tab (не будет обрабатываться системой)
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
            }
        }
    }
}