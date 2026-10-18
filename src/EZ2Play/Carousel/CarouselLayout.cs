
using System;

namespace EZ2Play.App
{
    /// <summary>
    /// Calculates and stores the dimensions used to arrange
    /// game items within the carousel.
    /// </summary>
    /// <remarks>
    /// CarouselPanel uses these values when measuring and
    /// arranging items. Dimensions are recalculated whenever
    /// the carousel viewport width changes.
    /// </remarks>
    public static class CarouselLayout
    {
        // Base layout dimensions.
        public static int VisibleCount { get; set; } = 9;
        public static double SelectedSizeBase { get; set; } = 250;
        public static double SidePaddingBase { get; set; } = 125;

        // Dimensions calculated for the current viewport.
        public static double NormalSize { get; private set; }
        public static double SelectedSize { get; private set; }
        public static double SidePadding { get; private set; }
        public static double GapBetweenItems { get; private set; }

        // Track whether hidden items exist beyond either edge.
        public static bool HasLeftOverflow { get; set; }
        public static bool HasRightOverflow { get; set; }

        // Dimensions used by CarouselPanel.
        public static double SlotHeight => SelectedSize;
        public static double SlotStep => NormalSize + GapBetweenItems;

        /// <summary>
        /// Recalculates carousel item dimensions and spacing
        /// using the current viewport width.
        /// </summary>
        public static void UpdateFromViewportWidth(double viewportWidth)
        {
            if (viewportWidth <= 0) return;

            // Scale dimensions relative to the reference resolution.
            double scale = viewportWidth / LayoutScaler.ReferenceWidth;

            SelectedSize = SelectedSizeBase * scale;
            NormalSize = SelectedSize * 0.92;
            SidePadding = SidePaddingBase * scale;

            // Calculate the space available between visible items.
            double available = viewportWidth - 2 * SidePadding;
            double totalWidth = NormalSize * VisibleCount;

            GapBetweenItems = available > totalWidth
                ? (available - totalWidth) / (VisibleCount - 1)
                : 0;
        }

        /// <summary>
        /// Calculates the horizontal margin required to center
        /// the visible carousel items within the viewport.
        /// </summary>
        public static double GetSideMargin(double viewportWidth)
        {
            double totalWidth = NormalSize * VisibleCount + GapBetweenItems * (VisibleCount - 1);

            return Math.Max(0, (viewportWidth - totalWidth) / 2);
        }
    }
}
