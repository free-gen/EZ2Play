using System;
using System.Windows;
using System.Windows.Controls;

// Разметка витрины

namespace EZ2Play.App
{
    public static class CarouselLayout
    {
        public static int VisibleCount { get; set; } = 9;
        private const double GapToElementRatio = 30.0 / 220.0;
        public static double GapBetweenItems { get; set; } = 40;
        public static double SidePadding { get; set; } = 80;
        private const double SelectedScaleFactor = 240.0 / 220.0;
        public static double MinElementSize { get; set; } = 80;

        public static double NormalSize { get; set; } = 220;
        public static double SelectedSize { get; set; } = 240;
        public static double ViewportWidth { get; set; } = 800;

        public static void UpdateFromViewportWidth(double viewportWidth)
        {
            if (viewportWidth <= 0) return;
            ViewportWidth = viewportWidth;
            double available = Math.Max(0, viewportWidth - 2 * SidePadding);
            double n = VisibleCount;
            double ratio = GapToElementRatio;
            double elementWidth = available / (n + ratio * (n - 1));
            elementWidth = Math.Max(MinElementSize, elementWidth);
            NormalSize = elementWidth * 0.95; // размер базовой иконки
            SelectedSize = elementWidth * SelectedScaleFactor * 0.97; // размер выбранной иконки
            GapBetweenItems = elementWidth * ratio;
        }

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

        public static double SlotHeight => SelectedSize;
        public static double SlotStep => NormalSize + GapBetweenItems;
        public static bool HasLeftOverflow { get; set; }
        public static bool HasRightOverflow { get; set; }
    }

    public class CarouselPanel : Panel
    {
        public CarouselPanel()
        {
            ClipToBounds = false;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double viewportWidth = availableSize.Width;
            if (viewportWidth > 0 && !double.IsNaN(viewportWidth) && !double.IsInfinity(viewportWidth))
                CarouselLayout.UpdateFromViewportWidth(viewportWidth);
            double slotHeight = CarouselLayout.SlotHeight;
            double desiredHeight = availableSize.Height > 0 && !double.IsNaN(availableSize.Height) && !double.IsInfinity(availableSize.Height)
                ? availableSize.Height
                : slotHeight;
            for (int i = 0; i < InternalChildren.Count; i++)
            {
                var child = InternalChildren[i] as UIElement;
                if (child == null) continue;
                child.Measure(new Size(CarouselLayout.NormalSize, slotHeight));
            }
            return new Size(viewportWidth, desiredHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            bool hasLeftOverflow = CarouselLayout.HasLeftOverflow;
            bool hasRightOverflow = CarouselLayout.HasRightOverflow;
            int startIndex = 0;
            int endIndex = InternalChildren.Count - 1;
            double slotHeight = CarouselLayout.SlotHeight;
            double startY = (finalSize.Height - slotHeight) * 0.1; // вертикальное центрирование
            if (startY < 0) startY = 0;

            if (hasLeftOverflow && InternalChildren.Count > 0)
            {
                ArrangeChildAt(InternalChildren[0], CarouselLayout.SideMargin - CarouselLayout.SlotStep, startY, slotHeight);
                startIndex = 1;
            }
            if (hasRightOverflow && endIndex >= startIndex)
                endIndex--;
            double x = CarouselLayout.SideMargin;
            for (int i = startIndex; i <= endIndex; i++)
            {
                ArrangeChildAt(InternalChildren[i], x, startY, slotHeight);
                x += CarouselLayout.SlotStep;
            }
            if (hasRightOverflow && InternalChildren.Count > 0)
                ArrangeChildAt(InternalChildren[InternalChildren.Count - 1], x, startY, slotHeight);
            return new Size(finalSize.Width, finalSize.Height);
        }

        private static void ArrangeChildAt(UIElement child, double x, double y, double height)
        {
            if (child == null) return;
            child.Arrange(new Rect(x, y, CarouselLayout.NormalSize, height));
        }
    }
}
