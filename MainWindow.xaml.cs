using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Text;
using System.Windows.Media.Animation;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Management;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Windows.Interop;

namespace EZ2Play
{
    public partial class MainWindow : Window
    {
        private const string LogFile = "EZ2Play.log";
        private ShortcutInfo[] _shortcuts = Array.Empty<ShortcutInfo>();
        private int _selectedIndex = 0;
        private bool _isExternalDisplay = false;
        private bool _hasMultipleDisplays = false;
        private Sound _audioManager;
        private Input _Input;
        private SelectorAnimation _glowAnimation;
        public bool IsGamepadConnected { get; private set; }
        [DllImport("user32.dll")]
        private static extern bool ShowCursor(bool bShow);

        public class ShortcutInfo
        {
            public string Name { get; set; }
            public ImageSource Icon { get; set; }
        }

        public MainWindow(bool hotSwap = false)
        {
            InitializeComponent();
            Log("MainWindow constructor started...");
            
            CheckMultipleDisplays();
            
            _audioManager = new Sound();
            _Input = new Input();
            _glowAnimation = new SelectorAnimation();
            App.GlowBrush = _glowAnimation.GetAnimatedBrush();
            LoadShortcuts();
            SetupInputEvents();

            Activated += (s, e) => HideCursor();
            Deactivated += (s, e) => ShowCursor();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            
            Opacity = 0.0;

            try
            {
                InitializeTopRightInfo();
            }
            catch (Exception ex)
            {
                Log($"TopRight info init error: {ex.Message}");
            }

            if (hotSwap)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_hasMultipleDisplays)
                    {
                        ToggleDisplay();
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void CheckMultipleDisplays()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBasicDisplayParams"))
                {
                    var monitors = searcher.Get();
                    int monitorCount = 0;
                    
                    foreach (ManagementObject monitor in monitors)
                    {
                        monitorCount++;
                    }
                    
                    _hasMultipleDisplays = monitorCount > 1;
                    Log($"Detected {monitorCount} monitor(s). Multiple displays: {_hasMultipleDisplays}");
                    
                    UpdateDisplayToggleVisibility();
                }
            }
            catch (Exception ex)
            {
                Log($"Error checking multiple displays: {ex.Message}");
                _hasMultipleDisplays = false;
                UpdateDisplayToggleVisibility();
            }
        }

