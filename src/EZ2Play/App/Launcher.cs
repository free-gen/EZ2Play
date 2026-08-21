using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace EZ2Play.App
{
    public class Launcher
    {
        private readonly ListBox _itemsListBox;
        private readonly TextBlock _selectedGameTitle;
        private readonly MainWindow _mainWindow;
        private readonly Sound _sound;

        private CarouselNavigation _navigation;
        private ShortcutInfo[] _defaultShortcuts = Array.Empty<ShortcutInfo>();
        private GameMetadata _metadata;
        private bool _launchCooldown;

        public ShortcutInfo[] Shortcuts => _navigation?.Shortcuts ?? Array.Empty<ShortcutInfo>();
        public int SelectedIndex => _navigation?.SelectedIndex ?? -1;
        public GameMetadata Playtime => _metadata;
        public bool SkipScaleUpAnimationOnEdgeScroll { get; set; }

        public event Action<int> SelectionChanged;

        public Launcher(ListBox itemsListBox, TextBlock selectedGameTitle, MainWindow mainWindow, Sound audioManager)
        {
            _itemsListBox = itemsListBox;
            _selectedGameTitle = selectedGameTitle;
            _mainWindow = mainWindow;
            _sound = audioManager;
            _metadata = new GameMetadata();
        }

        public void LoadShortcuts()
        {
            _defaultShortcuts = IconExtractor.LoadShortcuts();
            _navigation = new CarouselNavigation(_defaultShortcuts);

            ApplyVisibleWindow();
            UpdateSelectedName();
        }

        public void SortByLastPlayed()
        {
            if (_defaultShortcuts.Length == 0) return;

            var sorted = _defaultShortcuts
                .OrderByDescending(s => _metadata.GetLastPlayed(s.FullPath))
                .ToArray();

            _navigation = new CarouselNavigation(sorted);
            _navigation.ResetView();

            ApplyVisibleWindow();
            UpdateSelectedName();
        }

        public void SortDefault()
        {
            if (_defaultShortcuts.Length == 0) return;

            _navigation = new CarouselNavigation(_defaultShortcuts);
            _navigation.ResetView();

            ApplyVisibleWindow();
            UpdateSelectedName();
        }

        public void MoveSelection(int direction)
        {
            if (_navigation.IsEmpty) return;

            _navigation.MoveSelection(direction);
            _sound?.PlayMoveSound();

            bool windowScrolling = _navigation.IsWindowScrolling;
            ApplyVisibleWindow(updateItemsSource: windowScrolling);
        }

        public void HandleSelectionChanged(int visibleIndex)
        {
            if (_navigation.IsEmpty) return;

            int leftOffset = _navigation.HasLeftOverflow ? 1 : 0;
            _navigation.SetSelectedIndex(visibleIndex, leftOffset);

            UpdateSelectedName();
            SelectionChanged?.Invoke(_navigation.SelectedIndex);
        }

        public void HandleSelectionChangedAndAnimate(ListBox listBox, SelectionChangedEventArgs e)
        {
            if (listBox?.SelectedIndex < 0 || _navigation.IsEmpty) return;

            int currentVisibleIndex = listBox.SelectedIndex;
            int previousAbsoluteIndex = _navigation.SelectedIndex;

            HandleSelectionChanged(currentVisibleIndex);

            int currentAbsoluteIndex = _navigation.SelectedIndex;

            ApplySelectionAnimations(listBox, e, currentVisibleIndex, previousAbsoluteIndex, currentAbsoluteIndex);
        }

        public async void LaunchSelected()
        {
            if (_launchCooldown || _navigation.IsEmpty) return;

            _launchCooldown = true;

            try
            {
                _sound?.PlayLaunchSound();
                _mainWindow?.ShowLoadingUI(true);

                var shortcutPath = _navigation.Shortcuts[_navigation.SelectedIndex].FullPath;

                Process.Start(new ProcessStartInfo
                {
                    FileName = shortcutPath,
                    UseShellExecute = true
                });

                // Start the session only after Windows accepts the launch command.
                _metadata.Start(shortcutPath);
            }

            catch (Exception ex)
            {
                DebugLog.Error("Launcher", ex, "Failed to launch selected shortcut.");

                Application.Current?.Dispatcher.Invoke(() => _mainWindow?.ShowLoadingUI(false));
            }

            await Task.Delay(2000);
            _launchCooldown = false;
        }

        public void RefreshSelectedCover()
        {
            if (_navigation == null || _navigation.IsEmpty || _navigation.SelectedIndex < 0) return;

            var shortcut = _navigation.Shortcuts[_navigation.SelectedIndex];
            var cover = IconExtractor.GetCustomCover(shortcut.FullPath);

            if (cover == null) return;

            shortcut.Icon = cover;

            CarouselItem.ClearBrushCache();

            ApplyVisibleWindow();
            UpdateSelectedName();
        }

        private void ApplyVisibleWindow(bool updateItemsSource = true)
        {
            if (_itemsListBox == null || _navigation.IsEmpty) return;

            int centerCount = _navigation.GetCenterVisibleCount();
            CarouselLayout.HasLeftOverflow = _navigation.HasLeftOverflow;
            CarouselLayout.HasRightOverflow = _navigation.HasRightOverflow;

            if (updateItemsSource)
            {
                _itemsListBox.ItemsSource = null;
                _itemsListBox.ItemsSource = _navigation.GetVisibleShortcuts();
            }

            int visibleIndex = _navigation.GetSelectedVisibleIndex();

            if (visibleIndex >= 0 && visibleIndex < (_itemsListBox.Items?.Count ?? 0))
                _itemsListBox.SelectedIndex = visibleIndex;
        }

        private void UpdateSelectedName()
        {
            if (_selectedGameTitle == null || _navigation.IsEmpty) return;

            _selectedGameTitle.Text = _navigation.SelectedIndex >= 0
                ? _navigation.Shortcuts[_navigation.SelectedIndex].DisplayName
                : string.Empty;
        }

        private void ApplySelectionAnimations(ListBox listBox, SelectionChangedEventArgs e, int currentVisibleIndex, int previousAbsoluteIndex, int currentAbsoluteIndex)
        {
            bool wasWindowShift = _navigation.ConsumePendingWindowShiftAnimation();
            bool skipScaleUp = wasWindowShift && SkipScaleUpAnimationOnEdgeScroll;

            int fallbackPreviousIndex = _navigation.GetFallbackPreviousIndex(
                currentVisibleIndex,
                previousAbsoluteIndex,
                currentAbsoluteIndex,
                listBox.Items.Count);

            CarouselAnimation.AnimateSelectionChanged(listBox, e, fallbackPreviousIndex, skipScaleUp: skipScaleUp);
        }
    }
}