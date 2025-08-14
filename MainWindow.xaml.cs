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
        private bool _isHorizontalMode = false;
        private bool _isExternalDisplay = false;
        private bool _hasMultipleDisplays = false;
        private bool _wasLaunchedWithHotswap = false;
        private Sound _audioManager;
        private Input _Input;
        private SelectorAnimation _glowAnimation;
        private ImageSource _customBackgroundImage;
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
            
            _wasLaunchedWithHotswap = hotSwap;
            _isHorizontalMode = App.IsHorizontalMode;
            Log($"Application launched with hotswap: {_wasLaunchedWithHotswap}, horizontal mode: {_isHorizontalMode}");
            
            SetupLayoutMode();
            CheckMultipleDisplays();
            
            _audioManager = new Sound();
            _Input = new Input();
            _Input.SetHorizontalMode(_isHorizontalMode);
            _glowAnimation = new SelectorAnimation();
            App.GlowBrush = _glowAnimation.GetAnimatedBrush();
            App.HorizontalGlowBrush = _glowAnimation.GetHorizontalAnimatedBrush();
            LoadCustomBackgroundImage();
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

        private void SetupLayoutMode()
        {
            try
            {
                var verticalGrid = this.FindName("VerticalModeGrid") as Grid;
                var horizontalGrid = this.FindName("HorizontalModeGrid") as Grid;
                
                if (verticalGrid != null && horizontalGrid != null)
                {
                    if (_isHorizontalMode)
                    {
                        verticalGrid.Visibility = Visibility.Collapsed;
                        horizontalGrid.Visibility = Visibility.Visible;
                        Log("Switched to horizontal layout mode");
                    }
                    else
                    {
                        verticalGrid.Visibility = Visibility.Visible;
                        horizontalGrid.Visibility = Visibility.Collapsed;
                        Log("Using vertical layout mode");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error setting up layout mode: {ex.Message}");
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
                var bottomPanelName = _isHorizontalMode ? "HorizontalBottomPanel" : "VerticalBottomPanel";
                var bottomPanel = this.FindName(bottomPanelName) as Border;
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
                var listBoxName = _isHorizontalMode ? "HorizontalItemsListBox" : "VerticalItemsListBox";
                var itemsListBox = this.FindName(listBoxName) as ListBox;
                
                if (!Directory.Exists(shortcutsDir))
                {
                    Directory.CreateDirectory(shortcutsDir);
                    Log("Created shortcuts directory");
                    _shortcuts = Array.Empty<ShortcutInfo>();
                    if (itemsListBox != null)
                    {
                        itemsListBox.ItemsSource = _shortcuts;
                    }
                    UpdateEmptyState(true);
                    return;
                }

                var iconSize = _isHorizontalMode ? 256 : 64;
                _shortcuts = Directory.GetFiles(shortcutsDir, "*.lnk")
                    .Select(lnkPath => new ShortcutInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(lnkPath),
                        Icon = IconExtractor.GetIconForShortcut(lnkPath, iconSize)
                    })
                    .ToArray();

                if (itemsListBox != null)
                {
                    itemsListBox.ItemsSource = _shortcuts;
                    if (_shortcuts.Length > 0) itemsListBox.SelectedIndex = 0;
                }
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
                var bottomPanelName = _isHorizontalMode ? "HorizontalBottomPanel" : "VerticalBottomPanel";
                var topInfoPanelName = _isHorizontalMode ? "HorizontalTopInfoPanel" : "VerticalTopInfoPanel";
                
                var bottomPanel = this.FindName(bottomPanelName) as Border;
                var noShortcutsMessage = this.FindName("NoShortcutsMessage") as TextBlock;
                var topInfoPanel = this.FindName(topInfoPanelName) as Grid;
                
                if (bottomPanel != null)
                {
                    bottomPanel.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
                }
                if (topInfoPanel != null)
                {
                    topInfoPanel.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
                }
                if (noShortcutsMessage != null)
                {
                    noShortcutsMessage.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
                }
                
                // Для горизонтального режима управляем видимостью названия игры
                if (_isHorizontalMode)
                {
                    var selectedGameTitle = this.FindName("HorizontalSelectedGameTitle") as TextBlock;
                    if (selectedGameTitle != null)
                    {
                        selectedGameTitle.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
                    }
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
                var appNameTextName = _isHorizontalMode ? "HorizontalTopRightAppNameText" : "VerticalTopRightAppNameText";
                var timeTextName = _isHorizontalMode ? "HorizontalTopRightTimeText" : "VerticalTopRightTimeText";
                
                var appNameTb = this.FindName(appNameTextName) as TextBlock;
                var timeTb = this.FindName(timeTextName) as TextBlock;
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
            var listBoxName = _isHorizontalMode ? "HorizontalItemsListBox" : "VerticalItemsListBox";
            var itemsListBox = this.FindName(listBoxName) as ListBox;
            if (itemsListBox != null)
            {
                itemsListBox.SelectedIndex = _selectedIndex;
                itemsListBox.ScrollIntoView(itemsListBox.SelectedItem);
            }
            
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
                var backgroundBrush = (ImageBrush)Resources["BackgroundBrush"];
                
                // Для вертикального режима используем размытый фон под списком
                Border listBoxBlurredBackground = null;
                ImageBrush blurredListBackgroundBrush = null;
                
                if (!_isHorizontalMode)
                {
                    listBoxBlurredBackground = this.FindName("VerticalListBoxBlurredBackground") as Border;
                    blurredListBackgroundBrush = this.FindName("VerticalBlurredListBackgroundBrush") as ImageBrush;
                }

                // Если используется пользовательский фон, устанавливаем его
                if (App.UseCustomBackground && _customBackgroundImage != null)
                {
                    backgroundBrush.ImageSource = _customBackgroundImage;
                    
                    // Показываем размытый фон под ListBox и устанавливаем тот же источник
                    if (listBoxBlurredBackground != null && blurredListBackgroundBrush != null)
                    {
                        blurredListBackgroundBrush.ImageSource = _customBackgroundImage;
                        listBoxBlurredBackground.Visibility = Visibility.Visible;
                    }
                    
                    Log("Using custom background image with blurred ListBox area");
                }
                // Иначе используем динамический фон от иконки ярлыка
                else if (_selectedIndex >= 0 && _selectedIndex < _shortcuts.Length)
                {
                    var selectedShortcut = _shortcuts[_selectedIndex];
                    if (selectedShortcut.Icon != null)
                    {
                        var blurredImage = CreateBlurredImage(selectedShortcut.Icon);
                        backgroundBrush.ImageSource = blurredImage;
                    }
                    
                    // Скрываем размытый фон под ListBox для динамического фона
                    if (listBoxBlurredBackground != null)
                    {
                        listBoxBlurredBackground.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    // Скрываем размытый фон если нет фона
                    if (listBoxBlurredBackground != null)
                    {
                        listBoxBlurredBackground.Visibility = Visibility.Collapsed;
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

        private void LoadCustomBackgroundImage()
        {
            if (!App.UseCustomBackground)
                return;

            try
            {
                string[] possibleFiles = { "bg.jpg", "bg.png" };
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;

                foreach (string fileName in possibleFiles)
                {
                    string fullPath = Path.Combine(appDirectory, fileName);
                    if (File.Exists(fullPath))
                    {
                        Log($"Found custom background: {fullPath}");
                        _customBackgroundImage = new BitmapImage(new Uri(fullPath));
                        break;
                    }
                }

                if (_customBackgroundImage == null)
                {
                    Log("Custom background requested but no bg.jpg or bg.png found in application directory");
                }
            }
            catch (Exception ex)
            {
                Log($"Error loading custom background: {ex.Message}");
                _customBackgroundImage = null;
            }
        }

        private void ItemsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox != null && listBox.SelectedIndex >= 0)
            {
                _selectedIndex = listBox.SelectedIndex;
                UpdateBackground();
                UpdateSelectedNameTopRight();
            }
        }

        private void UpdateSelectedNameTopRight()
        {
            try
            {
                if (_isHorizontalMode)
                {
                    // В горизонтальном режиме обновляем название игры под списком
                    var selectedGameTitle = this.FindName("HorizontalSelectedGameTitle") as TextBlock;
                    if (selectedGameTitle != null)
                    {
                        if (_selectedIndex >= 0 && _selectedIndex < _shortcuts.Length)
                        {
                            selectedGameTitle.Text = _shortcuts[_selectedIndex].Name;
                        }
                        else
                        {
                            selectedGameTitle.Text = string.Empty;
                        }
                    }
                }
                // В вертикальном режиме можно было бы обновлять SelectedNameText, но он закомментирован в оригинальном XAML
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
                var listBoxName = _isHorizontalMode ? "HorizontalItemsListBox" : "VerticalItemsListBox";
                var bottomPanelName = _isHorizontalMode ? "HorizontalBottomPanel" : "VerticalBottomPanel";
                var topInfoPanelName = _isHorizontalMode ? "HorizontalTopInfoPanel" : "VerticalTopInfoPanel";
                
                var itemsListBox = this.FindName(listBoxName) as ListBox;
                var bottomPanel = this.FindName(bottomPanelName) as Border;
                var topInfoPanel = this.FindName(topInfoPanelName) as Grid;
                var noShortcutsMessage = this.FindName("NoShortcutsMessage") as TextBlock;
                var listBoxBlurredBackground = this.FindName("VerticalListBoxBlurredBackground") as Border;
                var selectedGameTitle = this.FindName("HorizontalSelectedGameTitle") as TextBlock;
                var exitOverlay = this.FindName("ExitOverlay") as Border;

                if (itemsListBox != null) itemsListBox.Visibility = Visibility.Collapsed;
                if (bottomPanel != null) bottomPanel.Visibility = Visibility.Collapsed;
                if (topInfoPanel != null) topInfoPanel.Visibility = Visibility.Collapsed;
                if (selectedGameTitle != null) selectedGameTitle.Visibility = Visibility.Collapsed;
                if (listBoxBlurredBackground != null) listBoxBlurredBackground.Visibility = Visibility.Collapsed;
                if (noShortcutsMessage != null) noShortcutsMessage.Visibility = Visibility.Collapsed;

                // Показываем блюр overlay
                if (exitOverlay != null)
                {
                    exitOverlay.Visibility = Visibility.Visible;
                    
                    // Копируем текущий фон для блюра
                    var backgroundBrush = (ImageBrush)Resources["BackgroundBrush"];
                    var exitBlurredBackgroundBrush = this.FindName("ExitBlurredBackgroundBrush") as ImageBrush;
                    var exitBlurEffect = this.FindName("ExitBlurEffect") as BlurEffect;
                    
                    if (exitBlurredBackgroundBrush != null && backgroundBrush != null)
                    {
                        exitBlurredBackgroundBrush.ImageSource = backgroundBrush.ImageSource;
                        
                        // Анимация появления фона
                        var backgroundAnimation = new DoubleAnimation
                        {
                            From = 0.0,
                            To = 1.0,
                            Duration = TimeSpan.FromMilliseconds(300),
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };
                        
                        exitBlurredBackgroundBrush.BeginAnimation(ImageBrush.OpacityProperty, backgroundAnimation);
                    }
                    
                    // Анимация блюра
                    if (exitBlurEffect != null)
                    {
                        var blurAnimation = new DoubleAnimation
                        {
                            From = 0.0,
                            To = 50.0,
                            Duration = TimeSpan.FromMilliseconds(600),
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };
                        
                        exitBlurEffect.BeginAnimation(BlurEffect.RadiusProperty, blurAnimation);
                    }
                }

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

                    exitMessageText.BeginAnimation(UIElement.OpacityProperty, animation);
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
            // Если приложение было запущено с --hotswap, переключаем монитор обратно при выходе
            if (_wasLaunchedWithHotswap && _hasMultipleDisplays)
            {
                try
                {
                    Log("Application was launched with hotswap, switching display back on exit");
                    
                    // Переключаем обратно на противоположный дисплей
                    var argument = _isExternalDisplay ? "/internal" : "/external";
                    
                    Log($"Switching display back to: {(_isExternalDisplay ? "internal" : "external")}");
                    
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
                    Log($"Error switching display back on exit: {ex.Message}");
                }
            }
            
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
                var bottomPanelName = _isHorizontalMode ? "HorizontalBottomPanel" : "VerticalBottomPanel";
                var bottomPanel = this.FindName(bottomPanelName) as Border;
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