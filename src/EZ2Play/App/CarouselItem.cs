using System;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EZ2Play.App
{
    public class CarouselItem : ContentControl
    {
        private readonly Rectangle _cover;
        private readonly Rectangle _background;

        private static readonly Dictionary<string, ImageBrush> BrushCache = new Dictionary<string, ImageBrush>();
        private static readonly object CacheLock = new object();

        public CarouselItem()
        {
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            ClipToBounds = false;

            _background = new Rectangle
            {
                SnapsToDevicePixels = true,
                Style = (Style)FindResource("BaseItemStyle")
            };

            _cover = new Rectangle
            {
                SnapsToDevicePixels = true
            };

            RenderOptions.SetBitmapScalingMode(_cover, BitmapScalingMode.HighQuality);

            var grid = new Grid();
            grid.Children.Add(_background);
            grid.Children.Add(_cover);
            Content = grid;

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateContent();
        }

        private void UpdateContent()
        {
            var shortcut = DataContext as ShortcutInfo;
            _cover.Fill = GetCachedImageBrush(shortcut?.Icon, shortcut?.FullPath);
            _cover.Stroke = null;
            _cover.StrokeThickness = 0;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double size = Math.Min(availableSize.Width, availableSize.Height);

            if (size <= 0 || double.IsNaN(size) || double.IsInfinity(size))
                size = CarouselLayout.NormalSize;

            _cover.Width = size;
            _cover.Height = size;
            _background.Width = size;
            _background.Height = size;

            if (TryFindResource(UiScaleKeys.GameCoverRadius) is double r)
            {
                _cover.RadiusX = r;
                _cover.RadiusY = r;
                _background.RadiusX = r;
                _background.RadiusY = r;
            }

            return new Size(size, size);
        }

        // Reuse frozen brushes for shortcut images across carousel items.
        private static ImageBrush GetCachedImageBrush(ImageSource source, string shortcutFullPath)
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

                brush = new ImageBrush(source) { Stretch = Stretch.UniformToFill };
                brush.Freeze();
                BrushCache[key] = brush;

                return brush;
            }
        }

        public static void ClearBrushCache()
        {
            lock (CacheLock)
                BrushCache.Clear();
        }
    }
}