using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

// Элемент карусели, который рендерит только иконку ярлыка внутри одного слота.

namespace EZ2Play.App
{
    public class CarouselItem : ContentControl
    {
        public static bool EnableXamlSelectionOutline { get; set; } = true;

        private readonly Rectangle _cover;

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(
                nameof(IsSelected),
                typeof(bool),
                typeof(CarouselItem),
                new PropertyMetadata(false, OnIsSelectedChanged));

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public CarouselItem()
        {
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            _cover = new Rectangle
            {
                SnapsToDevicePixels = true
            };

            Content = _cover;
            DataContextChanged += OnDataContextChanged;
        }

        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as CarouselItem)?.UpdateContent();
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
            // Радиус скругления из ресурса (масштабируется через LayoutScaler)
            if (TryFindResource(UiScaleKeys.CarouselItemCornerRadius) is double r)
            {
                _cover.RadiusX = r;
                _cover.RadiusY = r;
            }
            return new Size(size, size);
        }

        // Отрисовка обложки и кэш кистей
        private static readonly Dictionary<string, ImageBrush> BrushCache = new Dictionary<string, ImageBrush>();
        private static readonly object CacheLock = new object();
        private static ImageBrush GetCachedImageBrush(ImageSource source, string shortcutFullPath)
        {
            string key = !string.IsNullOrEmpty(shortcutFullPath) ? shortcutFullPath : ("img_" + (source?.GetHashCode() ?? 0));
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

        // Очистка кэша кистей (например при смене списка ярлыков).
        public static void ClearBrushCache()
        {
            lock (CacheLock)
                BrushCache.Clear();
        }
    }
}
