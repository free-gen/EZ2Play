using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EZ2Play.App
{
    public static class CarouselAnimation
    {
        // Scale animation duration in seconds
        public static double ScaleAnimationDuration { get; set; } = 0.15;

        // Easing function used for scale animations
        public static IEasingFunction ScaleEasing { get; set; } =
            new QuadraticEase { EasingMode = EasingMode.EaseOut };

        // Scale factor for the selected carousel item
        private static double ScaleFactor => CarouselLayout.SelectedSize / CarouselLayout.NormalSize;

        // Last known selected carousel item
        private static ListBoxItem _lastSelectedCarouselItem;

        // Animate carousel selection change
        public static void AnimateSelectionChanged(ListBox listBox, SelectionChangedEventArgs e, int fallbackPreviousIndex = -1, bool skipScaleUp = false)
        {
            if (listBox == null) return;

            object previousItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null;
            ListBoxItem previousContainer = TryResolvePreviousContainer(listBox, previousItem, fallbackPreviousIndex);

            // Fall back to the last known selected item
            if (previousContainer == null && IsConnectedToVisualTree(_lastSelectedCarouselItem))
                previousContainer = _lastSelectedCarouselItem;

            // Animate the newly selected item
            if (e.AddedItems.Count > 0)
            {
                var newContainer = listBox.ItemContainerGenerator.ContainerFromItem(e.AddedItems[0]) as ListBoxItem;

                if (newContainer != null)
                {
                    if (skipScaleUp)
                        SetSizeInstant(newContainer, true);
                    else
                        AnimateSelection(newContainer, true);

                    _lastSelectedCarouselItem = newContainer;
                }
            }

            else
            {
                _lastSelectedCarouselItem = null;
            }

            // Scale down the previously selected item
            if (previousContainer != null)
            {
                AnimateSelection(previousContainer, false);
            }

            else if (previousItem != null || fallbackPreviousIndex >= 0)
            {
                ScheduleScaleDownRetry(listBox, previousItem, fallbackPreviousIndex);
            }
        }

        // Force an item to scale down by index
        public static void ForceScaleDownByIndex(ListBox listBox, int index)
        {
            if (listBox == null || index < 0 || index >= listBox.Items.Count) return;

            var container = TryGetContainerByIndex(listBox, index);

            if (container != null)
            {
                AnimateSelection(container, false);
                return;
            }

            ScheduleScaleDownRetry(listBox, previousItem: null, fallbackPreviousIndex: index);
        }

        // Initialize the selected item without animation
        public static void InitializeSelectedItem(ListBox listBox)
        {
            if (listBox?.Items.Count == 0) return;

            int selectedIdx = listBox.SelectedIndex;
            if (selectedIdx < 0) return;

            var selectedContainer = listBox.ItemContainerGenerator.ContainerFromIndex(selectedIdx) as ListBoxItem;

            if (selectedContainer != null)
            {
                SetSizeInstant(selectedContainer, true);
                _lastSelectedCarouselItem = selectedContainer;
            }
        }

        // Animate item scaling for selected and normal states
        public static void AnimateSelection(ListBoxItem item, bool isSelected)
        {
            if (item == null) return;

            double targetScale = isSelected ? ScaleFactor : 1.0;
            var group = EnsureTransforms(item);
            var scale = (ScaleTransform)group.Children[0];
            var translate = (TranslateTransform)group.Children[1];

            // Skip if the selected scale is already applied
            if (isSelected && Math.Abs(scale.ScaleX - targetScale) < 0.01)
                return;

            var scaleAnimation = new DoubleAnimation
                {
                    To = targetScale,
                    Duration = TimeSpan.FromSeconds(ScaleAnimationDuration),
                    EasingFunction = ScaleEasing,
                    FillBehavior = FillBehavior.HoldEnd
                };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);

            // Shift vertically so the selected item grows downward
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

            translate.BeginAnimation(TranslateTransform.YProperty, translateAnimation);
        }

        // Set item scale immediately without animation
        public static void SetSizeInstant(ListBoxItem item, bool isSelected)
        {
            if (item == null) return;

            var group = EnsureTransforms(item);
            var scale = (ScaleTransform)group.Children[0];
            var translate = (TranslateTransform)group.Children[1];

            double scaleValue = isSelected ? ScaleFactor : 1.0;

            scale.ScaleX = scaleValue;
            scale.ScaleY = scaleValue;

            translate.Y = isSelected
                ? (CarouselLayout.SelectedSize - CarouselLayout.NormalSize) / 2
                : 0;
        }

        // Ensure the element has scale and translate transforms
        private static TransformGroup EnsureTransforms(FrameworkElement element)
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

        // Check whether the element is connected to the visual tree
        private static bool IsConnectedToVisualTree(DependencyObject element)
        {
            if (element == null) return false;
            return PresentationSource.FromVisual(element as Visual) != null;
        }

        // Resolve a container by item or fallback index
        private static ListBoxItem TryResolvePreviousContainer(ListBox listBox, object previousItem, int fallbackPreviousIndex)
        {
            var byItem = TryGetContainerByItem(listBox, previousItem);
            if (byItem != null) return byItem;

            return TryGetContainerByIndex(listBox, fallbackPreviousIndex);
        }

        // Find a container by its data item
        private static ListBoxItem TryGetContainerByItem(ListBox listBox, object item)
        {
            if (listBox == null || item == null) return null;
            return listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
        }

        // Find a container by index
        private static ListBoxItem TryGetContainerByIndex(ListBox listBox, int index)
        {
            if (listBox == null || index < 0 || index >= listBox.Items.Count) return null;
            return listBox.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
        }

        // Retry scaling down after the item container is generated
        private static void ScheduleScaleDownRetry(ListBox listBox, object previousItem, int fallbackPreviousIndex)
        {
            listBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var deferredPrevious = TryResolvePreviousContainer(listBox, previousItem, fallbackPreviousIndex);

                    if (deferredPrevious == null) return;

                    // Do not scale down the current selected item
                    if (ReferenceEquals(deferredPrevious, _lastSelectedCarouselItem)) return;

                    AnimateSelection(deferredPrevious, false);
                }), DispatcherPriority.Loaded);
        }
    }
}