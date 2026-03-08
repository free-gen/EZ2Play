using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

// Управляет визуальной анимацией карусели при смене selected-элемента.

namespace EZ2Play.App
{
    public static class CarouselAnimation
    {
        public static double ScaleAnimationDuration { get; set; } = 0.15;

        public static IEasingFunction ScaleEasing { get; set; } =
            new QuadraticEase { EasingMode = EasingMode.EaseOut };

        private static double ScaleFactor => CarouselLayout.SelectedSize / CarouselLayout.NormalSize;

        private static ListBoxItem _lastSelectedCarouselItem;

        public static void AnimateSelectionChanged(ListBox listBox, SelectionChangedEventArgs e, int fallbackPreviousIndex = -1, bool skipScaleUp = false)
        {
            if (listBox == null) return;

            object previousItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null;
            ListBoxItem previousContainer = TryResolvePreviousContainer(listBox, previousItem, fallbackPreviousIndex);
            if (previousContainer == null && IsConnectedToVisualTree(_lastSelectedCarouselItem))
                previousContainer = _lastSelectedCarouselItem;

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

            if (previousContainer != null)
                AnimateSelection(previousContainer, false);
            else if (previousItem != null || fallbackPreviousIndex >= 0)
            {
                ScheduleScaleDownRetry(listBox, previousItem, fallbackPreviousIndex);
            }
        }

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

        public static void AnimateSelection(ListBoxItem item, bool isSelected)
        {
            if (item == null) return;

            double targetScale = isSelected ? ScaleFactor : 1.0;

            var group = EnsureTransforms(item);
            var scale = (ScaleTransform)group.Children[0];
            var translate = (TranslateTransform)group.Children[1];


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

            // Дополнительный сдвиг по Y дает визуальный "рост вниз" при увеличении.
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

        // Установить масштаб без анимации (например при первой загрузке).
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

        // Гарантирует наличие TransformGroup (Scale + Translate) на контейнере.
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

        private static bool IsConnectedToVisualTree(DependencyObject element)
        {
            if (element == null) return false;
            return PresentationSource.FromVisual(element as Visual) != null;
        }

        private static ListBoxItem TryResolvePreviousContainer(ListBox listBox, object previousItem, int fallbackPreviousIndex)
        {
            var byItem = TryGetContainerByItem(listBox, previousItem);
            if (byItem != null) return byItem;
            return TryGetContainerByIndex(listBox, fallbackPreviousIndex);
        }

        private static ListBoxItem TryGetContainerByItem(ListBox listBox, object item)
        {
            if (listBox == null || item == null) return null;
            return listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
        }

        private static ListBoxItem TryGetContainerByIndex(ListBox listBox, int index)
        {
            if (listBox == null || index < 0 || index >= listBox.Items.Count) return null;
            return listBox.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
        }

        private static void ScheduleScaleDownRetry(ListBox listBox, object previousItem, int fallbackPreviousIndex)
        {
            listBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                var deferredPrevious = TryResolvePreviousContainer(listBox, previousItem, fallbackPreviousIndex);
                if (deferredPrevious == null) return;
                if (ReferenceEquals(deferredPrevious, _lastSelectedCarouselItem)) return;
                AnimateSelection(deferredPrevious, false);
            }), DispatcherPriority.Loaded);
        }

    }
}
