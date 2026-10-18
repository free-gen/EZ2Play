
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace EZ2Play.App
{
    /// <summary>
    /// Manages game shortcuts, carousel selection, sorting,
    /// game launching, and playtime tracking.
    /// </summary>
    /// <remarks>
    /// Uses CarouselNavigation to manage the selected game and
    /// visible item range, while CarouselAnimation handles
    /// selection transitions.
    /// </remarks>
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

        public event Action<int> SelectionChanged;

        public Launcher(ListBox itemsListBox, TextBlock selectedGameTitle, MainWindow mainWindow, Sound audioManager)
        {
            _itemsListBox = itemsListBox;
            _selectedGameTitle = selectedGameTitle;
            _mainWindow = mainWindow;
            _sound = audioManager;

            _metadata = new GameMetadata();
        }

        /// <summary>
        /// Loads available shortcuts and initializes the default
        /// carousel navigation state.
        /// </summary>
        public void LoadShortcuts()
        {
            _defaultShortcuts = IconExtractor.LoadShortcuts();
            _navigation = new CarouselNavigation(_defaultShortcuts);

            ApplyVisibleWindow();
            UpdateSelectedName();
        }

        /// <summary>
        /// Sorts shortcuts by their most recent launch time.
        /// </summary>
        public void SortByLastPlayed()
        {
            if (_defaultShortcuts.Length == 0) return;

            var sorted = _defaultShortcuts
                .OrderByDescending(s => _metadata.GetLastPlayed(s.FullPath))
                .ToArray();

            _navigation = new CarouselNavigation(sorted);

            ApplyVisibleWindow();
            UpdateSelectedName();
        }

        /// <summary>
        /// Restores the original shortcut ordering.
        /// </summary>
        public void SortDefault()
        {
            if (_defaultShortcuts.Length == 0) return;

            _navigation = new CarouselNavigation(_defaultShortcuts);

            ApplyVisibleWindow();
            UpdateSelectedName();
        }

        /// <summary>
        /// Moves the carousel selection in the specified direction.
        /// Updates the item source only when the visible window shifts.
        /// </summary>
        public void MoveSelection(int direction)
        {
            if (_navigation.IsEmpty) return;

            _navigation.MoveSelection(direction);
            _sound?.PlayMoveSound();

            bool windowScrolling = _navigation.IsWindowScrolling;

            ApplyVisibleWindow(updateItemsSource: windowScrolling);
        }

        /// <summary>
        /// Converts a visible ListBox index into the absolute
        /// shortcut index and notifies selection listeners.
        /// </summary>
        public void HandleSelectionChanged(int visibleIndex)
        {
            if (_navigation.IsEmpty) return;

            // Account for the hidden item preceding the visible range.
            int leftOffset = _navigation.HasLeftOverflow ? 1 : 0;

            _navigation.SetSelectedIndex(visibleIndex, leftOffset);

            UpdateSelectedName();
            SelectionChanged?.Invoke(_navigation.SelectedIndex);
        }

        /// <summary>
        /// Updates the selected shortcut and starts the corresponding
        /// carousel selection animation.
        /// </summary>
        public void HandleSelectionChangedAndAnimate(ListBox listBox, SelectionChangedEventArgs e)
        {
            if (listBox?.SelectedIndex < 0 || _navigation.IsEmpty) return;

            int currentVisibleIndex = listBox.SelectedIndex;
            int previousAbsoluteIndex = _navigation.SelectedIndex;

            HandleSelectionChanged(currentVisibleIndex);

            int currentAbsoluteIndex = _navigation.SelectedIndex;

            ApplySelectionAnimations(
                listBox,
                e,
                currentVisibleIndex,
                previousAbsoluteIndex,
                currentAbsoluteIndex);
        }

        /// <summary>
        /// Launches the currently selected shortcut and starts
        /// tracking its playtime after a successful process launch.
        /// </summary>
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

                Application.Current?.Dispatcher.Invoke(
                    () => _mainWindow?.ShowLoadingUI(false));
            }

            // Preserve the launch cooldown to prevent repeated launches.
            await Task.Delay(2000);

            _launchCooldown = false;
        }

        /// <summary>
        /// Reloads the custom cover for the currently selected
        /// shortcut and refreshes the visible carousel items.
        /// </summary>
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

        /// <summary>
        /// Synchronizes the visible shortcut range and selected
        /// ListBox item with CarouselNavigation.
        /// </summary>
        private void ApplyVisibleWindow(bool updateItemsSource = true)
        {
            if (_itemsListBox == null || _navigation.IsEmpty) return;

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

        /// <summary>
        /// Displays the name of the currently selected shortcut.
        /// </summary>
        private void UpdateSelectedName()
        {
            if (_selectedGameTitle == null || _navigation.IsEmpty) return;

            _selectedGameTitle.Text = _navigation.SelectedIndex >= 0
                ? _navigation.Shortcuts[_navigation.SelectedIndex].DisplayName
                : string.Empty;
        }

        /// <summary>
        /// Applies carousel selection animations while preserving
        /// the fallback index used during visible-window shifts.
        /// </summary>
        private void ApplySelectionAnimations(
            ListBox listBox,
            SelectionChangedEventArgs e,
            int currentVisibleIndex,
            int previousAbsoluteIndex,
            int currentAbsoluteIndex)
        {
            _navigation.ConsumePendingWindowShiftAnimation();

            int fallbackPreviousIndex = _navigation.GetFallbackPreviousIndex(
                currentVisibleIndex,
                previousAbsoluteIndex,
                currentAbsoluteIndex,
                listBox.Items.Count);

            CarouselAnimation.AnimateSelectionChanged(
                listBox,
                e,
                fallbackPreviousIndex);
        }
    }
}
