
using System;
using System.Windows;
using System.Windows.Controls;

namespace EZ2Play.App
{
    /// <summary>
    /// Arranges artwork items in a grid with a configurable
    /// number of columns and spacing between cells.
    /// </summary>
    /// <remarks>
    /// Used by the parser to display cover and background
    /// thumbnails without changing their measured heights.
    ///
    /// The panel measures its children using the available
    /// column width and uses the tallest measured child
    /// to determine a uniform row height.
    ///
    /// Layout dimensions and positions are rounded to
    /// preserve the original pixel-aligned arrangement.
    /// </remarks>
    public sealed class ParserAssetsPanel : Panel
    {
        // Layout values calculated during the measure pass
        // and reused when arranging child elements.
        private double _layoutCellHeight;
        private double _layoutVerticalGap;
        private double _layoutHorizontalGap;

        /// <summary>
        /// Initializes the panel with pixel snapping
        /// and layout rounding enabled.
        /// </summary>
        public ParserAssetsPanel()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
        }

        /// <summary>
        /// Identifies the Columns dependency property.
        /// Changing this property invalidates measurement.
        /// </summary>
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(
                nameof(Columns),
                typeof(int),
                typeof(ParserAssetsPanel),
                new FrameworkPropertyMetadata(
                    1,
                    FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>
        /// Identifies the Gap dependency property.
        /// Changing this property invalidates measurement.
        /// </summary>
        public static readonly DependencyProperty GapProperty =
            DependencyProperty.Register(
                nameof(Gap),
                typeof(Thickness),
                typeof(ParserAssetsPanel),
                new FrameworkPropertyMetadata(
                    new Thickness(0),
                    FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>
        /// Gets or sets the number of columns in the grid.
        /// </summary>
        public int Columns
        {
            get => (int)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        /// <summary>
        /// Gets or sets the spacing between grid cells.
        /// </summary>
        public Thickness Gap
        {
            get => (Thickness)GetValue(GapProperty);
            set => SetValue(GapProperty, value);
        }

        /// <summary>
        /// Measures the child elements and calculates
        /// the required panel dimensions.
        /// </summary>
        /// <param name="availableSize">
        /// Available size provided by the parent element.
        /// </param>
        /// <returns>
        /// Desired size of the artwork panel.
        /// </returns>
        /// <remarks>
        /// When the available width is infinite, the panel
        /// first measures its children to determine their
        /// natural width.
        ///
        /// Each child is then measured using the calculated
        /// column width. The tallest child determines
        /// the shared cell height.
        ///
        /// The calculated height is based on the number
        /// of rows and the vertical spacing between them.
        /// </remarks>
        protected override Size MeasureOverride(Size availableSize)
        {
            int columns = Math.Max(1, Columns);
            int rows = (InternalChildren.Count + columns - 1) / columns;

            if (InternalChildren.Count == 0)
            {
                return new Size();
            }

            double horizontalGap = Gap.Left + Gap.Right;
            double verticalGap = Gap.Top + Gap.Bottom;

            double maxWidth = 0;
            double maxHeight = 0;

            bool infiniteWidth = double.IsInfinity(availableSize.Width);

            // Natural width is needed only when the parent
            // provides no width limit.
            if (infiniteWidth)
            {
                foreach (UIElement child in InternalChildren)
                {
                    child.Measure(new Size(
                        double.PositiveInfinity,
                        double.PositiveInfinity));

                    maxWidth = Math.Max(
                        maxWidth,
                        child.DesiredSize.Width);
                }
            }

            // Calculate the width available to each column.
            double cellWidth = infiniteWidth
                ? maxWidth
                : Math.Max(
                    0,
                    (availableSize.Width -
                     horizontalGap * (columns - 1)) / columns);

            // Measure each item using its actual column width.
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(
                    cellWidth,
                    double.PositiveInfinity));

                maxHeight = Math.Max(
                    maxHeight,
                    child.DesiredSize.Height);
            }

            // Store rounded layout values for ArrangeOverride.
            _layoutCellHeight = Math.Ceiling(maxHeight);
            _layoutVerticalGap = Math.Ceiling(verticalGap);
            _layoutHorizontalGap = Math.Ceiling(horizontalGap);

            double desiredWidth = double.IsInfinity(availableSize.Width)
                ? cellWidth * columns + horizontalGap * (columns - 1)
                : availableSize.Width;

            double desiredHeight =
                _layoutCellHeight * rows +
                _layoutVerticalGap * Math.Max(0, rows - 1);

            return new Size(desiredWidth, desiredHeight);
        }

        /// <summary>
        /// Arranges child elements in rows and columns
        /// using the dimensions calculated during measurement.
        /// </summary>
        /// <param name="finalSize">
        /// Final size assigned to the panel.
        /// </param>
        /// <returns>
        /// The final panel size.
        /// </returns>
        /// <remarks>
        /// Items are positioned in their original order.
        ///
        /// Horizontal positions and right edges are rounded,
        /// while vertical positions use the measured cell
        /// height and spacing.
        ///
        /// The arrangement algorithm must remain consistent
        /// with MeasureOverride to preserve stable artwork
        /// layout and selector positioning.
        /// </remarks>
        protected override Size ArrangeOverride(Size finalSize)
        {
            int columns = Math.Max(1, Columns);

            double horizontalGap = _layoutHorizontalGap;
            double verticalGap = _layoutVerticalGap;

            double cellWidth = Math.Max(
                0,
                (finalSize.Width -
                 horizontalGap * (columns - 1)) / columns);

            double cellHeight = _layoutCellHeight;

            for (int index = 0; index < InternalChildren.Count; index++)
            {
                int row = index / columns;
                int column = index % columns;

                // Calculate pixel-aligned horizontal coordinates.
                double x = Math.Round(
                    column * (cellWidth + horizontalGap));

                // Keep the original vertical positioning formula.
                double y = row * (cellHeight + verticalGap);

                double right = Math.Round(
                    (column + 1) * (cellWidth + horizontalGap) -
                    horizontalGap);

                // All cells use the same measured row height.
                InternalChildren[index].Arrange(
                    new Rect(
                        x,
                        y,
                        right - x,
                        cellHeight));
            }

            return finalSize;
        }
    }
}
