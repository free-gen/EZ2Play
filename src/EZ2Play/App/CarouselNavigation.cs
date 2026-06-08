using System;

namespace EZ2Play.App
{
    // --------------- Логика навигации карусели ---------------

    public class CarouselNavigation
    {
        // --------------- Поля ---------------

        private readonly ShortcutInfo[] _shortcuts;
        
        private int _selectedIndex = 0;
        private int _visibleWindowStart = 0;
        private bool _pendingWindowShiftAnimation;
        private int _pendingMoveDirection;

        // --------------- Свойства ---------------

        public int SelectedIndex => _selectedIndex;
        public int VisibleWindowStart => _visibleWindowStart;
        public bool HasLeftOverflow => _visibleWindowStart > 0;
        public bool HasRightOverflow => GetHasRightOverflow();

        public ShortcutInfo[] Shortcuts => _shortcuts;
        public bool IsEmpty => _shortcuts.Length == 0;

        // --------------- Конструктор ---------------

        public CarouselNavigation(ShortcutInfo[] shortcuts)
        {
            _shortcuts = shortcuts ?? Array.Empty<ShortcutInfo>();
        }

        // --------------- Навигация ---------------

        public void ResetView()
        {
            _selectedIndex = 0;
            _visibleWindowStart = 0;
        }

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
            _pendingWindowShiftAnimation = oldStart != _visibleWindowStart;
        }

        public void SetSelectedIndex(int visibleIndex, int leftOffset)
        {
            if (visibleIndex < 0 || _shortcuts.Length == 0) return;

            int absoluteIndex = (_visibleWindowStart - leftOffset) + visibleIndex;
            _selectedIndex = Math.Max(0, Math.Min(absoluteIndex, _shortcuts.Length - 1));
        }

        // --------------- Видимое окно ---------------

        public int GetSelectedVisibleIndex()
        {
            if (_shortcuts.Length == 0) return -1;

            int leftOffset = HasLeftOverflow ? 1 : 0;
            return (_selectedIndex - _visibleWindowStart) + leftOffset;
        }

        public ShortcutInfo[] GetVisibleShortcuts()
        {
            if (_shortcuts.Length == 0) return Array.Empty<ShortcutInfo>();

            int centerCount = GetCenterVisibleCount();
            if (centerCount <= 0) return Array.Empty<ShortcutInfo>();

            int start = HasLeftOverflow ? _visibleWindowStart - 1 : _visibleWindowStart;
            int totalCount = centerCount + (HasLeftOverflow ? 1 : 0) + (HasRightOverflow ? 1 : 0);

            var slice = new ShortcutInfo[totalCount];
            Array.Copy(_shortcuts, start, slice, 0, totalCount);
            return slice;
        }

        // --------------- Геометрические расчеты ---------------

        public int GetCenterVisibleCount()
        {
            return Math.Min(CarouselLayout.VisibleCount, _shortcuts.Length - _visibleWindowStart);
        }

        private bool GetHasRightOverflow()
        {
            int centerCount = GetCenterVisibleCount();
            if (_shortcuts.Length == 0 || centerCount <= 0) return false;
            return _visibleWindowStart + centerCount < _shortcuts.Length;
        }

        private static int ClampWindowStart(int start, int itemCount, int visibleCount)
        {
            int maxStart = Math.Max(0, itemCount - visibleCount);
            return Math.Max(0, Math.Min(start, maxStart));
        }

        public int GetFallbackPreviousIndex(int currentVisibleIndex, int previousAbsoluteIndex, 
                                             int currentAbsoluteIndex, int itemsCount)
        {
            int delta = currentAbsoluteIndex - previousAbsoluteIndex;
            if (delta > 1 || delta < -1) delta = 0;

            int fallbackPreviousIndex = -1;
            if (delta > 0) fallbackPreviousIndex = currentVisibleIndex - 1;
            else if (delta < 0) fallbackPreviousIndex = currentVisibleIndex + 1;

            return (fallbackPreviousIndex >= 0 && fallbackPreviousIndex < itemsCount) 
                ? fallbackPreviousIndex : -1;
        }

        // --------------- Управление флагами анимации ---------------

        public bool ConsumePendingWindowShiftAnimation()
        {
            bool value = _pendingWindowShiftAnimation;
            _pendingWindowShiftAnimation = false;
            return value;
        }

        public int ConsumePendingMoveDirection()
        {
            int value = _pendingMoveDirection;
            _pendingMoveDirection = 0;
            return value;
        }

        public bool IsWindowScrolling => _pendingWindowShiftAnimation;
    }
}