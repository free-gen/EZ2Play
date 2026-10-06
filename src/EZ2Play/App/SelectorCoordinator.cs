using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
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
        private Owner? _activeOwner;
        private bool _disposed;
        private bool _mainUpdateScheduled;

        public SelectorCoordinator(Selector selector, ListBox itemsListBox)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _itemsListBox = itemsListBox ?? throw new ArgumentNullException(nameof(itemsListBox));

            _itemsListBox.SelectionChanged += OnSelectionChanged;
            _itemsListBox.LayoutUpdated += OnItemsListBoxLayoutUpdated;
        }

        public void ShowMain()
        {
            if (_disposed)
                return;

            _activeOwner = Owner.Main;
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

            _mainUpdateScheduled = true;
            _itemsListBox.Dispatcher.BeginInvoke(new Action(PerformMainUpdate), DispatcherPriority.Loaded);
        }

        private void PerformMainUpdate()
        {
            _mainUpdateScheduled = false;

            if (_disposed || _activeOwner != Owner.Main)
                return;

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

        public void ShowOverlay(Owner owner, FrameworkElement target, Selector.SelectorProfile profile)
        {
            if (_disposed)
                return;

            if (target == null)
            {
                Hide(owner);
                return;
            }

            _activeOwner = owner;
            _selector.Attach(target, profile);
        }

        public void Hide(Owner owner)
        {
            if (_disposed || _activeOwner != owner)
                return;

            _selector.Detach();
            _activeOwner = null;
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

            _itemsListBox.SelectionChanged -= OnSelectionChanged;
            _itemsListBox.LayoutUpdated -= OnItemsListBoxLayoutUpdated;

            _selector.Detach();
            _activeOwner = null;
        }
    }
}