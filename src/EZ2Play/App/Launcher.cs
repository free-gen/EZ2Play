using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace EZ2Play.App
{
    // --------------- Координатор логики карусели и выбора ярлыков ---------------

    public class Launcher
    {
        // --------------- UI ссылки ---------------

        private readonly ListBox _itemsListBox;
        private readonly TextBlock _selectedGameTitle;
        private readonly MainWindow _mainWindow;
        private readonly Sound _audioManager;

        // --------------- Состояние ---------------

        private ShortcutInfo[] _shortcuts = Array.Empty<ShortcutInfo>();
        private int _selectedIndex = 0;
        private int _visibleWindowStart = 0;
        private bool _pendingWindowShiftAnimation;
        private int _pendingMoveDirection;
        private bool _launchCooldown = false;

        // --------------- Публичные свойства ---------------

        // Текущий набор ярлыков (только для чтения)
        public ShortcutInfo[] Shortcuts => _shortcuts;

        // Индекс выбранного элемента (только для чтения)
        public int SelectedIndex => _selectedIndex;

        // Пропуск анимации увеличения для крайних элементов при скролле
        public bool SkipScaleUpAnimationOnEdgeScroll { get; set; } = false;

        // --------------- События ---------------

        public event Action<int> SelectionChanged;

        // --------------- Конструктор ---------------

        // Инициализирует Launcher с прямыми ссылками на UI-элементы
        public Launcher(ListBox itemsListBox, TextBlock selectedGameTitle, MainWindow mainWindow, Sound audioManager)
        {
            _itemsListBox = itemsListBox;
            _selectedGameTitle = selectedGameTitle;
            _mainWindow = mainWindow;
            _audioManager = audioManager;
        }

        // --------------- Публичные методы ---------------

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

        // Загружает ярлыки, сбрасывает выбор на первый элемент и обновляет UI
        public void LoadShortcuts()
        {
            _shortcuts = IconExtractor.LoadShortcuts();
            _selectedIndex = 0;
            ApplyVisibleWindow();
        }

        // Основной шаг навигации по карусели
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

            // Циклическая навигация
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
                _mainWindow?.ShowLoading(true);

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
                    _mainWindow?.ShowLoading(false);
                });
            }

            // Блокируем повторный запуск
            await Task.Delay(2000);
            _launchCooldown = false;
        }

        // Переводит индекс выбранного слота в ListBox в абсолютный индекс
        public void HandleSelectionChanged(int visibleIndex)
        {
            if (visibleIndex < 0 || _shortcuts.Length == 0) return;

            int leftOffset = HasLeftOverflow() ? 1 : 0;
            int absoluteIndex = (_visibleWindowStart - leftOffset) + visibleIndex;
            _selectedIndex = Math.Max(0, Math.Min(absoluteIndex, _shortcuts.Length - 1));

            UpdateSelectedName();
            SelectionChanged?.Invoke(_selectedIndex);
        }

        // Единая точка обработки SelectionChanged с анимациями
        public void HandleSelectionChangedAndAnimate(ListBox listBox, SelectionChangedEventArgs e)
        {
            if (listBox?.SelectedIndex < 0) return;

            int currentVisibleIndex = listBox.SelectedIndex;
            int previousAbsoluteIndex = _selectedIndex;

            HandleSelectionChanged(currentVisibleIndex);
            int currentAbsoluteIndex = _selectedIndex;

            ApplySelectionAnimations(listBox, e, currentVisibleIndex, previousAbsoluteIndex, currentAbsoluteIndex);
        }

        // --------------- Приватные методы - UI обновления ---------------

        // Синхронизирует ListBox с текущим логическим окном
        private void ApplyVisibleWindow(bool updateItemsSource = true)
        {
            if (_itemsListBox == null) return;

            int centerCount = GetCenterVisibleCount();
            CarouselLayout.HasLeftOverflow = HasLeftOverflow();
            CarouselLayout.HasRightOverflow = HasRightOverflow(centerCount);

            if (updateItemsSource)
            {
                _itemsListBox.ItemsSource = null;
                _itemsListBox.ItemsSource = GetVisibleShortcuts();
            }

            int visibleIndex = GetSelectedVisibleIndex();
            if (visibleIndex >= 0 && visibleIndex < (_itemsListBox.Items?.Count ?? 0))
                _itemsListBox.SelectedIndex = visibleIndex;
        }

        // Обновляет отображение имени выбранной игры
        private void UpdateSelectedName()
        {
            if (_selectedGameTitle == null) return;

            _selectedGameTitle.Text = _selectedIndex >= 0 && _selectedIndex < _shortcuts.Length
                ? _shortcuts[_selectedIndex].Name
                : string.Empty;
        }

        // --------------- Приватные методы - Анимации ---------------

        // Внутренняя маршрутизация scale-анимаций
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

            int fallbackPreviousIndex = GetFallbackPreviousIndex(
                currentVisibleIndex,
                previousAbsoluteIndex,
                currentAbsoluteIndex,
                listBox.Items.Count);

            CarouselAnimation.AnimateSelectionChanged(
                listBox,
                e,
                fallbackPreviousIndex,
                skipScaleUp: skipScaleUpOnThisChange);
        }

        // Вычисляет fallback-индекс "предыдущего" слота внутри ListBox для анимации
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

        // --------------- Приватные методы - Управление состоянием ---------------

        // Сбрасывает флаг pending window shift animation
        private bool ConsumePendingWindowShiftAnimation()
        {
            bool value = _pendingWindowShiftAnimation;
            _pendingWindowShiftAnimation = false;
            return value;
        }

        // Сбрасывает pending move direction
        private int ConsumePendingMoveDirection()
        {
            int value = _pendingMoveDirection;
            _pendingMoveDirection = 0;
            return value;
        }

        // --------------- Приватные методы - Геометрия ---------------

        // Ограничивает start в допустимых пределах
        private static int ClampWindowStart(int start, int itemCount, int visibleCount)
        {
            int maxStart = Math.Max(0, itemCount - visibleCount);
            return Math.Max(0, Math.Min(start, maxStart));
        }

        // Проверяет наличие переполнения слева
        private bool HasLeftOverflow() => _visibleWindowStart > 0;

        // Проверяет наличие переполнения справа
        private bool HasRightOverflow(int centerCount)
        {
            if (_shortcuts.Length == 0 || centerCount <= 0) return false;
            return _visibleWindowStart + centerCount < _shortcuts.Length;
        }

        // Возвращает количество видимых элементов в центре
        private int GetCenterVisibleCount()
        {
            return Math.Min(CarouselLayout.VisibleCount, _shortcuts.Length - _visibleWindowStart);
        }
    }
}