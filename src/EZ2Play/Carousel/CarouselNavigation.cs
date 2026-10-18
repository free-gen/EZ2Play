
using System;

namespace EZ2Play.App
{
    /// <summary>
    /// Manages carousel selection and the visible window
    /// within the complete collection of game shortcuts.
    /// </summary>
    /// <remarks>
    /// Maintains absolute shortcut indices separately from
    /// visible ListBox indices.
    ///
    /// The visible collection may include one additional item
    /// on either side to support carousel transitions.
    /// </remarks>
    public class CarouselNavigation
    {
        private readonly ShortcutInfo[] _shortcuts;

        // Absolute index of the selected shortcut.
        private int _selectedIndex = 0;

        // Absolute index of the first item in the visible window.
        private int _visibleWindowStart = 0;

        // Indicates that navigation has shifted the visible window.
        private bool _pendingWindowShiftAnimation;

        public int SelectedIndex => _selectedIndex;

        public bool HasLeftOverflow => _visibleWindowStart > 0;
        public bool HasRightOverflow => GetHasRightOverflow();

        public ShortcutInfo[] Shortcuts => _shortcuts;
        public bool IsEmpty => _shortcuts.Length == 0;

        public CarouselNavigation(ShortcutInfo[] shortcuts)
        {
            _shortcuts = shortcuts ?? Array.Empty<ShortcutInfo>();
        }

        /// <summary>
        /// Moves the selection by one item in the specified
        /// direction and adjusts the visible window when needed.
        /// </summary>
        /// <remarks>
        /// Selection wraps around at both ends of the collection.
        /// The window shifts only when the new selection moves
        /// outside the currently visible range.
        /// </remarks>
        public void MoveSelection(int direction)
        {
            if (_shortcuts.Length == 0) return;

            direction = Math.Sign(direction);
            if (direction == 0) return;

            int count = _shortcuts.Length;
            int oldStart = _visibleWindowStart;
            int visibleCount = CarouselLayout.VisibleCount;

            _selectedIndex += direction;

            // Wrap selection when reaching either end.
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
                // Convert the absolute selection into a position
                // relative to the current visible window.
                int visibleIndex = _selectedIndex - _visibleWindowStart;

                if (direction > 0 && visibleIndex >= visibleCount)
                    _visibleWindowStart++;

                if (direction < 0 && visibleIndex < 0)
                    _visibleWindowStart--;
            }

            // Prevent the window from extending beyond the collection.
            _visibleWindowStart = ClampWindowStart(
                _visibleWindowStart,
                count,
                visibleCount);

            // Remember whether the visible item range has changed.
            _pendingWindowShiftAnimation = oldStart != _visibleWindowStart;
        }

        /// <summary>
        /// Converts a ListBox selection index into an absolute
        /// shortcut index.
        /// </summary>
        /// <param name="visibleIndex">
        /// Index of the selected item within the visible ListBox.
        /// </param>
        /// <param name="leftOffset">
        /// Number of overflow items preceding the visible window.
        /// </param>
        public void SetSelectedIndex(int visibleIndex, int leftOffset)
        {
            if (visibleIndex < 0 || _shortcuts.Length == 0) return;

            int absoluteIndex = (_visibleWindowStart - leftOffset) + visibleIndex;

            _selectedIndex = Math.Max(
                0,
                Math.Min(absoluteIndex, _shortcuts.Length - 1));
        }

        /// <summary>
        /// Returns the ListBox index corresponding to the
        /// currently selected shortcut.
        /// </summary>
        public int GetSelectedVisibleIndex()
        {
            if (_shortcuts.Length == 0) return -1;

            int leftOffset = HasLeftOverflow ? 1 : 0;

            return (_selectedIndex - _visibleWindowStart) + leftOffset;
        }

        /// <summary>
        /// Returns the shortcut collection currently displayed
        /// by the carousel, including available overflow items.
        /// </summary>
        public ShortcutInfo[] GetVisibleShortcuts()
        {
            if (_shortcuts.Length == 0)
                return Array.Empty<ShortcutInfo>();

            int centerCount = GetCenterVisibleCount();

            if (centerCount <= 0)
                return Array.Empty<ShortcutInfo>();

            // Include the preceding item when left overflow exists.
            int start = HasLeftOverflow
                ? _visibleWindowStart - 1
                : _visibleWindowStart;

            // Include up to one additional item on each side.
            int totalCount =
                centerCount +
                (HasLeftOverflow ? 1 : 0) +
                (HasRightOverflow ? 1 : 0);

            var slice = new ShortcutInfo[totalCount];

            Array.Copy(_shortcuts, start, slice, 0, totalCount);

            return slice;
        }

        /// <summary>
        /// Returns the number of regular visible items,
        /// excluding the additional overflow items.
        /// </summary>
        public int GetCenterVisibleCount()
        {
            return Math.Min(
                CarouselLayout.VisibleCount,
                _shortcuts.Length - _visibleWindowStart);
        }

        /// <summary>
        /// Determines whether additional shortcuts exist
        /// beyond the right edge of the visible window.
        /// </summary>
        private bool GetHasRightOverflow()
        {
            int centerCount = GetCenterVisibleCount();

            if (_shortcuts.Length == 0 || centerCount <= 0)
                return false;

            return _visibleWindowStart + centerCount < _shortcuts.Length;
        }

        /// <summary>
        /// Restricts the visible window's starting position
        /// to the valid range of shortcut indices.
        /// </summary>
        private static int ClampWindowStart(
            int start,
            int itemCount,
            int visibleCount)
        {
            int maxStart = Math.Max(0, itemCount - visibleCount);

            return Math.Max(0, Math.Min(start, maxStart));
        }

        /// <summary>
        /// Resolves the previous visible item index for
        /// carousel selection animation.
        /// </summary>
        /// <remarks>
        /// Only transitions between adjacent absolute indices
        /// produce a fallback index.
        ///
        /// Larger jumps, including wrapping between the first
        /// and last shortcut, return -1.
        /// </remarks>
        public int GetFallbackPreviousIndex(
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

            return fallbackPreviousIndex >= 0 &&
                   fallbackPreviousIndex < itemsCount
                ? fallbackPreviousIndex
                : -1;
        }

        /// <summary>
        /// Returns the pending window-shift flag and resets it.
        /// </summary>
        public bool ConsumePendingWindowShiftAnimation()
        {
            bool value = _pendingWindowShiftAnimation;

            _pendingWindowShiftAnimation = false;

            return value;
        }

        /// <summary>
        /// Indicates whether the most recent navigation operation
        /// changed the visible window and has not yet been consumed.
        /// </summary>
        public bool IsWindowScrolling => _pendingWindowShiftAnimation;
    }
}