        private void UpdateDisplayToggleVisibility()
        {
            try
            {
                var bottomPanel = this.FindName("BottomPanel") as Border;
                if (bottomPanel?.Child is StackPanel mainStackPanel)
                {
                    if (mainStackPanel.Children.Count >= 3)
                    {
                        var displayTogglePanel = mainStackPanel.Children[2] as StackPanel;
                        if (displayTogglePanel != null)
                        {
                            displayTogglePanel.Visibility = _hasMultipleDisplays ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating display toggle visibility: {ex.Message}");
            }
        }


        private void SetupInputEvents()
        {
            _Input.OnMoveSelection += MoveSelection;
            _Input.OnLaunchSelected += LaunchSelected;
            _Input.OnExitApplication += ExitApplication;
            _Input.OnGamepadConnectionChanged += HandleGamepadConnectionChanged;
            
            if (_hasMultipleDisplays)
            {
                _Input.OnToggleDisplay += ToggleDisplay;
            }

            // начальное состояние
            HandleGamepadConnectionChanged(_Input.IsGamepadConnected);
        }



        private void LoadShortcuts()
        {
            try
            {
                var shortcutsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shortcuts");
                if (!Directory.Exists(shortcutsDir))
                {
                    Directory.CreateDirectory(shortcutsDir);
                    Log("Created shortcuts directory");
                    _shortcuts = Array.Empty<ShortcutInfo>();
                    ItemsListBox.ItemsSource = _shortcuts;
                    UpdateEmptyState(true);
                    return;
                }

                _shortcuts = Directory.GetFiles(shortcutsDir, "*.lnk")
                    .Select(lnkPath => new ShortcutInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(lnkPath),
                        Icon = IconExtractor.GetIconForShortcut(lnkPath)
                    })
                    .ToArray();

                ItemsListBox.ItemsSource = _shortcuts;
                if (_shortcuts.Length > 0) ItemsListBox.SelectedIndex = 0;
                Log($"Loaded {_shortcuts.Length} shortcuts");
                
                UpdateEmptyState(_shortcuts.Length == 0);
                if (_shortcuts.Length > 0)
                {
                    UpdateBackground();
                    UpdateSelectedNameTopRight();
                }
            }
            catch (Exception ex) { Log($"Error loading shortcuts: {ex}"); }
        }

        private void UpdateEmptyState(bool isEmpty)
        {
            try
            {
                var bottomPanel = this.FindName("BottomPanel") as Border;
                var noShortcutsMessage = this.FindName("NoShortcutsMessage") as TextBlock;
                var topInfoPanel = this.FindName("TopInfoPanel") as Grid;
                var selectedNameText = this.FindName("SelectedNameText") as TextBlock;

                if (bottomPanel != null)
                {
                    bottomPanel.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
                }
                if (topInfoPanel != null)
                {
                    topInfoPanel.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
                }
                if (selectedNameText != null)
                {
                    selectedNameText.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
                }
                if (noShortcutsMessage != null)
                {
                    noShortcutsMessage.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating empty state: {ex.Message}");
            }
        }

        private DispatcherTimer _clockTimer;
        private string _appDisplayName;
        private string _appVersion;

        private void InitializeTopRightInfo()
        {
            _appDisplayName = AppInfo.GetProductName();
            _appVersion = AppInfo.GetVersion(shortFormat: true);
            UpdateTopRightText();

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => UpdateTopRightText();
            _clockTimer.Start();
        }

        private void UpdateTopRightText()
        {
            try
            {
                var appNameTb = this.FindName("TopRightAppNameText") as TextBlock;
                var timeTb = this.FindName("TopRightTimeText") as TextBlock;
                if (appNameTb == null || timeTb == null) return;
                
                var time = DateTime.Now.ToString("HH:mm");
                appNameTb.Text = $"{_appDisplayName} Launcher";
                timeTb.Text = time;
            }
            catch {}
        }

        private void MoveSelection(int direction)
        {
            var newIndex = _selectedIndex + direction;
            
            if (newIndex < 0)
            {
                newIndex = _shortcuts.Length - 1;
            }
            else if (newIndex >= _shortcuts.Length)
            {
                newIndex = 0;
            }
            
            _selectedIndex = newIndex;
            ItemsListBox.SelectedIndex = _selectedIndex;
            ItemsListBox.ScrollIntoView(ItemsListBox.SelectedItem);
            
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateBackground();
            }), System.Windows.Threading.DispatcherPriority.Background);
            
            _audioManager.PlayMoveSound();
        }

        private void UpdateBackground()
        {
            try
            {
                if (_selectedIndex >= 0 && _selectedIndex < _shortcuts.Length)
                {
                    var selectedShortcut = _shortcuts[_selectedIndex];
                    if (selectedShortcut.Icon != null)
                    {
                        var backgroundBrush = (ImageBrush)Resources["BackgroundBrush"];
                        var blurredImage = CreateBlurredImage(selectedShortcut.Icon);
                        backgroundBrush.ImageSource = blurredImage;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating background: {ex.Message}");
            }
        }

        private ImageSource CreateBlurredImage(ImageSource originalImage)
        {
            try
            {
                var renderTarget = new RenderTargetBitmap(
                    (int)originalImage.Width,
                    (int)originalImage.Height,
                    96, 96, PixelFormats.Pbgra32);

                var visual = new System.Windows.Controls.Image
                {
                    Source = originalImage,
                    Effect = new BlurEffect { Radius = 20 }
                };

                visual.Measure(new Size(originalImage.Width, originalImage.Height));
                visual.Arrange(new Rect(0, 0, originalImage.Width, originalImage.Height));

                renderTarget.Render(visual);
                return renderTarget;
            }
            catch (Exception ex)
            {
                Log($"Error creating blurred image: {ex.Message}");
                return originalImage;
            }
        }

        private void ItemsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ItemsListBox.SelectedIndex >= 0)
            {
                _selectedIndex = ItemsListBox.SelectedIndex;
                UpdateBackground();
                UpdateSelectedNameTopRight();
            }
        }

