
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace EZ2Play.App
{
    /// <summary>
    /// Coordinates the shared selection indicator between
    /// the main carousel and application overlays.
    /// </summary>
    /// <remarks>
    /// Only one screen owns the selector at a time.
    ///
    /// Main carousel selection updates are deferred until
    /// DispatcherPriority.Loaded so WPF can generate and
    /// arrange the required item containers.
    ///
    /// Ownership generations prevent outdated scheduled
    /// updates from affecting a newly active screen.
    /// </remarks>
    public sealed class SelectorCoordinator : IDisposable
    {
        /// <summary>
        /// Identifies the screen currently using the selector.
        /// </summary>
        public enum Owner
        {
            Main,
            Settings,
            Parser
        }

        // Shared selector and main carousel references.
        private readonly Selector _selector;
        private readonly ListBox _itemsListBox;

        // Main screen opacity source and clipping area.
        private readonly FrameworkElement _mainSurface;
        private readonly FrameworkElement _mainClipScope;

        // Overlay elements used as opacity sources.
        private FrameworkElement _settingsSurface;
        private FrameworkElement _parserSurface;

        // Current selector ownership and lifecycle state.
        private Owner? _activeOwner;
        private int _ownerGeneration;
        private bool _disposed;

        // Prevents duplicate scheduled main selection updates.
        private bool _mainUpdateScheduled;

        public SelectorCoordinator(
            Selector selector,
            ListBox itemsListBox,
            FrameworkElement mainSurface,
            FrameworkElement mainClipScope)
        {
            _selector = selector
                ?? throw new ArgumentNullException(nameof(selector));

            _itemsListBox = itemsListBox
                ?? throw new ArgumentNullException(nameof(itemsListBox));

            _mainSurface = mainSurface
                ?? throw new ArgumentNullException(nameof(mainSurface));

            _mainClipScope = mainClipScope
                ?? throw new ArgumentNullException(nameof(mainClipScope));

            _itemsListBox.SelectionChanged += OnSelectionChanged;
            _itemsListBox.LayoutUpdated += OnItemsListBoxLayoutUpdated;
        }

        /// <summary>
        /// Registers the overlay surfaces whose opacity
        /// should be followed by the shared selector.
        /// </summary>
        public void SetOverlaySurfaces(
            FrameworkElement settings,
            FrameworkElement parser)
        {
            _settingsSurface = settings;
            _parserSurface = parser;
        }

        /// <summary>
        /// Binds selector opacity to the surface
        /// belonging to the current owner.
        /// </summary>
        /// <remarks>
        /// The previous binding is removed before a new
        /// opacity source is assigned.
        ///
        /// When no owner is active, the selector is hidden.
        /// </remarks>
        private void UpdateSelectorOpacity()
        {
            FrameworkElement source = null;

            if (_activeOwner == Owner.Main)
                source = _mainSurface;
            else if (_activeOwner == Owner.Settings)
                source = _settingsSurface;
            else if (_activeOwner == Owner.Parser)
                source = _parserSurface;

            BindingOperations.ClearBinding(
                _selector,
                UIElement.OpacityProperty);

            if (source == null)
            {
                _selector.Opacity = 0;
                return;
            }

            BindingOperations.SetBinding(
                _selector,
                UIElement.OpacityProperty,
                new Binding("Opacity")
                {
                    Source = source,
                    Mode = BindingMode.OneWay
                });
        }

        /// <summary>
        /// Transfers selector ownership to the main carousel
        /// and schedules an update of its selected item.
        /// </summary>
        public void ShowMain()
        {
            if (_disposed) return;

            if (_activeOwner != Owner.Main)
            {
                ++_ownerGeneration;

                _activeOwner = Owner.Main;

                _selector.Detach();
                UpdateSelectorOpacity();
            }

            _selector.SetClipScope(_mainClipScope);

            UpdateMain();
        }

        /// <summary>
        /// Requests an update of the main carousel selector
        /// when the main screen currently owns it.
        /// </summary>
        public void UpdateMain()
        {
            if (_disposed || _activeOwner != Owner.Main)
                return;

            ScheduleMainUpdate();
        }

        /// <summary>
        /// Creates the selector appearance profile
        /// used for game covers and other media items.
        /// </summary>
        public Selector.SelectorProfile CreateMediaProfile()
        {
            return _selector.CreateMediaProfile();
        }

        /// <summary>
        /// Creates the selector appearance profile
        /// used for interactive overlay elements.
        /// </summary>
        public Selector.SelectorProfile CreateOverlayItemProfile()
        {
            return _selector.CreateOverlayItemProfile();
        }

        /// <summary>
        /// Schedules a main carousel selection update
        /// after WPF has processed the required layout.
        /// </summary>
        /// <remarks>
        /// Only one update may be scheduled at a time.
        ///
        /// The ownership generation is captured so the
        /// deferred callback can detect ownership changes.
        /// </remarks>
        private void ScheduleMainUpdate()
        {
            if (_disposed ||
                _activeOwner != Owner.Main ||
                _mainUpdateScheduled)
            {
                return;
            }

            int generation = _ownerGeneration;

            _mainUpdateScheduled = true;

            _itemsListBox.Dispatcher.BeginInvoke(
                new Action(() => PerformMainUpdate(generation)),
                DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Resolves the currently selected carousel item
        /// and attaches the selector to its visual element.
        /// </summary>
        /// <remarks>
        /// If ownership changed while the update was pending,
        /// another update is scheduled for the current generation.
        ///
        /// If the selected item has no generated container,
        /// the selector is detached until a later layout update.
        /// </remarks>
        private void PerformMainUpdate(int generation)
        {
            _mainUpdateScheduled = false;

            if (_disposed || _activeOwner != Owner.Main)
                return;

            if (generation != _ownerGeneration)
            {
                ScheduleMainUpdate();
                return;
            }

            CarouselItem carouselItem = GetSelectedCarouselItem();

            if (carouselItem == null)
            {
                _selector.Detach();
                return;
            }

            _selector.Attach(
                carouselItem,
                _selector.CreateMediaProfile());
        }

        /// <summary>
        /// Retries main selector positioning after layout
        /// when the selected item has not yet been attached.
        /// </summary>
        /// <remarks>
        /// Avoids scheduling unnecessary updates once
        /// the selector already has a target.
        /// </remarks>
        private void OnItemsListBoxLayoutUpdated(
            object sender,
            EventArgs e)
        {
            if (_disposed ||
                _activeOwner != Owner.Main ||
                _itemsListBox.SelectedIndex < 0 ||
                _selector.Target != null)
            {
                return;
            }

            UpdateMain();
        }

        /// <summary>
        /// Transfers selector ownership to an overlay
        /// and attaches it to the specified target.
        /// </summary>
        /// <param name="owner">
        /// Overlay requesting selector ownership.
        /// </param>
        /// <param name="target">
        /// Element to highlight, or null to detach.
        /// </param>
        /// <param name="profile">
        /// Selection appearance profile.
        /// </param>
        /// <param name="clipScope">
        /// Optional visual area restricting selector drawing.
        /// </param>
        public void ShowOverlay(
            Owner owner,
            FrameworkElement target,
            Selector.SelectorProfile profile,
            FrameworkElement clipScope = null)
        {
            if (_disposed) return;

            if (_activeOwner != owner)
            {
                ++_ownerGeneration;

                _activeOwner = owner;

                _selector.Detach();
                UpdateSelectorOpacity();
            }

            _selector.SetClipScope(clipScope);

            if (target == null)
            {
                if (_selector.Target != null)
                    _selector.Detach();

                return;
            }

            _selector.Attach(target, profile);
        }

        /// <summary>
        /// Releases selector ownership when the specified
        /// screen is currently active.
        /// </summary>
        public void Hide(Owner owner)
        {
            if (_disposed || _activeOwner != owner)
                return;

            _ownerGeneration++;

            _selector.Detach();
            _selector.SetClipScope(null);

            _activeOwner = null;

            UpdateSelectorOpacity();
        }

        /// <summary>
        /// Finds the CarouselItem visual element inside
        /// the currently selected ListBoxItem container.
        /// </summary>
        private CarouselItem GetSelectedCarouselItem()
        {
            if (_itemsListBox.SelectedIndex < 0)
                return null;

            var selectedItem =
                _itemsListBox.ItemContainerGenerator.ContainerFromIndex(
                    _itemsListBox.SelectedIndex) as ListBoxItem;

            return FindVisualChild<CarouselItem>(selectedItem);
        }

        /// <summary>
        /// Requests selector repositioning when the
        /// main carousel selection changes.
        /// </summary>
        private void OnSelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_activeOwner == Owner.Main)
                UpdateMain();
        }

        /// <summary>
        /// Recursively searches the WPF visual tree
        /// for the first child of the specified type.
        /// </summary>
        private static T FindVisualChild<T>(
            DependencyObject parent)
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
        /// Releases event subscriptions and detaches
        /// the shared selector from its current target.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _ownerGeneration++;

            _itemsListBox.SelectionChanged -= OnSelectionChanged;
            _itemsListBox.LayoutUpdated -= OnItemsListBoxLayoutUpdated;

            _selector.Detach();
            _selector.SetClipScope(null);

            _activeOwner = null;

            UpdateSelectorOpacity();
        }
    }
}
