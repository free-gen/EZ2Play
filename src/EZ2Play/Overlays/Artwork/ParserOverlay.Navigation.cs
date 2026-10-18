
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

using Rectangle = System.Windows.Shapes.Rectangle;

namespace EZ2Play.App
{
    /// <summary>
    /// Handles navigation, artwork tabs, and selection
    /// highlighting within the parser overlay.
    /// </summary>
    /// <remarks>
    /// This partial class shares its state and XAML elements
    /// with ParserOverlay.xaml.cs.
    ///
    /// Game results use list item highlighting, while
    /// artwork previews use media-specific highlighting.
    ///
    /// Selector updates are scheduled through the WPF
    /// dispatcher to preserve the original layout timing.
    /// </remarks>
    public partial class ParserOverlay
    {
        /// <summary>
        /// Handles selection changes in parser result lists.
        /// </summary>
        /// <remarks>
        /// Ignores routed selection events originating
        /// from other elements.
        /// </remarks>
        private void OnParserSelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, sender))
                return;

            ScheduleParserSelectorUpdate();
        }

        /// <summary>
        /// Schedules an update of the parser selector
        /// at DispatcherPriority.Loaded.
        /// </summary>
        /// <remarks>
        /// Prevents multiple pending selector updates.
        ///
        /// The pending flag is cleared when the dispatcher
        /// callback executes. Disposed overlays are ignored.
        /// </remarks>
        private void ScheduleParserSelectorUpdate()
        {
            if (_disposed || _parserSelectorUpdateScheduled)
                return;

            _parserSelectorUpdateScheduled = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _parserSelectorUpdateScheduled = false;

                if (!_disposed)
                    UpdateParserSelector();
            }), DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Refreshes the parser selector when the
        /// overlay is visible and not closing.
        /// </summary>
        public void RefreshSelector()
        {
            if (!_disposed &&
                !_isClosing &&
                Visibility == Visibility.Visible)
            {
                UpdateParserSelector();
            }
        }

        /// <summary>
        /// Updates the selector target according to
        /// the active parser mode and selected item.
        /// </summary>
        /// <remarks>
        /// Game results use the selected ListBoxItem.
        ///
        /// Covers and backgrounds use the Rectangle
        /// contained in the selected item's visual tree.
        ///
        /// The corresponding selector profile and layout
        /// container are passed to SelectorCoordinator.
        /// </remarks>
        private void UpdateParserSelector()
        {
            if (_selectorCoordinator == null)
                return;

            if (Visibility != Visibility.Visible || _isClosing)
            {
                _selectorCoordinator.Hide(
                    SelectorCoordinator.Owner.Parser);

                return;
            }

            ListBox activeListBox = null;

            // Select the list associated with the current mode.
            if (_mode == ParserMode.Games &&
                GamesListBox.Visibility == Visibility.Visible)
            {
                activeListBox = GamesListBox;
            }
            else if (_mode == ParserMode.Covers &&
                     CoversListBox.Visibility == Visibility.Visible)
            {
                activeListBox = CoversListBox;
            }
            else if (_mode == ParserMode.Backgrounds &&
                     BackgroundsListBox.Visibility == Visibility.Visible)
            {
                activeListBox = BackgroundsListBox;
            }

            // No active list or no selected result.
            if (activeListBox == null ||
                activeListBox.SelectedIndex < 0)
            {
                _selectorCoordinator.ShowOverlay(
                    SelectorCoordinator.Owner.Parser,
                    null,
                    null);

                return;
            }

            var selectedItem =
                activeListBox.ItemContainerGenerator.ContainerFromIndex(
                    activeListBox.SelectedIndex) as ListBoxItem;

            // WPF may not have generated the container yet.
            if (selectedItem == null)
            {
                _selectorCoordinator.ShowOverlay(
                    SelectorCoordinator.Owner.Parser,
                    null,
                    null);

                return;
            }

            bool isMedia =
                _mode == ParserMode.Covers ||
                _mode == ParserMode.Backgrounds;

            // Media selection follows the artwork rectangle,
            // rather than the outer ListBoxItem container.
            FrameworkElement target = isMedia
                ? (FrameworkElement)FindVisualChild<Rectangle>(selectedItem)
                : selectedItem;

            Selector.SelectorProfile profile = isMedia
                ? _selectorCoordinator.CreateMediaProfile()
                : _selectorCoordinator.CreateOverlayItemProfile();

            if (target == null)
            {
                _selectorCoordinator.ShowOverlay(
                    SelectorCoordinator.Owner.Parser,
                    null,
                    null);

                return;
            }

            _selectorCoordinator.ShowOverlay(
                SelectorCoordinator.Owner.Parser,
                target,
                profile,
                _mode == ParserMode.Games
                    ? (FrameworkElement)activeListBox
                    : ParserContentGrid);
        }

        /// <summary>
        /// Finds the first descendant of the requested
        /// type in the WPF visual tree.
        /// </summary>
        /// <typeparam name="T">
        /// Type of visual element to locate.
        /// </typeparam>
        /// <param name="parent">
        /// Root element of the search.
        /// </param>
        /// <returns>
        /// The matching descendant, or null if not found.
        /// </returns>
        private static T FindVisualChild<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent == null)
                return null;

            for (int i = 0;
                 i < VisualTreeHelper.GetChildrenCount(parent);
                 i++)
            {
                DependencyObject child =
                    VisualTreeHelper.GetChild(parent, i);

                if (child is T result)
                    return result;

                T nestedResult = FindVisualChild<T>(child);

                if (nestedResult != null)
                    return nestedResult;
            }

            return null;
        }

        /// <summary>
        /// Moves the selection horizontally within
        /// the current artwork grid.
        /// </summary>
        /// <param name="direction">
        /// Direction of movement, normally -1 or 1.
        /// </param>
        /// <remarks>
        /// Horizontal movement is unavailable while
        /// displaying game search results.
        ///
        /// Movement does not wrap across grid row boundaries.
        /// </remarks>
        public void NavigateHorizontal(int direction)
        {
            if (_isBusy || _mode == ParserMode.Games)
                return;

            ListBox listBox = _mode == ParserMode.Covers
                ? CoversListBox
                : BackgroundsListBox;

            int columns = _mode == ParserMode.Covers
                ? GridColumns
                : BackgroundColumns;

            if (listBox.Items.Count == 0)
                return;

            int index = Math.Max(0, listBox.SelectedIndex);
            int column = index % columns;

            // Prevent movement outside the current row.
            if (direction < 0 && column == 0)
                return;

            if (direction > 0 && column == columns - 1)
                return;

            MoveSelection(
                listBox,
                index + Math.Sign(direction));
        }

        /// <summary>
        /// Moves the selection vertically through
        /// game results or artwork grid rows.
        /// </summary>
        /// <param name="direction">
        /// Direction of movement, normally -1 or 1.
        /// </param>
        /// <remarks>
        /// Game results move one item at a time.
        ///
        /// Artwork navigation moves by the number
        /// of columns in the active grid.
        /// </remarks>
        public void NavigateVertical(int direction)
        {
            if (_isBusy)
                return;

            // Game results are arranged as a vertical list.
            if (_mode == ParserMode.Games)
            {
                if (GamesListBox.Items.Count == 0)
                    return;

                int index = Math.Max(
                    0,
                    GamesListBox.SelectedIndex);

                MoveSelection(
                    GamesListBox,
                    index + Math.Sign(direction));

                return;
            }

            // Artwork uses a fixed number of columns.
            ListBox listBox = _mode == ParserMode.Covers
                ? CoversListBox
                : BackgroundsListBox;

            int columns = _mode == ParserMode.Covers
                ? GridColumns
                : BackgroundColumns;

            if (listBox.Items.Count == 0)
                return;

            int indexAsset = Math.Max(
                0,
                listBox.SelectedIndex);

            MoveSelection(
                listBox,
                indexAsset + Math.Sign(direction) * columns);
        }

        /// <summary>
        /// Activates the requested artwork tab.
        /// </summary>
        /// <remarks>
        /// Cover results are displayed immediately.
        ///
        /// Background results are downloaded when
        /// the background tab is opened for the first time.
        /// </remarks>
        private async Task SetActiveParserAssetTab(ParserMode mode)
        {
            _mode = mode;
            UpdateAssetTabs();

            if (mode == ParserMode.Covers)
            {
                ShowCoversTab();
                return;
            }

            if (mode == ParserMode.Backgrounds)
            {
                if (!_backgroundsLoaded)
                {
                    await LoadBackgroundsAsync(
                        _assetGame,
                        _sessionCts.Token);
                }
                else
                {
                    ShowBackgroundsTab();
                }
            }
        }

        /// <summary>
        /// Switches between the cover and background tabs.
        /// </summary>
        /// <param name="direction">
        /// Negative to select covers, positive to select
        /// backgrounds.
        /// </param>
        /// <remarks>
        /// Tab switching is disabled during active operations
        /// and while the parser is showing game results.
        ///
        /// Successful tab changes play the tab navigation sound.
        /// </remarks>
        public async void SwitchAssetTab(int direction)
        {
            if (_isBusy ||
                _mode == ParserMode.Games ||
                _assetGame == null ||
                _sessionCts == null)
            {
                return;
            }

            if (direction < 0)
            {
                if (_mode == ParserMode.Covers)
                    return;

                _mainWindow.GetSound()?.PlayTabSound();

                await SetActiveParserAssetTab(ParserMode.Covers);
                return;
            }

            if (direction > 0)
            {
                if (_mode == ParserMode.Backgrounds)
                    return;

                _mainWindow.GetSound()?.PlayTabSound();

                await SetActiveParserAssetTab(ParserMode.Backgrounds);
            }
        }

        /// <summary>
        /// Updates artwork tab visibility and selection opacity.
        /// </summary>
        private void UpdateAssetTabs()
        {
            AssetTabsPanel.Visibility = _mode == ParserMode.Games
                ? Visibility.Collapsed
                : Visibility.Visible;

            ParserCoversTabText.Opacity =
                _mode == ParserMode.Covers ? 1.0 : 0.5;

            ParserBackgroundsTabText.Opacity =
                _mode == ParserMode.Backgrounds ? 1.0 : 0.5;
        }

        /// <summary>
        /// Displays the current cover results and
        /// restores cover list selection and focus.
        /// </summary>
        private void ShowCoversTab()
        {
            BackgroundsListBox.Visibility = Visibility.Collapsed;

            UpdateAssetViewportHeight(
                CoversListBox,
                CoversVisibleRows,
                UiScaleKeys.ParserCoverSize);

            if (_gridResults.Count == 0)
            {
                CoversListBox.Visibility = Visibility.Collapsed;

                ShowStatus(Locals.GetString("NoResultsFound"));

                ScheduleParserSelectorUpdate();
                return;
            }

            HideStatus();

            CoversListBox.Visibility = Visibility.Visible;

            if (CoversListBox.SelectedIndex < 0)
                CoversListBox.SelectedIndex = 0;

            CoversListBox.ScrollIntoView(CoversListBox.SelectedItem);
            CoversListBox.Focus();

            ScheduleParserSelectorUpdate();
        }

        /// <summary>
        /// Displays the current background results and
        /// restores background list selection and focus.
        /// </summary>
        private void ShowBackgroundsTab()
        {
            CoversListBox.Visibility = Visibility.Collapsed;

            UpdateAssetViewportHeight(
                BackgroundsListBox,
                BackgroundsVisibleRows,
                UiScaleKeys.ParserBackgroundHeight);

            if (_heroResults.Count == 0)
            {
                BackgroundsListBox.Visibility = Visibility.Collapsed;

                ShowStatus(Locals.GetString("NoResultsFound"));

                ScheduleParserSelectorUpdate();
                return;
            }

            HideStatus();

            BackgroundsListBox.Visibility = Visibility.Visible;

            if (BackgroundsListBox.SelectedIndex < 0)
                BackgroundsListBox.SelectedIndex = 0;

            BackgroundsListBox.ScrollIntoView(
                BackgroundsListBox.SelectedItem);

            BackgroundsListBox.Focus();

            ScheduleParserSelectorUpdate();
        }

        /// <summary>
        /// Updates the selected item, scroll position,
        /// selector highlight, and navigation sound.
        /// </summary>
        /// <param name="listBox">
        /// List containing the item to select.
        /// </param>
        /// <param name="targetIndex">
        /// Requested selection index.
        /// </param>
        private void MoveSelection(
            ListBox listBox,
            int targetIndex)
        {
            if (listBox.Items.Count == 0)
                return;

            targetIndex = Math.Max(
                0,
                Math.Min(targetIndex, listBox.Items.Count - 1));

            if (targetIndex == listBox.SelectedIndex)
                return;

            listBox.SelectedIndex = targetIndex;

            // Preserve the original update order.
            UpdateParserSelector();
            listBox.ScrollIntoView(listBox.SelectedItem);

            _mainWindow.GetSound()?.PlayMoveSound();
        }
    }
}
