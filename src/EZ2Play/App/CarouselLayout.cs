using System;
using System.Windows;
using System.Windows.Controls;

namespace EZ2Play.App
{
    // Геометрия карусели
    public static class CarouselLayout
    {
        // ----------- НАСТРОЙКИ -----------
        public static int VisibleCount { get; set; } = 9;
        public static double SelectedSizeBase { get; set; } = 250;
        public static double SidePaddingBase { get; set; } = 125;

        // ----------- ТЕКУЩИЕ РАЗМЕРЫ -----------
        public static double NormalSize { get; private set; }
        public static double SelectedSize { get; private set; }
        public static double SidePadding { get; private set; }
        public static double GapBetweenItems { get; private set; }

        // Флаги переполнения
        public static bool HasLeftOverflow { get; set; }
        public static bool HasRightOverflow { get; set; }

        // Высота слота и шаг между слотами
        public static double SlotHeight => SelectedSize;
        public static double SlotStep => NormalSize + GapBetweenItems;

        // ----------- ОБНОВЛЕНИЕ ПО ШИРИНЕ VIEWPORT -----------
        public static void UpdateFromViewportWidth(double viewportWidth)
        {
            if (viewportWidth <= 0) return;

            double scale = viewportWidth / LayoutScaler.ReferenceWidth;

            SelectedSize = SelectedSizeBase * scale;
            NormalSize = SelectedSize * 0.92;
            SidePadding = SidePaddingBase * scale;

            double available = viewportWidth - 2 * SidePadding;
            double totalWidth = NormalSize * VisibleCount;

            GapBetweenItems = available > totalWidth
                ? (available - totalWidth) / (VisibleCount - 1)
                : 0;
        }

        // ----------- ОТСТУП ДЛЯ ЦЕНТРИРОВКИ -----------
        public static double GetSideMargin(double viewportWidth)
        {
            double totalWidth = NormalSize * VisibleCount + GapBetweenItems * (VisibleCount - 1);
            return Math.Max(0, (viewportWidth - totalWidth) / 2);
        }
    }

    // Панель карусели
    public class CarouselPanel : Panel
    {
        public CarouselPanel() => ClipToBounds = false;

        protected override Size MeasureOverride(Size availableSize)
        {
            CarouselLayout.UpdateFromViewportWidth(availableSize.Width);
            double slotHeight = CarouselLayout.SlotHeight;

            foreach (UIElement child in InternalChildren)
                child?.Measure(new Size(CarouselLayout.NormalSize, slotHeight));

            double height = double.IsNaN(availableSize.Height) || availableSize.Height <= 0
                ? slotHeight
                : availableSize.Height;

            return new Size(availableSize.Width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int startIndex = 0;
            int endIndex = InternalChildren.Count - 1;

            double slotHeight = CarouselLayout.SlotHeight;
            double slotStep = CarouselLayout.SlotStep;
            double sideMargin = CarouselLayout.GetSideMargin(finalSize.Width);
            double startY = Math.Max(0, (finalSize.Height - slotHeight) * 0.1);

            if (CarouselLayout.HasLeftOverflow && InternalChildren.Count > 0)
            {
                ArrangeChildAt(InternalChildren[0], sideMargin - slotStep, startY, slotHeight);
                startIndex = 1;
            }

            if (CarouselLayout.HasRightOverflow && endIndex >= startIndex)
                endIndex--;

            double x = sideMargin;
            for (int i = startIndex; i <= endIndex; i++)
            {
                ArrangeChildAt(InternalChildren[i], x, startY, slotHeight);
                x += slotStep;
            }

            if (CarouselLayout.HasRightOverflow && InternalChildren.Count > 0)
                ArrangeChildAt(InternalChildren[InternalChildren.Count - 1], x, startY, slotHeight);

            return finalSize;
        }

        private static void ArrangeChildAt(UIElement child, double x, double y, double height)
        {
            child?.Arrange(new Rect(x, y, CarouselLayout.NormalSize, height));
        }
    }
}