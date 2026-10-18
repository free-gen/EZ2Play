
using System;
using System.Windows;
using System.Windows.Controls;

namespace EZ2Play.App
{
    /// <summary>
    /// Arranges carousel items horizontally using the dimensions
    /// calculated by CarouselLayout.
    /// </summary>
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

            // Place overflow items outside the visible carousel range.
            if (CarouselLayout.HasLeftOverflow && InternalChildren.Count > 0)
            {
                ArrangeChildAt(
                    InternalChildren[0],
                    sideMargin - slotStep,
                    startY,
                    slotHeight);

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

            // Keep the right overflow item immediately after the visible range.
            if (CarouselLayout.HasRightOverflow && InternalChildren.Count > 0)
            {
                ArrangeChildAt(
                    InternalChildren[InternalChildren.Count - 1],
                    x,
                    startY,
                    slotHeight);
            }

            return finalSize;
        }

        /// <summary>
        /// Arranges a carousel item at the specified position
        /// using the current normal item width.
        /// </summary>
        private static void ArrangeChildAt(
            UIElement child,
            double x,
            double y,
            double height)
        {
            child?.Arrange(new Rect(x, y, CarouselLayout.NormalSize, height));
        }
    }
}
