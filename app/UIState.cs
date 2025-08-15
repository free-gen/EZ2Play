using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Linq;
using System.Windows.Documents;

namespace EZ2Play.App
{
    public class UIState
    {
        private readonly bool _isHorizontalMode;
        private readonly FrameworkElement _window;
        private DispatcherTimer _clockTimer;
        private string _appDisplayName;
        private string _appVersion;

        public UIState(FrameworkElement window, bool isHorizontalMode)
        {
            _window = window;
            _isHorizontalMode = isHorizontalMode;
        }

        public void InitializeTopRightInfo()
        {
            _appDisplayName = AppInfo.GetProductName();
            _appVersion = AppInfo.GetVersion(shortFormat: true);
            UpdateTopRightText();

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => UpdateTopRightText();
            _clockTimer.Start();
        }

        public void SetupLayoutMode()
        {
            var verticalGrid = _window.FindName("VerticalModeGrid") as Grid;
            var horizontalGrid = _window.FindName("HorizontalModeGrid") as Grid;
            
            if (verticalGrid != null && horizontalGrid != null)
            {
                if (_isHorizontalMode)
                {
                    verticalGrid.Visibility = Visibility.Collapsed;
                    horizontalGrid.Visibility = Visibility.Visible;
                }
                else
                {
                    verticalGrid.Visibility = Visibility.Visible;
                    horizontalGrid.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void UpdateTopRightText()
        {
            var appNameTextName = _isHorizontalMode ? "HorizontalTopRightAppNameText" : "VerticalTopRightAppNameText";
            var timeTextName = _isHorizontalMode ? "HorizontalTopRightTimeText" : "VerticalTopRightTimeText";
            
            var appNameTb = _window.FindName(appNameTextName) as TextBlock;
            var timeTb = _window.FindName(timeTextName) as TextBlock;
            if (appNameTb == null || timeTb == null) return;
            
            var time = DateTime.Now.ToString("HH:mm");
            appNameTb.Text = $"{_appDisplayName} Launcher";
            timeTb.Text = time;
        }

        public void ShowExitOverlay()
        {
            var listBoxName = _isHorizontalMode ? "HorizontalItemsListBox" : "VerticalItemsListBox";
            var bottomPanelName = _isHorizontalMode ? "HorizontalBottomPanel" : "VerticalBottomPanel";
            var topInfoPanelName = _isHorizontalMode ? "HorizontalTopInfoPanel" : "VerticalTopInfoPanel";
            
            var itemsListBox = _window.FindName(listBoxName) as ListBox;
            var bottomPanel = _window.FindName(bottomPanelName) as Border;
            var topInfoPanel = _window.FindName(topInfoPanelName) as Grid;
            var noShortcutsMessage = _window.FindName("NoShortcutsMessage") as TextBlock;
            var listBoxBlurredBackground = _window.FindName("VerticalListBoxBlurredBackground") as Border;
            var selectedGameTitle = _window.FindName("HorizontalSelectedGameTitle") as TextBlock;
            var exitOverlay = _window.FindName("ExitOverlay") as Border;

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
                var backgroundBrush = (_window as Window)?.Resources["BackgroundBrush"] as ImageBrush;
                var exitBlurredBackgroundBrush = _window.FindName("ExitBlurredBackgroundBrush") as ImageBrush;
                var exitBlurEffect = _window.FindName("ExitBlurEffect") as BlurEffect;
                
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
            var companyNameRun = _window.FindName("CompanyNameRun") as System.Windows.Documents.Run;
            if (companyNameRun != null)
            {
                companyNameRun.Text = companyName;
            }

            var exitMessageText = _window.FindName("ExitMessageText") as TextBlock;
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

        public void UpdateHintItem(StackPanel itemPanel, bool isGamepad, string iconGlyph, string geometryKey, string accentResourceKey)
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
                    
                    if (!string.IsNullOrWhiteSpace(accentResourceKey) && 
                        (_window as Window)?.TryFindResource(accentResourceKey) is Brush accentBrush)
                    {
                        iconTextBlock.Foreground = accentBrush;
                    }
                    iconTextBlock.ClearValue(TextBlock.FontSizeProperty);
                }
                
                foreach (var child in itemPanel.Children.OfType<Path>())
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
                
                Path pathIcon = itemPanel.Children.OfType<Path>().FirstOrDefault();
                if (pathIcon == null)
                {
                    pathIcon = new Path
                    {
                        Height = 30,
                        Stretch = Stretch.Uniform,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    itemPanel.Children.Insert(0, pathIcon);
                }
                
                if ((_window as Window)?.TryFindResource(geometryKey) is PathGeometry geometry)
                {
                    pathIcon.Data = geometry;
                    pathIcon.Fill = (_window as Window)?.TryFindResource("PrimaryTextColor") as Brush ?? Brushes.White;
                    pathIcon.Visibility = Visibility.Visible;
                }
            }
        }

        public void Dispose()
        {
            try
            {
                _clockTimer?.Stop();
            }
            catch { /* Ignore */ }
        }


    }
} 