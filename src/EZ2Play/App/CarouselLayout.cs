using System;
using System.Windows;
using System.Windows.Controls;

namespace EZ2Play.App
{
    // --------------- Настройки и геометрия витрины карусели ---------------

    public static class CarouselLayout
    {
        // --------------- Константы макета ---------------

        // Количество видимых элементов в карусели
        public static int VisibleCount { get; set; } = 9;

        // Соотношение зазора к размеру элемента
        private const double GapToElementRatio = 30.0 / 220.0;

        // Коэффициент масштабирования для выбранного элемента
        private const double SelectedScaleFactor = 240.0 / 220.0;

        // Минимальный размер элемента (px)
        public static double MinElementSize { get; set; } = 80;

        // --------------- Динамические размеры ---------------

        // Базовый размер элемента карусели
        public static double NormalSize { get; set; } = 220;

        // Размер выбранного элемента (увеличенный)
        public static double SelectedSize { get; set; } = 240;

        // Ширина области просмотра (viewport)
        public static double ViewportWidth { get; set; } = 800;

        // Зазор между элементами
        public static double GapBetweenItems { get; set; } = 40;

        // Отступ по бокам карусели
        public static double SidePadding { get; set; } = 80;

        // --------------- Состояние переполнения ---------------

        // Есть ли элементы слева за пределами видимой области
        public static bool HasLeftOverflow { get; set; }

        // Есть ли элементы справа за пределами видимой области
        public static bool HasRightOverflow { get; set; }

        // --------------- Вычисляемые свойства ---------------

        // Боковой отступ для центрирования контента
        public static double SideMargin
        {
            get
            {
                double totalItemsWidth = NormalSize * VisibleCount;
                double totalGapsWidth = GapBetweenItems * (VisibleCount - 1);
                double freeSpace = ViewportWidth - totalItemsWidth - totalGapsWidth;
                return Math.Max(0, freeSpace / 2);
            }
        }

        // Высота слота элемента
        public static double SlotHeight => SelectedSize;

        // Шаг между слотами (размер + зазор)
        public static double SlotStep => NormalSize + GapBetweenItems;

        // --------------- Обновление размеров ---------------

        // Пересчитывает все размеры на основе ширины viewport
        public static void UpdateFromViewportWidth(double viewportWidth)
        {
            if (viewportWidth <= 0) return;

            ViewportWidth = viewportWidth;

            // Доступное пространство с учётом боковых отступов
            double available = Math.Max(0, viewportWidth - 2 * SidePadding);

            // Вычисляем ширину элемента с учётом зазоров
            double n = VisibleCount;
            double ratio = GapToElementRatio;
            double elementWidth = available / (n + ratio * (n - 1));

            // Ограничиваем минимальным размером
            elementWidth = Math.Max(MinElementSize, elementWidth);

            // Базовый размер иконки (95% от вычисленного)
            NormalSize = elementWidth * 0.95;

            // Размер выбранной иконки (с коэффициентом масштабирования)
            SelectedSize = elementWidth * SelectedScaleFactor * 0.97;

            // Вычисляем зазор между элементами
            GapBetweenItems = elementWidth * ratio;
        }
    }

    // --------------- Панель для размещения элементов карусели ---------------

    public class CarouselPanel : Panel
    {
        // --------------- Конструктор ---------------

        public CarouselPanel()
        {
            ClipToBounds = false;
        }

        // --------------- Измерение элементов ---------------

        // Вычисляет требуемый размер для всех дочерних элементов
        protected override Size MeasureOverride(Size availableSize)
        {
            double viewportWidth = availableSize.Width;

            // Обновляем размеры карусели если ширина валидна
            if (viewportWidth > 0 && !double.IsNaN(viewportWidth) && !double.IsInfinity(viewportWidth))
                CarouselLayout.UpdateFromViewportWidth(viewportWidth);

            double slotHeight = CarouselLayout.SlotHeight;

            // Высота по доступному пространству или по умолчанию
            double desiredHeight = availableSize.Height > 0 
                && !double.IsNaN(availableSize.Height) 
                && !double.IsInfinity(availableSize.Height)
                ? availableSize.Height
                : slotHeight;

            // Измеряем все дочерние элементы
            for (int i = 0; i < InternalChildren.Count; i++)
            {
                var child = InternalChildren[i] as UIElement;
                if (child == null) continue;

                child.Measure(new Size(CarouselLayout.NormalSize, slotHeight));
            }

            return new Size(viewportWidth, desiredHeight);
        }

        // --------------- Размещение элементов ---------------

        // Располагает дочерние элементы согласно геометрии карусели
        protected override Size ArrangeOverride(Size finalSize)
        {
            bool hasLeftOverflow = CarouselLayout.HasLeftOverflow;
            bool hasRightOverflow = CarouselLayout.HasRightOverflow;

            int startIndex = 0;
            int endIndex = InternalChildren.Count - 1;

            double slotHeight = CarouselLayout.SlotHeight;

            // Вертикальное центрирование (10% отступ сверху)
            double startY = (finalSize.Height - slotHeight) * 0.1;
            if (startY < 0) startY = 0;

            // Левый переполняющий элемент (если есть)
            if (hasLeftOverflow && InternalChildren.Count > 0)
            {
                ArrangeChildAt(InternalChildren[0], CarouselLayout.SideMargin - CarouselLayout.SlotStep, startY, slotHeight);
                startIndex = 1;
            }

            // Правый переполняющий элемент (если есть)
            if (hasRightOverflow && endIndex >= startIndex)
                endIndex--;

            // Размещение основных элементов
            double x = CarouselLayout.SideMargin;
            for (int i = startIndex; i <= endIndex; i++)
            {
                ArrangeChildAt(InternalChildren[i], x, startY, slotHeight);
                x += CarouselLayout.SlotStep;
            }

            // Правый крайний элемент (если есть переполнение)
            if (hasRightOverflow && InternalChildren.Count > 0)
                ArrangeChildAt(InternalChildren[InternalChildren.Count - 1], x, startY, slotHeight);

            return new Size(finalSize.Width, finalSize.Height);
        }

        // --------------- Вспомогательные методы ---------------

        // Размещает один дочерний элемент по координатам
        private static void ArrangeChildAt(UIElement child, double x, double y, double height)
        {
            if (child == null) return;

            child.Arrange(new Rect(x, y, CarouselLayout.NormalSize, height));
        }
    }
}