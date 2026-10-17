using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Threading;

namespace EZ2Play.App
{
    public sealed class SelectorCoordinator : IDisposable
    {
        public enum Owner
        {
            Main,
            Settings,
            Parser
        }

        private readonly Selector _selector;
        private readonly ListBox _itemsListBox;
        private readonly FrameworkElement _mainSurface;
        private readonly FrameworkElement _mainClipScope;
        private FrameworkElement _settingsSurface;
        private FrameworkElement _parserSurface;
        private Owner? _activeOwner;
        private int _ownerGeneration;
        private bool _disposed;
        private bool _mainUpdateScheduled;

        public SelectorCoordinator(Selector selector, ListBox itemsListBox,
            FrameworkElement mainSurface, FrameworkElement mainClipScope)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _itemsListBox = itemsListBox ?? throw new ArgumentNullException(nameof(itemsListBox));
            _mainSurface = mainSurface ?? throw new ArgumentNullException(nameof(mainSurface));
            _mainClipScope = mainClipScope ?? throw new ArgumentNullException(nameof(mainClipScope));

            _itemsListBox.SelectionChanged += OnSelectionChanged;
            _itemsListBox.LayoutUpdated += OnItemsListBoxLayoutUpdated;
        }

        public void SetOverlaySurfaces(FrameworkElement settings, FrameworkElement parser)
        {
            _settingsSurface = settings;
            _parserSurface = parser;
        }

        private void UpdateSelectorOpacity()
        {
            FrameworkElement source = null;

            if (_activeOwner == Owner.Main) source = _mainSurface;
            else if (_activeOwner == Owner.Settings) source = _settingsSurface;
            else if (_activeOwner == Owner.Parser) source = _parserSurface;

            BindingOperations.ClearBinding(_selector, UIElement.OpacityProperty);

            if (source == null)
            {
                _selector.Opacity = 0;
                return;
            }

            BindingOperations.SetBinding(_selector, UIElement.OpacityProperty,
                new Binding("Opacity") { Source = source, Mode = BindingMode.OneWay });
        }

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

        public void UpdateMain()
        {
            if (_disposed || _activeOwner != Owner.Main)
                return;

            ScheduleMainUpdate();
        }

        public Selector.SelectorProfile CreateMediaProfile()
        {
            return _selector.CreateMediaProfile();
        }

        public Selector.SelectorProfile CreateOverlayItemProfile()
        {
            return _selector.CreateOverlayItemProfile();
        }

        private void ScheduleMainUpdate()
        {
            if (_disposed || _activeOwner != Owner.Main || _mainUpdateScheduled)
                return;

            int generation = _ownerGeneration;
            _mainUpdateScheduled = true;
            _itemsListBox.Dispatcher.BeginInvoke(new Action(() => PerformMainUpdate(generation)), DispatcherPriority.Loaded);
        }

        private void PerformMainUpdate(int generation)
        {
            _mainUpdateScheduled = false;

            if (_disposed || _activeOwner != Owner.Main) return;

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

            _selector.Attach(carouselItem, _selector.CreateMediaProfile());
        }

        private void OnItemsListBoxLayoutUpdated(object sender, EventArgs e)
        {
            if (_disposed ||
                _activeOwner != Owner.Main ||
                _itemsListBox.SelectedIndex < 0 ||
                _selector.Target != null)
                return;

            UpdateMain();
        }

        public void ShowOverlay(Owner owner, FrameworkElement target,
            Selector.SelectorProfile profile, FrameworkElement clipScope = null)
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

        private CarouselItem GetSelectedCarouselItem()
        {
            if (_itemsListBox.SelectedIndex < 0)
                return null;

            var selectedItem = _itemsListBox.ItemContainerGenerator.ContainerFromIndex(
                _itemsListBox.SelectedIndex) as ListBoxItem;

            return FindVisualChild<CarouselItem>(selectedItem);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_activeOwner == Owner.Main)
                UpdateMain();
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T result)
                    return result;

                T nestedResult = FindVisualChild<T>(child);

                if (nestedResult != null)
                    return nestedResult;
            }

            return null;
        }

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