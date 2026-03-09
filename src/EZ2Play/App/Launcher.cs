using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows.Threading;

// Координатор логики карусели и выбора

namespace EZ2Play.App
{
    public class Launcher
    {
        // ---- Core state ----
        private readonly FrameworkElement _window;
        private readonly Sound _audioManager;
        private ShortcutInfo[] _shortcuts = Array.Empty<ShortcutInfo>();
        private int _selectedIndex = 0;
        private int _visibleWindowStart = 0;
        private bool _pendingWindowShiftAnimation;
        private int _pendingMoveDirection;

        private bool _launchCooldown = false;

        // ---- Public read model ----
        public ShortcutInfo[] Shortcuts => _shortcuts;
        public int SelectedIndex => _selectedIndex;

        // Master switch для scale-анимаций карусели.
        // public bool EnableSelectionAnimations { get; set; } = true;

        // пропуск анимации увеличения для крайних элементов
        public bool SkipScaleUpAnimationOnEdgeScroll { get; set; } = false;

        private bool ConsumePendingWindowShiftAnimation()
        {
            bool value = _pendingWindowShiftAnimation;
            _pendingWindowShiftAnimation = false;
            return value;
        }

        private int ConsumePendingMoveDirection()
        {
            int value = _pendingMoveDirection;
            _pendingMoveDirection = 0;
            return value;
        }

        public event Action<int> SelectionChanged;

        public Launcher(FrameworkElement window, Sound audioManager)
        {
            _window = window;
            _audioManager = audioManager;
        }

        // Формирует текущий набор элементов витрины
        public ShortcutInfo[] GetVisibleShortcuts()
        {
            if (_shortcuts.Length == 0) return Array.Empty<ShortcutInfo>();
            int centerCount = GetCenterVisibleCount();
            if (centerCount <= 0) return Array.Empty<ShortcutInfo>();

            bool hasLeftOverflow = HasLeftOverflow();
            bool hasRightOverflow = HasRightOverflow(centerCount);
            int start = hasLeftOverflow ? _visibleWindowStart - 1 : _visibleWindowStart;
            int totalCount = centerCount + (hasLeftOverflow ? 1 : 0) + (hasRightOverflow ? 1 : 0);

            var slice = new ShortcutInfo[totalCount];
            Array.Copy(_shortcuts, start, slice, 0, totalCount);
            return slice;
        }

        // Возвращает selected индекс внутри текущей витрины
        public int GetSelectedVisibleIndex()
        {
            if (_shortcuts.Length == 0) return -1;
            int leftOffset = HasLeftOverflow() ? 1 : 0;
            return (_selectedIndex - _visibleWindowStart) + leftOffset;
        }

        // Синхронизирует ListBox с текущим логическим окном
        private void ApplyVisibleWindow(bool updateItemsSource = true)
        {
            var itemsListBox = _window.FindName("ItemsListBox") as ListBox;
            if (itemsListBox == null) return;

            int centerCount = GetCenterVisibleCount();
            CarouselLayout.HasLeftOverflow = HasLeftOverflow();
            CarouselLayout.HasRightOverflow = HasRightOverflow(centerCount);

            if (updateItemsSource)
            {
                itemsListBox.ItemsSource = null;
                itemsListBox.ItemsSource = GetVisibleShortcuts();
            }

            int visibleIndex = GetSelectedVisibleIndex();
            if (visibleIndex >= 0 && visibleIndex < (itemsListBox.Items?.Count ?? 0))
                itemsListBox.SelectedIndex = visibleIndex;
        }

        // Загружает ярлыки, сбрасывает выбор на первый элемент и обновляет связанный UI
        public void LoadShortcuts()
        {
            _shortcuts = IconExtractor.LoadShortcuts();
            _selectedIndex = 0;
            ApplyVisibleWindow();
        }

        // Основной шаг навигации по карусели
        // Обновляет абсолютный selected индекс, при необходимости сдвигает окно витрины
        // помечает контекст edge-scroll анимации и применяет изменения в ListBox
        public void MoveSelection(int direction)
        {
            if (_shortcuts.Length == 0) return;

            direction = Math.Sign(direction);
            if (direction == 0) return;
            _pendingMoveDirection = direction;

            int count = _shortcuts.Length;
            int oldStart = _visibleWindowStart;
            int visibleCount = CarouselLayout.VisibleCount;

            _selectedIndex += direction;

            if (_selectedIndex >= count)
            {
                _selectedIndex = 0;
                _visibleWindowStart = 0;
            }
            else if (_selectedIndex < 0)
            {
                _selectedIndex = count - 1;
                _visibleWindowStart = Math.Max(0, count - visibleCount);
            }
            else
            {
                int visibleIndex = _selectedIndex - _visibleWindowStart;
                if (direction > 0 && visibleIndex >= visibleCount)
                    _visibleWindowStart++;
                if (direction < 0 && visibleIndex < 0)
                    _visibleWindowStart--;
            }

            _visibleWindowStart = ClampWindowStart(_visibleWindowStart, count, visibleCount);

            bool windowScrolling = oldStart != _visibleWindowStart;
            _pendingWindowShiftAnimation = windowScrolling;
            ApplyVisibleWindow(updateItemsSource: windowScrolling);
            _audioManager?.PlayMoveSound();
        }

