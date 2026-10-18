
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EZ2Play.App
{
    /// <summary>
    /// Manages selection animations for carousel items.
    /// Handles scaling, vertical translation, and visual
    /// state restoration when item containers are recreated.
    /// </summary>
    /// <remarks>
    /// WPF may generate ListBoxItem containers after a selection
    /// change. Pending selection handling and deferred retries
    /// preserve the original animation behavior in these cases.
    /// </remarks>
    public static class CarouselAnimation
    {
        // Scale animation duration in seconds.
        public static double ScaleAnimationDuration { get; set; } = 0.15;

        // Easing function used for scale animations.
        public static IEasingFunction ScaleEasing { get; set; } =
            new QuadraticEase { EasingMode = EasingMode.EaseOut };

        // Scale factor for the selected carousel item.
        private static double ScaleFactor =>
            CarouselLayout.SelectedSize / CarouselLayout.NormalSize;

        // Last known selected carousel item.
        private static ListBoxItem _lastSelectedCarouselItem;

        // Selection waiting for WPF to generate its item container.
        private static ListBox _pendingSelectionListBox;
        private static object _pendingSelectionItem;

        /// <summary>
        /// Animates the transition between the previously selected
        /// item and the newly selected carousel item.
        /// </summary>
        /// <param name="listBox">
        /// ListBox containing the carousel items.
        /// </param>
        /// <param name="e">
        /// Selection change information provided by WPF.
        /// </param>
        /// <param name="fallbackPreviousIndex">
        /// Optional index used to resolve the previous item
        /// when its data object cannot be resolved directly.
        /// </param>
        public static void AnimateSelectionChanged(
            ListBox listBox,
            SelectionChangedEventArgs e,
            int fallbackPreviousIndex = -1)
        {
            if (listBox == null) return;

            object previousItem = e.RemovedItems.Count > 0
                ? e.RemovedItems[0]
                : null;

            ListBoxItem previousContainer =
                TryResolvePreviousContainer(
                    listBox,
                    previousItem,
                    fallbackPreviousIndex);

            // Reuse the last selected container if it is
            // still connected to the visual tree.
            if (previousContainer == null &&
                IsConnectedToVisualTree(_lastSelectedCarouselItem))
            {
                previousContainer = _lastSelectedCarouselItem;
            }

            if (e.AddedItems.Count > 0)
            {
                object selectedItem = e.AddedItems[0];

                var newContainer =
                    listBox.ItemContainerGenerator.ContainerFromItem(selectedItem)
                    as ListBoxItem;

                if (newContainer != null)
                {
                    ClearPendingSelection(listBox);

                    AnimateSelection(newContainer, true);

                    _lastSelectedCarouselItem = newContainer;
                }
                else
                {
                    // The container has not been generated yet.
                    // Remember the selection for a later retry.
                    SetPendingSelection(listBox, selectedItem);
                }
            }
            else
            {
                ClearPendingSelection(listBox);
                _lastSelectedCarouselItem = null;
            }

            if (previousContainer != null)
            {
                var current = listBox.SelectedIndex >= 0
                    ? listBox.ItemContainerGenerator.ContainerFromIndex(
                        listBox.SelectedIndex) as ListBoxItem
                    : null;

                // Never scale down the currently selected container.
                if (!ReferenceEquals(previousContainer, current))
                    AnimateSelection(previousContainer, false);
            }
            else if (previousItem != null || fallbackPreviousIndex >= 0)
            {
                // Retry after WPF has generated the necessary container.
                ScheduleScaleDownRetry(
                    listBox,
                    previousItem,
                    fallbackPreviousIndex);
            }
        }

        /// <summary>
        /// Initializes the currently selected carousel item
        /// at its target size without playing an animation.
        /// </summary>
        /// <returns>
        /// True if the selected item was initialized;
        /// otherwise false.
        /// </returns>
        public static bool InitializeSelectedItem(ListBox listBox)
        {
            if (listBox?.Items.Count == 0)
                return false;

            if (CarouselLayout.NormalSize <= 0 ||
                CarouselLayout.SelectedSize <= 0)
            {
                return false;
            }

            int selectedIdx = listBox.SelectedIndex;

            if (selectedIdx < 0)
                return false;

            var selectedContainer =
                listBox.ItemContainerGenerator.ContainerFromIndex(selectedIdx)
                as ListBoxItem;

            if (selectedContainer == null)
                return false;

            ClearPendingSelection(listBox);
            SetSizeInstant(selectedContainer, true);

            _lastSelectedCarouselItem = selectedContainer;

            return true;
        }

        /// <summary>
        /// Attempts to animate a selection whose ListBoxItem
        /// container was not available during the original event.
        /// </summary>
        public static bool RetryPendingSelection(ListBox listBox)
        {
            if (listBox == null ||
                !ReferenceEquals(_pendingSelectionListBox, listBox))
            {
                return false;
            }

            var selectedContainer = TryGetContainerByItem(
                listBox,
                _pendingSelectionItem);

            if (selectedContainer == null)
                return false;

            AnimateSelection(selectedContainer, true);

            _lastSelectedCarouselItem = selectedContainer;

            ClearPendingSelection(listBox);

            return true;
        }

        /// <summary>
        /// Animates an item's scale and vertical translation
        /// between selected and normal states.
        /// </summary>
        public static void AnimateSelection(
            ListBoxItem item,
            bool isSelected)
        {
            if (item == null) return;

            double targetScale = isSelected ? ScaleFactor : 1.0;

            var group = EnsureTransforms(item);
            var scale = (ScaleTransform)group.Children[0];
            var translate = (TranslateTransform)group.Children[1];

            var scaleAnimation = new DoubleAnimation
            {
                To = targetScale,
                Duration = TimeSpan.FromSeconds(ScaleAnimationDuration),
                EasingFunction = ScaleEasing,
                FillBehavior = FillBehavior.HoldEnd
            };

            scale.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                scaleAnimation);

            scale.BeginAnimation(
                ScaleTransform.ScaleYProperty,
                scaleAnimation);

            // Shift vertically so the selected item grows downward.
            double targetTranslate = isSelected
                ? (CarouselLayout.SelectedSize - CarouselLayout.NormalSize) / 2
                : 0;

            var translateAnimation = new DoubleAnimation
            {
                To = targetTranslate,
                Duration = TimeSpan.FromSeconds(ScaleAnimationDuration),
                EasingFunction = ScaleEasing,
                FillBehavior = FillBehavior.HoldEnd
            };

            translate.BeginAnimation(
                TranslateTransform.YProperty,
                translateAnimation);
        }

        /// <summary>
        /// Sets an item's selected or normal size immediately,
        /// without playing an animation.
        /// </summary>
        public static void SetSizeInstant(
            ListBoxItem item,
            bool isSelected)
        {
            if (item == null) return;

            var group = EnsureTransforms(item);
            var scale = (ScaleTransform)group.Children[0];
            var translate = (TranslateTransform)group.Children[1];

            // Remove running animations before assigning values.
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            translate.BeginAnimation(TranslateTransform.YProperty, null);

            double scaleValue = isSelected ? ScaleFactor : 1.0;

            scale.ScaleX = scaleValue;
            scale.ScaleY = scaleValue;

            translate.Y = isSelected
                ? (CarouselLayout.SelectedSize - CarouselLayout.NormalSize) / 2
                : 0;
        }

        /// <summary>
        /// Ensures that an element has a TransformGroup
        /// containing scale and translation transforms.
        /// </summary>
        /// <remarks>
        /// Child index 0 is always ScaleTransform.
        /// Child index 1 is always TranslateTransform.
        /// This ordering is required by the animation methods.
        /// </remarks>
        private static TransformGroup EnsureTransforms(
            FrameworkElement element)
        {
            if (element.RenderTransform is TransformGroup group)
                return group;

            var scale = new ScaleTransform(1.0, 1.0);
            var translate = new TranslateTransform();

            group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(translate);

            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = group;

            return group;
        }

        /// <summary>
        /// Checks whether an element is still connected
        /// to a WPF presentation source.
        /// </summary>
        private static bool IsConnectedToVisualTree(
            DependencyObject element)
        {
            if (element == null) return false;

            return PresentationSource.FromVisual(element as Visual) != null;
        }

        /// <summary>
        /// Resolves the previous ListBoxItem container using
        /// its data object or an optional fallback index.
        /// </summary>
        private static ListBoxItem TryResolvePreviousContainer(
            ListBox listBox,
            object previousItem,
            int fallbackPreviousIndex)
        {
            var byItem = TryGetContainerByItem(listBox, previousItem);

            if (byItem != null)
                return byItem;

            return TryGetContainerByIndex(
                listBox,
                fallbackPreviousIndex);
        }

        /// <summary>
        /// Returns the generated ListBoxItem container
        /// associated with a data item.
        /// </summary>
        private static ListBoxItem TryGetContainerByItem(
            ListBox listBox,
            object item)
        {
            if (listBox == null || item == null)
                return null;

            return listBox.ItemContainerGenerator.ContainerFromItem(item)
                as ListBoxItem;
        }

        /// <summary>
        /// Returns the generated ListBoxItem container
        /// at the specified index.
        /// </summary>
        private static ListBoxItem TryGetContainerByIndex(
            ListBox listBox,
            int index)
        {
            if (listBox == null ||
                index < 0 ||
                index >= listBox.Items.Count)
            {
                return null;
            }

            return listBox.ItemContainerGenerator.ContainerFromIndex(index)
                as ListBoxItem;
        }

        /// <summary>
        /// Stores a selection until its visual container
        /// becomes available.
        /// </summary>
        private static void SetPendingSelection(
            ListBox listBox,
            object selectedItem)
        {
            _pendingSelectionListBox = listBox;
            _pendingSelectionItem = selectedItem;
        }

        /// <summary>
        /// Clears the pending selection only if it belongs
        /// to the specified ListBox.
        /// </summary>
        private static void ClearPendingSelection(ListBox listBox)
        {
            if (!ReferenceEquals(_pendingSelectionListBox, listBox))
                return;

            _pendingSelectionListBox = null;
            _pendingSelectionItem = null;
        }

        /// <summary>
        /// Schedules another attempt to scale down the previous
        /// item after WPF has generated its visual container.
        /// </summary>
        /// <remarks>
        /// Uses DispatcherPriority.Loaded to preserve the original
        /// execution timing.
        /// The currently selected item must never be scaled down.
        /// </remarks>
        private static void ScheduleScaleDownRetry(
            ListBox listBox,
            object previousItem,
            int fallbackPreviousIndex)
        {
            listBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                var deferredPrevious = TryResolvePreviousContainer(
                    listBox,
                    previousItem,
                    fallbackPreviousIndex);

                if (deferredPrevious == null)
                    return;

                // Do not scale down the current selected item.
                if (ReferenceEquals(
                    deferredPrevious,
                    _lastSelectedCarouselItem))
                {
                    return;
                }

                if (listBox.SelectedIndex >= 0 &&
                    ReferenceEquals(
                        deferredPrevious,
                        listBox.ItemContainerGenerator.ContainerFromIndex(
                            listBox.SelectedIndex)))
                {
                    return;
                }

                AnimateSelection(deferredPrevious, false);

            }), DispatcherPriority.Loaded);
        }
    }
}