        private void UpdateSelectedNameTopRight()
        {
            try
            {
                var tb = this.FindName("SelectedNameText") as TextBlock;
                if (tb == null) return;
                if (_selectedIndex >= 0 && _selectedIndex < _shortcuts.Length)
                {
                    tb.Text = _shortcuts[_selectedIndex].Name;
                }
                else
                {
                    tb.Text = string.Empty;
                }
            }
            catch {}
        }

        private void Log(string message)
        {
            if (App.EnableLogging)
            {
                try { File.AppendAllText(LogFile, $"[{DateTime.Now}] {message}\n"); }
                catch { /* Ignore */ }
            }
        }

        private void HideCursor()
        {
            try
            {
                ShowCursor(false);
            }
            catch (Exception ex)
            {
                Log($"Error hiding cursor: {ex.Message}");
            }
        }

        private void ShowCursor()
        {
            try
            {
                ShowCursor(true);
            }
            catch (Exception ex)
            {
                Log($"Error showing cursor: {ex.Message}");
            }
        }

        private void ToggleDisplay()
        {
            if (!_hasMultipleDisplays)
            {
                Log("Display toggle requested but only one monitor detected");
                return;
            }

            try
            {
                _audioManager.PlayBackSound();
                
                _isExternalDisplay = !_isExternalDisplay;
                var argument = _isExternalDisplay ? "/external" : "/internal";
                
                Log($"Switching display to: {(_isExternalDisplay ? "external" : "internal")}");
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = "DisplaySwitch.exe",
                    Arguments = argument,
                    UseShellExecute = true,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                Log($"Display switch error: {ex.Message}");
                MessageBox.Show($"Failed to switch display: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LaunchSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _shortcuts.Length) return;

            try
            {
                _audioManager.PlayLaunchSound();
                
                var shortcutPath = Path.Combine("shortcuts", $"{_shortcuts[_selectedIndex].Name}.lnk");
                Log($"Launching: {_shortcuts[_selectedIndex].Name}");
                Process.Start(new ProcessStartInfo
                {
                    FileName = shortcutPath,
                    UseShellExecute = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                });
            }
            catch (Exception ex)
            {
                Log($"Launch error: {ex}");
                MessageBox.Show($"Failed to launch: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExitApplication()
        {
            _audioManager.PlayBackSound();
            
            ShowExitOverlay();
            
            Task.Delay(2000).ContinueWith(_ => 
            {
                Dispatcher.Invoke(() => Close());
            });
        }

        private void ShowExitOverlay()
        {
            try
            {
                var itemsListBox = this.FindName("ItemsListBox") as ListBox;
                var bottomPanel = this.FindName("BottomPanel") as Border;
                var topInfoPanel = this.FindName("TopInfoPanel") as Grid;
                var selectedNameText = this.FindName("SelectedNameText") as TextBlock;
                var noShortcutsMessage = this.FindName("NoShortcutsMessage") as TextBlock;

                if (itemsListBox != null) itemsListBox.Visibility = Visibility.Collapsed;
                if (bottomPanel != null) bottomPanel.Visibility = Visibility.Collapsed;
                if (topInfoPanel != null) topInfoPanel.Visibility = Visibility.Collapsed;
                if (selectedNameText != null) selectedNameText.Visibility = Visibility.Collapsed;
                if (noShortcutsMessage != null) noShortcutsMessage.Visibility = Visibility.Collapsed;

                var companyName = AppInfo.GetCompanyName();
                var companyNameRun = this.FindName("CompanyNameRun") as System.Windows.Documents.Run;
                if (companyNameRun != null)
                {
                    companyNameRun.Text = companyName;
                }

                var exitMessageText = this.FindName("ExitMessageText") as TextBlock;
                if (exitMessageText != null)
                {
                    exitMessageText.Visibility = Visibility.Visible;
                    exitMessageText.Opacity = 1.0;
                    
                    var animation = new DoubleAnimation
                    {
                        From = 0.0,
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(800),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };

                    animation.BeginAnimation(UIElement.OpacityProperty, animation);
                }
            }
            catch (Exception ex)
            {
                Log($"Error showing exit overlay: {ex.Message}");
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            HideCursor();
            EnsureMaximizedAndRefreshLayout();
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
            _glowAnimation?.StopAnimation();
            _Input?.Dispose();
            _audioManager?.Dispose();
            ShowCursor();
            Log("Application closed");
            try { SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged; } catch {}
            base.OnClosed(e);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                var source = (HwndSource)PresentationSource.FromVisual(this);
                source?.AddHook(WndProc);
            }
            catch (Exception ex)
            {
                Log($"Error adding WndProc hook: {ex.Message}");
            }
        }

        private const int WM_DISPLAYCHANGE = 0x007E;
        private const int WM_DPICHANGED = 0x02E0;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_DISPLAYCHANGE || msg == WM_DPICHANGED)
            {
                Dispatcher.BeginInvoke(new Action(EnsureMaximizedAndRefreshLayout), DispatcherPriority.Background);
            }
            return IntPtr.Zero;
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(EnsureMaximizedAndRefreshLayout), DispatcherPriority.Background);
        }

        private void EnsureMaximizedAndRefreshLayout()
        {
            try
            {
                WindowState = WindowState.Normal;
                Left = 0;
                Top = 0;
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
                UpdateLayout();
                WindowState = WindowState.Maximized;
                UpdateLayout();

                UpdateBackground();
                UpdateSelectedNameTopRight();
                UpdateTopRightText();
            }
            catch (Exception ex)
            {
                Log($"Error refreshing layout after display change: {ex.Message}");
            }
        }

        private void HandleGamepadConnectionChanged(bool isConnected)
        {
            IsGamepadConnected = isConnected;
            try
            {
                var bottomPanel = this.FindName("BottomPanel") as Border;
                if (bottomPanel?.Child is StackPanel mainStackPanel && mainStackPanel.Children.Count >= 3)
                {
                    UpdateHintItem(mainStackPanel.Children[0] as StackPanel, isConnected, "\uF093", "EnterKeyIcon", "GreenAccentColor");
                    UpdateHintItem(mainStackPanel.Children[1] as StackPanel, isConnected, "\uF094", "EscKeyIcon", "RedAccentColor");
                    UpdateHintItem(mainStackPanel.Children[2] as StackPanel, isConnected, "\uF096", "XKeyIcon", "BlueAccentColor");
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating hints: {ex.Message}");
            }
        }

        private void UpdateHintItem(StackPanel itemPanel, bool isGamepad, string iconGlyph, string geometryKey, string accentResourceKey)
        {
            if (itemPanel == null) return;
            if (itemPanel.Children.Count < 2) return;

            if (isGamepad)
            {
                TextBlock iconTextBlock = null;
                foreach (var child in itemPanel.Children.OfType<TextBlock>())
                {
                    if (child.Style?.ToString().Contains("HintIconStyle") == true || 
                        child.FontFamily?.Source == "Segoe MDL2 Assets")
                    {
                        iconTextBlock = child;
                        break;
                    }
                }
                
                if (iconTextBlock != null)
                {
                    iconTextBlock.Text = iconGlyph;
                    iconTextBlock.FontFamily = new FontFamily("Segoe MDL2 Assets");
                    iconTextBlock.Visibility = Visibility.Visible;
                    
                    if (!string.IsNullOrWhiteSpace(accentResourceKey) && TryFindResource(accentResourceKey) is Brush accentBrush)
                    {
                        iconTextBlock.Foreground = accentBrush;
                    }
                    iconTextBlock.ClearValue(TextBlock.FontSizeProperty);
                }
                
                foreach (var child in itemPanel.Children.OfType<System.Windows.Shapes.Path>())
                {
                    child.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                foreach (var child in itemPanel.Children.OfType<TextBlock>())
                {
                    if (child.Style?.ToString().Contains("HintIconStyle") == true || 
                        child.FontFamily?.Source == "Segoe MDL2 Assets")
                    {
                        child.Visibility = Visibility.Collapsed;
                        break;
                    }
                }
                
                System.Windows.Shapes.Path pathIcon = itemPanel.Children.OfType<System.Windows.Shapes.Path>().FirstOrDefault();
                if (pathIcon == null)
                {
                    pathIcon = new System.Windows.Shapes.Path
                    {
                        Height = 30,
                        Stretch = Stretch.Uniform,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    itemPanel.Children.Insert(0, pathIcon);
                }
                
                if (TryFindResource(geometryKey) is PathGeometry geometry)
                {
                    pathIcon.Data = geometry;
                    pathIcon.Fill = TryFindResource("PrimaryTextColor") as Brush ?? Brushes.White;
                    pathIcon.Visibility = Visibility.Visible;
                }
            }
        }
    }
}