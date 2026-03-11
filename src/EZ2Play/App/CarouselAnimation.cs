using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EZ2Play.App
{
    // --------------- Управляет визуальной анимацией карусели ---------------

    public static class CarouselAnimation
    {
        // --------------- Настройки анимации ---------------

        // Длительность анимации масштабирования (сек)
        public static double ScaleAnimationDuration { get; set; } = 0.15;

        // Функция плавности для анимации
        public static IEasingFunction ScaleEasing { get; set; } =
            new QuadraticEase { EasingMode = EasingMode.EaseOut };

        // Коэффициент масштабирования для выбранного элемента
        private static double ScaleFactor => CarouselLayout.SelectedSize / CarouselLayout.NormalSize;

        // --------------- Поля класса ---------------

        // Ссылка на последний выбранный элемент карусели
        private static ListBoxItem _lastSelectedCarouselItem;

        // --------------- Публичные методы - Анимация ---------------

        // Основная анимация при смене выбранного элемента
        public static void AnimateSelectionChanged(
            ListBox listBox,
            SelectionChangedEventArgs e,
            int fallbackPreviousIndex = -1,
            bool skipScaleUp = false)
        {
            if (listBox == null) return;

            // Получаем предыдущий контейнер
            object previousItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null;
            ListBoxItem previousContainer = TryResolvePreviousContainer(listBox, previousItem, fallbackPreviousIndex);

            // Fallback на последний известный выбранный элемент
            if (previousContainer == null && IsConnectedToVisualTree(_lastSelectedCarouselItem))
                previousContainer = _lastSelectedCarouselItem;

            // Анимация нового выбранного элемента
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

            // Анимация предыдущего элемента (уменьшение)
            if (previousContainer != null)
                AnimateSelection(previousContainer, false);
            else if (previousItem != null || fallbackPreviousIndex >= 0)
            {
                ScheduleScaleDownRetry(listBox, previousItem, fallbackPreviousIndex);
            }
        }

        // Принудительное уменьшение элемента по индексу
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

        // Инициализация выбранного элемента при загрузке (без анимации)
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

        // --------------- Публичные методы - Прямая анимация ---------------

        // Анимация масштабирования элемента (увеличение/уменьшение)
        public static void AnimateSelection(ListBoxItem item, bool isSelected)
        {
            if (item == null) return;

            double targetScale = isSelected ? ScaleFactor : 1.0;
            var group = EnsureTransforms(item);
            var scale = (ScaleTransform)group.Children[0];
            var translate = (TranslateTransform)group.Children[1];

            // Пропускаем если масштаб уже установлен
            if (isSelected && Math.Abs(scale.ScaleX - targetScale) < 0.01)
                return;

            // Анимация масштабирования
            var scaleAnimation = new DoubleAnimation
            {
                To = targetScale,
                Duration = TimeSpan.FromSeconds(ScaleAnimationDuration),
                EasingFunction = ScaleEasing,
                FillBehavior = FillBehavior.HoldEnd
            };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);

            // Дополнительный сдвиг по Y для визуального "роста вниз"
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

        // Установка масштаба без анимации (для первой загрузки)
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

        // --------------- Приватные методы - Transform ---------------

        // Гарантирует наличие TransformGroup (Scale + Translate) на контейнере
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

        // --------------- Приватные методы - Поиск контейнеров ---------------

        // Проверка: элемент подключён к визуальному дереву
        private static bool IsConnectedToVisualTree(DependencyObject element)
        {
            if (element == null) return false;
            return PresentationSource.FromVisual(element as Visual) != null;
        }

        // Поиск контейнера по элементу или индексу
        private static ListBoxItem TryResolvePreviousContainer(
            ListBox listBox,
            object previousItem,
            int fallbackPreviousIndex)
        {
            var byItem = TryGetContainerByItem(listBox, previousItem);
            if (byItem != null) return byItem;

            return TryGetContainerByIndex(listBox, fallbackPreviousIndex);
        }

        // Поиск контейнера по элементу данных
        private static ListBoxItem TryGetContainerByItem(ListBox listBox, object item)
        {
            if (listBox == null || item == null) return null;
            return listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
        }

        // Поиск контейнера по индексу
        private static ListBoxItem TryGetContainerByIndex(ListBox listBox, int index)
        {
            if (listBox == null || index < 0 || index >= listBox.Items.Count) return null;
            return listBox.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
        }

        // --------------- Приватные методы - Отложенная анимация ---------------

        // Планирует повторную попытку уменьшения элемента (когда контейнер будет готов)
        private static void ScheduleScaleDownRetry(
            ListBox listBox,
            object previousItem,
            int fallbackPreviousIndex)
        {
            listBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                var deferredPrevious = TryResolvePreviousContainer(listBox, previousItem, fallbackPreviousIndex);
                if (deferredPrevious == null) return;

                // Не уменьшаем если это тот же элемент что и последний выбранный
                if (ReferenceEquals(deferredPrevious, _lastSelectedCarouselItem)) return;

                AnimateSelection(deferredPrevious, false);
            }), DispatcherPriority.Loaded);
        }
    }
}