        // Запуск выбранного ярлыка
        public async void LaunchSelected()
        {
            if (_launchCooldown) return;
            _launchCooldown = true;

            try
            {
                _audioManager?.PlayLaunchSound();
                (_window as MainWindow)?.ShowLoading(true);

                var shortcutPath = _shortcuts[_selectedIndex].FullPath;
                Process.Start(new ProcessStartInfo
                {
                    FileName = shortcutPath,
                    UseShellExecute = true
                });
            }
            catch
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    (_window as MainWindow)?.ShowLoading(false);
                });
            }

            // блокируем повторный запуск
            await Task.Delay(2000);
            _launchCooldown = false;
        }
        // Переводит индекс выбранного слота в ListBox в абсолютный индекс в полном списке
        public void HandleSelectionChanged(int visibleIndex)
        {
            if (visibleIndex < 0 || _shortcuts.Length == 0) return;
            int leftOffset = HasLeftOverflow() ? 1 : 0;
            int absoluteIndex = (_visibleWindowStart - leftOffset) + visibleIndex;
            _selectedIndex = Math.Max(0, Math.Min(absoluteIndex, _shortcuts.Length - 1));
            UpdateSelectedName();
            SelectionChanged?.Invoke(_selectedIndex);
        }

        // Единая точка обработки SelectionChanged
        // 1) обновляет selected индекс в полном списке
        // 2) при включенных анимациях запускает соответствующий сценарий в CarouselAnimation
        public void HandleSelectionChangedAndAnimate(ListBox listBox, SelectionChangedEventArgs e)
        {
            if (listBox?.SelectedIndex < 0) return;

            int currentVisibleIndex = listBox.SelectedIndex;
            int previousAbsoluteIndex = _selectedIndex;
            HandleSelectionChanged(currentVisibleIndex);
            int currentAbsoluteIndex = _selectedIndex;

            // if (!EnableSelectionAnimations)
            //     return;

            ApplySelectionAnimations(listBox, e, currentVisibleIndex, previousAbsoluteIndex, currentAbsoluteIndex);
        }

        // Внутренняя маршрутизация scale-анимаций
        // - обычный сценарий (up/down по SelectionChanged)
        // - edge-scroll сценарий (forced down 8/2 + опциональный skip up)
        private void ApplySelectionAnimations(
            ListBox listBox,
            SelectionChangedEventArgs e,
            int currentVisibleIndex,
            int previousAbsoluteIndex,
            int currentAbsoluteIndex)
        {
            bool wasWindowShift = ConsumePendingWindowShiftAnimation();
            int moveDirection = ConsumePendingMoveDirection();
            bool skipScaleUpOnThisChange = wasWindowShift && SkipScaleUpAnimationOnEdgeScroll;
            int fallbackPreviousIndex = GetFallbackPreviousIndex(currentVisibleIndex, previousAbsoluteIndex, currentAbsoluteIndex, listBox.Items.Count);

            CarouselAnimation.AnimateSelectionChanged(
                listBox,
                e,
                fallbackPreviousIndex,
                skipScaleUp: skipScaleUpOnThisChange);
        }

        // Вычисляет fallback-индекс "предыдущего" слота внутри ListBox
        // для анимации уменьшения, если RemovedItems недоступен/некорректен
        private static int GetFallbackPreviousIndex(
            int currentVisibleIndex,
            int previousAbsoluteIndex,
            int currentAbsoluteIndex,
            int itemsCount)
        {
            int delta = currentAbsoluteIndex - previousAbsoluteIndex;
            if (delta > 1 || delta < -1)
                delta = 0;

            int fallbackPreviousIndex = -1;
            if (delta > 0)
                fallbackPreviousIndex = currentVisibleIndex - 1;
            else if (delta < 0)
                fallbackPreviousIndex = currentVisibleIndex + 1;

            return (fallbackPreviousIndex >= 0 && fallbackPreviousIndex < itemsCount)
                ? fallbackPreviousIndex
                : -1;
        }

        private void UpdateSelectedName()
        {
            var selectedGameTitle = _window.FindName("SelectedGameTitle") as TextBlock;
            if (selectedGameTitle == null) return;
            
            selectedGameTitle.Text = _selectedIndex >= 0 && _selectedIndex < _shortcuts.Length 
                ? _shortcuts[_selectedIndex].Name 
                : string.Empty;
        }

        // ---- Geometry helpers ----
        private static int ClampWindowStart(int start, int itemCount, int visibleCount)
        {
            int maxStart = Math.Max(0, itemCount - visibleCount);
            return Math.Max(0, Math.Min(start, maxStart));
        }

        private bool HasLeftOverflow() => _visibleWindowStart > 0;

        private bool HasRightOverflow(int centerCount)
        {
            if (_shortcuts.Length == 0 || centerCount <= 0) return false;
            return _visibleWindowStart + centerCount < _shortcuts.Length;
        }

        private int GetCenterVisibleCount()
        {
            return Math.Min(CarouselLayout.VisibleCount, _shortcuts.Length - _visibleWindowStart);
        }
    }
}