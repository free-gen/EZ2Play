using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Interop;
using EZ2Play.App;

namespace EZ2Play
{
    public partial class MainWindow : Window
    {
        private const string LogFile = "EZ2Play.log";
        private EZ2Play.App.Sound _audioManager;
        private EZ2Play.App.Input _Input;
        private EZ2Play.App.SelectorAnimation _glowAnimation;
        private EZ2Play.App.Background _background;
        private EZ2Play.App.Display _display;
        private EZ2Play.App.UIState _uiState;
        private EZ2Play.App.Launcher _launcher;
        public bool IsGamepadConnected { get; private set; }
        [DllImport("user32.dll")]
        private static extern bool ShowCursor(bool bShow);

        public MainWindow(bool hotSwap = false)
        {
            InitializeComponent();
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            var isHorizontalMode = EZ2Play.Main.App.IsHorizontalMode;
            _uiState = new EZ2Play.App.UIState(this, isHorizontalMode);
            _audioManager = new EZ2Play.App.Sound();
            _display = new EZ2Play.App.Display(this, isHorizontalMode, hotSwap, _audioManager);
            _launcher = new EZ2Play.App.Launcher(this, isHorizontalMode, _audioManager);
            _uiState.SetupLayoutMode();
            _Input = new EZ2Play.App.Input();
            _Input.SetHorizontalMode(isHorizontalMode);
            _glowAnimation = new EZ2Play.App.SelectorAnimation();
            EZ2Play.Main.App.GlowBrush = _glowAnimation.GetAnimatedBrush();
            EZ2Play.Main.App.HorizontalGlowBrush = _glowAnimation.GetHorizontalAnimatedBrush();
            _background = new EZ2Play.App.Background();
            
            _launcher.LoadShortcuts();
            _launcher.BackgroundUpdated += UpdateBackground;
            _launcher.SelectionChanged += OnSelectionChanged;
            
            if (isHorizontalMode)
            {
                InitializeFirstCoverSize();
            }
            
            EZ2Play.App.Langs.ApplyLocalization(this);
            
            UpdateBackground();
            
            SetupInputEvents();

            Activated += (s, e) => HideCursor();
            Deactivated += (s, e) => ShowCursor();
            Opacity = 0.0;

            _uiState.InitializeTopRightInfo();

            if (hotSwap)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_display.HasMultipleDisplays)
                    {
                        _display.ToggleDisplay();
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void SetupInputEvents()
        {
            _Input.OnMoveSelection += _launcher.MoveSelection;
            _Input.OnLaunchSelected += _launcher.LaunchSelected;
            _Input.OnExitApplication += ExitApplication;
            _Input.OnGamepadConnectionChanged += HandleGamepadConnectionChanged;
            
            if (_display.HasMultipleDisplays)
            {
                _Input.OnToggleDisplay += _display.ToggleDisplay;
            }

            HandleGamepadConnectionChanged(_Input.IsGamepadConnected);
        }

        private void UpdateBackground()
        {
            var backgroundBrush = (ImageBrush)Resources["BackgroundBrush"];
            
            // Для вертикального режима используем размытый фон под списком
            Border listBoxBlurredBackground = null;
            
            if (!_launcher.IsHorizontalMode)
            {
                listBoxBlurredBackground = this.FindName("VerticalListBoxBlurredBackground") as Border;
            }

            // Вызов нового метода с 2 аргументами
            _background.UpdateBackground(backgroundBrush, listBoxBlurredBackground);
        }

        private void ItemsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox != null && listBox.SelectedIndex >= 0)
            {
                _launcher.HandleSelectionChanged(listBox.SelectedIndex);
                
                // Анимация обложек для горизонтального режима
                if (listBox == HorizontalItemsListBox)
                {
                    AnimateCoverSelection(listBox, e);
                }
            }
        }

        private void AnimateCoverSelection(ListBox listBox, SelectionChangedEventArgs e)
        {
            // Сбрасываем размер предыдущего выбранного элемента
            if (e.RemovedItems.Count > 0)
            {
                var removedItem = e.RemovedItems[0];
                var container = listBox.ItemContainerGenerator.ContainerFromItem(removedItem) as ListBoxItem;
                if (container != null)
                {
                    var coverImage = FindVisualChild<Image>(container, "CoverImage");
                    var opacityMaskBorder = FindVisualChild<Border>(container, "OpacityMaskBorder");
                    
                    if (coverImage != null && opacityMaskBorder != null)
                    {
                        CoverAnimation.AnimateSelection(coverImage, opacityMaskBorder, false);
                    }
                }
            }

            // Увеличиваем размер нового выбранного элемента
            if (e.AddedItems.Count > 0)
            {
                var addedItem = e.AddedItems[0];
                var container = listBox.ItemContainerGenerator.ContainerFromItem(addedItem) as ListBoxItem;
                if (container != null)
                {
                    var coverImage = FindVisualChild<Image>(container, "CoverImage");
                    var opacityMaskBorder = FindVisualChild<Border>(container, "OpacityMaskBorder");
                    
                    if (coverImage != null && opacityMaskBorder != null)
                    {
                        CoverAnimation.AnimateSelection(coverImage, opacityMaskBorder, true);
                    }
                }
            }
        }

