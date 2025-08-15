using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EZ2Play.App;

namespace EZ2Play.App
{
    public class Launcher
    {
        private readonly bool _isHorizontalMode;
        private readonly FrameworkElement _window;
        private readonly EZ2Play.App.Sound _audioManager;
        private ShortcutInfo[] _shortcuts = Array.Empty<ShortcutInfo>();
        private int _selectedIndex = 0;

        public ShortcutInfo[] Shortcuts => _shortcuts;
        public int SelectedIndex => _selectedIndex;
        public bool IsHorizontalMode => _isHorizontalMode;

        public event Action<int> SelectionChanged;
        public event Action BackgroundUpdated;

        public Launcher(FrameworkElement window, bool isHorizontalMode, EZ2Play.App.Sound audioManager)
        {
            _window = window;
            _isHorizontalMode = isHorizontalMode;
            _audioManager = audioManager;
        }

        public void LoadShortcuts()
        {
            var listBoxName = _isHorizontalMode ? "HorizontalItemsListBox" : "VerticalItemsListBox";
            var itemsListBox = _window.FindName(listBoxName) as ListBox;
            
            _shortcuts = IconExtractor.LoadShortcuts(_isHorizontalMode);
            
            if (itemsListBox != null)
            {
                itemsListBox.ItemsSource = _shortcuts;
                if (_shortcuts.Length > 0) itemsListBox.SelectedIndex = 0;
            }
            
            UpdateEmptyState(_shortcuts.Length == 0);
            if (_shortcuts.Length > 0)
            {
                BackgroundUpdated?.Invoke();
                UpdateSelectedNameTopRight();
            }
            else
            {
                // Даже если нет ярлыков, обновляем фон для показа кастомного фона
                BackgroundUpdated?.Invoke();
            }
        }

        public void MoveSelection(int direction)
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
            var itemsListBox = _window.FindName(listBoxName) as ListBox;
            if (itemsListBox != null)
            {
                itemsListBox.SelectedIndex = _selectedIndex;
                itemsListBox.ScrollIntoView(itemsListBox.SelectedItem);
            }
            
            _audioManager?.PlayMoveSound();
            
            if (_window is Window window)
            {
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    BackgroundUpdated?.Invoke();
                }), DispatcherPriority.Background);
            }
        }

        public void LaunchSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _shortcuts.Length) return;

            try
            {
                _audioManager?.PlayLaunchSound();
                
                var shortcutPath = Path.Combine("shortcuts", $"{_shortcuts[_selectedIndex].Name}.lnk");
                Process.Start(new ProcessStartInfo
                {
                    FileName = shortcutPath,
                    UseShellExecute = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void HandleSelectionChanged(int newIndex)
        {
            _selectedIndex = newIndex;
            BackgroundUpdated?.Invoke();
            UpdateSelectedNameTopRight();
            SelectionChanged?.Invoke(newIndex);
        }

        private void UpdateEmptyState(bool isEmpty)
        {
            var bottomPanelName = _isHorizontalMode ? "HorizontalBottomPanel" : "VerticalBottomPanel";
            var topInfoPanelName = _isHorizontalMode ? "HorizontalTopInfoPanel" : "VerticalTopInfoPanel";
            
            var bottomPanel = _window.FindName(bottomPanelName) as Border;
            var noShortcutsMessage = _window.FindName("NoShortcutsMessage") as TextBlock;
            var topInfoPanel = _window.FindName(topInfoPanelName) as Grid;
            
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
                var selectedGameTitle = _window.FindName("HorizontalSelectedGameTitle") as TextBlock;
                if (selectedGameTitle != null)
                {
                    selectedGameTitle.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
                }
            }
        }

        private void UpdateSelectedNameTopRight()
        {
            if (_isHorizontalMode)
            {
                // В горизонтальном режиме обновляем название игры под списком
                var selectedGameTitle = _window.FindName("HorizontalSelectedGameTitle") as TextBlock;
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
        }


    }
} 