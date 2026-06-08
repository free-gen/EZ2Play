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
    // --------------- Координатор логики карусели ---------------

    public class Launcher
    {
        // --------------- Поля ---------------

        private readonly ListBox _itemsListBox;
        private readonly TextBlock _selectedGameTitle;
        private readonly MainWindow _mainWindow;
        private readonly Sound _sound;

        private CarouselNavigation _navigation;
        private GameMetadata _metadata;
        private bool _launchCooldown;

        // --------------- Публичные свойства ---------------

        public ShortcutInfo[] Shortcuts => _navigation?.Shortcuts ?? Array.Empty<ShortcutInfo>();
        public int SelectedIndex => _navigation?.SelectedIndex ?? -1;
        public GameMetadata Playtime => _metadata;
        public bool SkipScaleUpAnimationOnEdgeScroll { get; set; }

        // --------------- События ---------------

        public event Action<int> SelectionChanged;

        // --------------- Конструктор ---------------

        public Launcher(ListBox itemsListBox, TextBlock selectedGameTitle, 
                        MainWindow mainWindow, Sound audioManager)
        {
            _itemsListBox = itemsListBox;
            _selectedGameTitle = selectedGameTitle;
            _mainWindow = mainWindow;
            _sound = audioManager;
            _metadata = new GameMetadata();
        }

        // --------------- Загрузка и сортировка ---------------

        public void LoadShortcuts()
        {
            var shortcuts = IconExtractor.LoadShortcuts();
            _navigation = new CarouselNavigation(shortcuts);
            ApplyVisibleWindow();
            UpdateSelectedName();
        }

        public void SortByLastPlayed()
        {
            if (_navigation.IsEmpty) return;

            var sorted = _navigation.Shortcuts
                .OrderByDescending(s =>
                {
                    int seconds = _metadata.GetSeconds(s.FullPath);
                    return seconds > 0 ? _metadata.GetLastPlayed(s.FullPath) : DateTime.MinValue;
                })
                .ToArray();

            _navigation = new CarouselNavigation(sorted);
            _navigation.ResetView();
            ApplyVisibleWindow();
        }

        public void SortDefault()
        {
            LoadShortcuts();
        }

        // --------------- Навигация ---------------

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

            ApplySelectionAnimations(listBox, e, currentVisibleIndex, 
                                     previousAbsoluteIndex, currentAbsoluteIndex);
        }

        // --------------- Запуск игры ---------------

        public async void LaunchSelected()
        {
            if (_launchCooldown || _navigation.IsEmpty) return;
            _launchCooldown = true;

            try
            {
                _sound?.PlayLaunchSound();
                _mainWindow?.ShowLoadingUI(true);

                var shortcutPath = _navigation.Shortcuts[_navigation.SelectedIndex].FullPath;
                _metadata.Start(shortcutPath);

                Process.Start(new ProcessStartInfo
                {
                    FileName = shortcutPath,
                    UseShellExecute = true
                });
            }
            catch
            {
                Application.Current?.Dispatcher.Invoke(() => _mainWindow?.ShowLoadingUI(false));
            }

            await Task.Delay(2000);
            _launchCooldown = false;
        }

        // --------------- UI синхронизация ---------------

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

        private void ApplySelectionAnimations(ListBox listBox, SelectionChangedEventArgs e,
                                              int currentVisibleIndex, int previousAbsoluteIndex,
                                              int currentAbsoluteIndex)
        {
            bool wasWindowShift = _navigation.ConsumePendingWindowShiftAnimation();
            bool skipScaleUp = wasWindowShift && SkipScaleUpAnimationOnEdgeScroll;

            int fallbackPreviousIndex = _navigation.GetFallbackPreviousIndex(
                currentVisibleIndex, previousAbsoluteIndex, currentAbsoluteIndex,
                listBox.Items.Count);

            CarouselAnimation.AnimateSelectionChanged(listBox, e, fallbackPreviousIndex, 
                                                       skipScaleUp: skipScaleUp);
        }
    }
}