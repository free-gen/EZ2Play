
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EZ2Play.App
{
    /// <summary>
    /// Represents a single game item in the carousel.
    /// Displays the game cover over a styled background
    /// and manages the item's visual dimensions.
    /// </summary>
    /// <remarks>
    /// Image brushes are cached and reused between carousel items
    /// to avoid creating duplicate brushes during navigation.
    /// </remarks>
    public class CarouselItem : ContentControl
    {
        private readonly Rectangle _cover;
        private readonly Rectangle _background;

        // Shared cache of image brushes, indexed by shortcut path.
        private static readonly Dictionary<string, ImageBrush> BrushCache =
            new Dictionary<string, ImageBrush>();

        private static readonly object CacheLock = new object();

        public CarouselItem()
        {
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            ClipToBounds = false;

            // Background appearance is defined by the shared WPF style.
            _background = new Rectangle
            {
                SnapsToDevicePixels = true,
                Style = (Style)FindResource("BaseItemStyle")
            };

            // The cover is rendered above the background rectangle.
            _cover = new Rectangle
            {
                SnapsToDevicePixels = true
            };

            RenderOptions.SetBitmapScalingMode(
                _cover,
                BitmapScalingMode.Linear);

            var grid = new Grid();

            grid.Children.Add(_background);
            grid.Children.Add(_cover);

            Content = grid;

            DataContextChanged += OnDataContextChanged;
        }

        /// <summary>
        /// Updates the displayed cover when the game data changes.
        /// </summary>
        private void OnDataContextChanged(
            object sender,
            DependencyPropertyChangedEventArgs e)
        {
            UpdateContent();
        }

        /// <summary>
        /// Applies the current shortcut's image to the cover
        /// and removes its stroke.
        /// </summary>
        private void UpdateContent()
        {
            var shortcut = DataContext as ShortcutInfo;

            _cover.Fill = GetCachedImageBrush(
                shortcut?.Icon,
                shortcut?.FullPath);

            _cover.Stroke = null;
            _cover.StrokeThickness = 0;
        }

        /// <summary>
        /// Calculates the square dimensions of the game item
        /// and applies the scaled corner radius.
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            double size = Math.Min(
                availableSize.Width,
                availableSize.Height);

            // Use the current carousel size when the available
            // dimensions are invalid or cannot be measured.
            if (size <= 0 || double.IsNaN(size) || double.IsInfinity(size))
                size = CarouselLayout.NormalSize;

            _cover.Width = size;
            _cover.Height = size;

            _background.Width = size;
            _background.Height = size;

            // Both rectangles use the same dynamically scaled radius.
            if (TryFindResource(UiScaleKeys.GameCoverRadius) is double r)
            {
                _cover.RadiusX = r;
                _cover.RadiusY = r;

                _background.RadiusX = r;
                _background.RadiusY = r;
            }

            return new Size(size, size);
        }

        /// <summary>
        /// Returns a cached image brush or creates one for
        /// the specified shortcut image.
        /// </summary>
        /// <remarks>
        /// Brushes are frozen before being cached.
        /// Cache access is synchronized because the dictionary
        /// is shared by all CarouselItem instances.
        /// </remarks>
        private static ImageBrush GetCachedImageBrush(
            ImageSource source,
            string shortcutFullPath)
        {
            string key = !string.IsNullOrEmpty(shortcutFullPath)
                ? shortcutFullPath
                : "img_" + (source?.GetHashCode() ?? 0);

            lock (CacheLock)
            {
                if (BrushCache.TryGetValue(key, out var brush))
                    return brush;

                if (source == null)
                    return null;

                brush = new ImageBrush(source)
                {
                    Stretch = Stretch.UniformToFill
                };

                brush.Freeze();

                BrushCache[key] = brush;

                return brush;
            }
        }

        /// <summary>
        /// Clears all cached image brushes.
        /// Called when carousel artwork needs to be refreshed.
        /// </summary>
        public static void ClearBrushCache()
        {
            lock (CacheLock)
                BrushCache.Clear();
        }
    }
}