        private T FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T element && element.Name == name)
                {
                    return element;
                }
                
                var result = FindVisualChild<T>(child, name);
                if (result != null) return result;
            }
            
            return null;
        }

        private T FindVisualChildByTag<T>(DependencyObject parent, object tag, bool requireVisible = true) where T : FrameworkElement
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T element)
                {
                    bool tagMatches = Equals(element.Tag, tag);
                    bool visibilityOk = !requireVisible || element.IsVisible;
                    if (tagMatches && visibilityOk)
                    {
                        return element;
                    }
                }

                var result = FindVisualChildByTag<T>(child, tag, requireVisible);
                if (result != null) return result;
            }

            return null;
        }

        private void InitializeFirstCoverSize()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (HorizontalItemsListBox.Items.Count > 0)
                {
                    var firstContainer = HorizontalItemsListBox.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                    if (firstContainer != null)
                    {
                        var coverImage = FindVisualChild<Image>(firstContainer, "CoverImage");
                        var opacityMaskBorder = FindVisualChild<Border>(firstContainer, "OpacityMaskBorder");
                        
                        if (coverImage != null)
                        {
                            CoverAnimation.SetSizeInstant(coverImage, opacityMaskBorder, true);
                        }
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void OnSelectionChanged(int newIndex)
        {
            UpdateBackground();
        }

        private void Log(string message)
        {
            if (EZ2Play.Main.App.EnableLogging)
            {
                try { File.AppendAllText(LogFile, $"[{DateTime.Now}] {message}\n"); }
                catch { /* Ignore */ }
            }
        }

        private void HideCursor()
        {
            ShowCursor(false);
        }

        private void ShowCursor()
        {
            ShowCursor(true);
        }

        private void ExitApplication()
        {
            _audioManager.PlayBackSound();
            
            _uiState.ShowExitOverlay();
            
            Task.Delay(2000).ContinueWith(_ => 
            {
                Dispatcher.Invoke(() => Close());
            });
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            HideCursor();
            _display.EnsureMaximizedAndRefreshLayout();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            _Input.HandleKeyDown(e.Key);
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            _Input.HandleKeyUp(e.Key);
            base.OnKeyUp(e);
        }

        public void ShowWithAnimation()
        {
            ShowInTaskbar = true;
            Visibility = Visibility.Visible;
            
            HideCursor();
            
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(800),
                EasingFunction = new System.Windows.Media.Animation.CubicEase 
                { 
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut 
                }
            };
            
            animation.Completed += (s, e) =>
            {
                Activate();
            };
            
            BeginAnimation(OpacityProperty, animation);
        }

        protected override void OnClosed(EventArgs e)
        {
            // Если приложение было запущено с --hotswap, переключаем монитор обратно при выходе
            _display?.HandleHotswapOnExit();
            
            _glowAnimation?.StopAnimation();
            _Input?.Dispose();
            _audioManager?.Dispose();
            _uiState?.Dispose();
            _display?.Dispose();
            ShowCursor();
            Log("Application closed");
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
            catch (Exception ex)
            {
                Log($"Error adding WndProc hook: {ex.Message}");
            }
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            _display.EnsureMaximizedAndRefreshLayout();
            UpdateBackground();
        }

        private void HandleGamepadConnectionChanged(bool isConnected)
        {
            IsGamepadConnected = isConnected;
            var bottomPanel = FindVisualChildByTag<Border>(this, "BottomPanel", true);
            if (bottomPanel?.Child is StackPanel mainStackPanel && mainStackPanel.Children.Count >= 3)
            {
                _uiState.UpdateHintItem(mainStackPanel.Children[0] as StackPanel, isConnected, "\uF093", "EnterKeyIcon", "GreenAccentColor");
                _uiState.UpdateHintItem(mainStackPanel.Children[1] as StackPanel, isConnected, "\uF094", "EscKeyIcon", "RedAccentColor");
                _uiState.UpdateHintItem(mainStackPanel.Children[2] as StackPanel, isConnected, "\uF096", "XKeyIcon", "BlueAccentColor");
                _uiState.UpdateHintItem(mainStackPanel.Children[3] as StackPanel, isConnected, "\uF095", "YKeyIcon", "YellowAccentColor");
            }
        }


    }
}