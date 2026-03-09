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
    public partial class MainWindow : FluentWindow
    {
        private Sound _audioManager;
        private Input _input;
        private Display _display;
        private UIState _uiState;
        private Launcher _launcher;

        private GuideExitHandler _guideHandler;

        public bool IsGamepadConnected { get; private set; }

        private DispatcherTimer _activityTimer;
        private bool _wasActive;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public MainWindow(bool hotSwap = false)
        {
            InitializeComponent();

            PreviewKeyDown += MainWindow_PreviewKeyDown; // Блокирует нажатие Tab

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            SizeChanged += OnWindowSizeChanged;
            Loaded += (s, e) => UpdateUiScaleResources(ActualHeight > 0 ? ActualHeight : LayoutScaler.ReferenceHeight);
            UpdateUiScaleResources(ActualHeight > 0 ? ActualHeight : LayoutScaler.ReferenceHeight);

            OptimizeListBoxPerformance();
            _uiState = new UIState(this);
            _audioManager = new Sound();
            _display = new Display(this, hotSwap, _audioManager);
            _launcher = new Launcher(this, _audioManager);
            _input = new Input();
            _guideHandler = new GuideExitHandler(_audioManager);

            _activityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            ItemsListBox.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
            _activityTimer.Tick += CheckAppActivity;

            _uiState.SetupLayoutMode();
            InitializeCarouselSelectedItem();
            Locals.ApplyLocalization(this);
            Opacity = 0.0;
            _uiState.InitializeTopRightInfo();

            if (hotSwap)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_display.HasMultipleDisplays)
                        _display.ToggleDisplay();
                }), DispatcherPriority.Background);
            }
        }

        private void CheckAppActivity(object sender, EventArgs e)
        {
            bool isActive = IsReallyForeground();

            if (isActive && !_wasActive)
            {
                // Приложение стало активным
                _audioManager.PlayBackgroundMusic(Sound.FadeDurationMs * 3);
                HideCursor();
            }
            else if (!isActive && _wasActive)
            {
                // Приложение стало неактивным
                _audioManager.StopBackgroundMusicSafe(Sound.FadeDurationMs);
                ShowLoading(false);
                ShowCursor();
            }

            _wasActive = isActive;
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
            _input.OnMoveSelection += _launcher.MoveSelection;
            _input.OnLaunchSelected += _launcher.LaunchSelected;
            _input.OnExitApplication += ExitApplication;
            
            if (_display.HasMultipleDisplays)
            {
                _input.OnToggleDisplay += _display.ToggleDisplay;
            }
        }

        public void ShowLoading(bool show)
        {
            var ring = FindName("LoadingProgress") as Wpf.Ui.Controls.ProgressRing;
            if (ring != null)
            {
                ring.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // Запускает музыку и управление после завершения сплеша.
        private void StartPostSplash()
        {
            _launcher.LoadShortcuts();
            _uiState.SetEmptyState(_launcher.Shortcuts.Length == 0);

            var baseGrid = this.FindName("BaseModeGrid") as Grid;
            if (baseGrid != null)
            {
                baseGrid.Visibility = Visibility.Visible;
                baseGrid.Opacity = 0;

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                baseGrid.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }

            _activityTimer.Start();
            SetupInputEvents();
            KeyDown += MainWindow_KeyDown;
            KeyUp += MainWindow_KeyUp;
            _input.OnGamepadConnectionChanged += OnGamepadConnectionChanged;
            IsGamepadConnected = _input.IsGamepadConnected;
            _uiState.RefreshHintIcons(IsGamepadConnected);
        }

        private void OnGamepadConnectionChanged(bool connected)
        {
            IsGamepadConnected = connected;
            _uiState.RefreshHintIcons(connected);
        }

        private void ItemsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == ItemsListBox)
                _launcher.HandleSelectionChangedAndAnimate(listBox, e);
        }

        private void OptimizeListBoxPerformance()
        {
            ItemsListBox.ManipulationBoundaryFeedback += (s, e) => e.Handled = true;
        }

        private void InitializeCarouselSelectedItem()
        {
            CarouselAnimation.InitializeSelectedItem(ItemsListBox);
        }

        private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {
            if (ItemsListBox.ItemContainerGenerator.Status ==
                System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                InitializeCarouselSelectedItem();
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            double h = ActualHeight;
            if (h <= 0) return;
            UpdateUiScaleResources(h);
            ItemsListBox.InvalidateMeasure();
            UpdateLayout();
            InitializeCarouselSelectedItem();

            ItemsListBox.Items.Refresh(); // исправление для обложек
        }

        private void UpdateUiScaleResources(double windowHeight)
        {
            LayoutScaler.ApplyUiScaleToDictionary(this.Resources, windowHeight);
        }

        private void HideCursor()
        {
            Mouse.OverrideCursor = Cursors.None;
        }

        private void ShowCursor()
        {
            Mouse.OverrideCursor = null;
        }

        private bool IsReallyForeground()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var foreground = GetForegroundWindow();
            return hwnd == foreground;
        }

        private void ExitApplication()
        {
            _audioManager.PlayBackSound();
            _audioManager.StopBackgroundMusicSafe(Sound.FadeDurationMs);

            _input.OnExitApplication -= ExitApplication;

            _uiState.ShowExitOverlay();
            
            Task.Delay(2000).ContinueWith(_ => 
            {
                Dispatcher.Invoke(Close);
            });
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

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            Dispatcher.BeginInvoke(new Action(() => {
                if (ActualHeight > 0)
                {
                    UpdateUiScaleResources(ActualHeight);
                    ItemsListBox?.Items.Refresh();
                }
            }));
        }

        // Tab не будет обрабатываться системой
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
            }
        }
    }
}