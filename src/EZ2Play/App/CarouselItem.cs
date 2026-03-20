using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EZ2Play.App
{
    // --------------- Элемент карусели для рендеринга иконки ярлыка ---------------

    public class CarouselItem : ContentControl
    {
        // --------------- Константы анимации ---------------

        private const double GlowStartOffset = -2;
        private const double GlowEndOffset = 2;
        private const double AnimDuration = 1.5;
        private const double AnimDelay = 4;

        // --------------- Статические поля для глобального управления анимацией ---------------

        private static readonly HashSet<CarouselItem> _activeItems = new HashSet<CarouselItem>();
        private static DateTime _startTime = DateTime.UtcNow;
        private static bool _isRenderingHooked = false;

        // --------------- Dependency Properties ---------------

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

        public static readonly DependencyProperty GlowOffsetProperty =
            DependencyProperty.Register(
                nameof(GlowOffset),
                typeof(double),
                typeof(CarouselItem),
                new FrameworkPropertyMetadata(GlowStartOffset, FrameworkPropertyMetadataOptions.AffectsRender));

        public double GlowOffset
        {
            get => (double)GetValue(GlowOffsetProperty);
            set => SetValue(GlowOffsetProperty, value);
        }

        // --------------- Поля класса ---------------

        private readonly Rectangle _cover;
        private readonly Rectangle _background;

        // --------------- Кэш кистей ---------------

        private static readonly Dictionary<string, ImageBrush> BrushCache = new Dictionary<string, ImageBrush>();
        private static readonly object CacheLock = new object();

        // --------------- Конструктор ---------------

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

            var grid = new Grid();
            grid.Children.Add(_background);
            grid.Children.Add(_cover);
            Content = grid;

            DataContextChanged += OnDataContextChanged;

            if (!_isRenderingHooked)
            {
                CompositionTarget.Rendering += OnRendering;
                _isRenderingHooked = true;
            }
        }

        // --------------- Обработчики событий ---------------

        // Изменение свойства IsSelected
        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var item = (CarouselItem)d;

            item.UpdateContent();

            if ((bool)e.NewValue)
            {
                _activeItems.Add(item);
            }
            else
            {
                _activeItems.Remove(item);
                item.GlowOffset = GlowStartOffset;
            }

            item.InvalidateVisual();
        }

        // Глобальный рендеринг для анимации
        private static void OnRendering(object sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            var totalSeconds = (now - _startTime).TotalSeconds;

            double cycle = AnimDelay + AnimDuration;
            double t = totalSeconds % cycle;

            foreach (var item in _activeItems)
            {
                item.UpdateGlow(t);
            }
        }

        // Обновление эффекта свечения
        private void UpdateGlow(double t)
        {
            if (!IsSelected)
                return;

            if (t < AnimDelay)
            {
                GlowOffset = GlowStartOffset;
            }
            else
            {
                double animT = (t - AnimDelay) / AnimDuration;
                animT = animT * animT * (3 - 2 * animT);
                GlowOffset = GlowStartOffset + (GlowEndOffset - GlowStartOffset) * animT;
            }

            InvalidateVisual();
        }

        // Изменение DataContext
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateContent();
        }

        // --------------- Обновление контента ---------------

        private void UpdateContent()
        {
            var shortcut = DataContext as ShortcutInfo;
            _cover.Fill = GetCachedImageBrush(shortcut?.Icon, shortcut?.FullPath);
            _cover.Stroke = null;
            _cover.StrokeThickness = 0;
        }

        // --------------- Измерение и компоновка ---------------

        protected override Size MeasureOverride(Size availableSize)
        {
            double size = Math.Min(availableSize.Width, availableSize.Height);
            
            if (size <= 0 || double.IsNaN(size) || double.IsInfinity(size))
                size = CarouselLayout.NormalSize;
            
            _cover.Width = size;
            _cover.Height = size;
            _background.Width = size;
            _background.Height = size;
            
            if (TryFindResource(UiScaleKeys.ItemCornerRadius) is double r)
            {
                _cover.RadiusX = r;
                _cover.RadiusY = r;
                _background.RadiusX = r;
                _background.RadiusY = r;
            }
            
            return new Size(size, size);
        }

        // --------------- Отрисовка селектора ---------------

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (!IsSelected)
                return;

            double thickness = (double)TryFindResource(UiScaleKeys.SelectorThickness);
            double spacing = (double)TryFindResource(UiScaleKeys.SelectorSpacing);
            double half = thickness / 2.0;
            double radiusOffset = spacing * 1.25;

            Rect rect = new Rect(
                -spacing - half,
                -spacing - half,
                ActualWidth + (spacing + half) * 2,
                ActualHeight + (spacing + half) * 2
            );

            var baseBrush = (SolidColorBrush)FindResource("FocusStrokeColorOuterBrush");
            Color c = baseBrush.Color;

            double o = GlowOffset;

            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0,0),
                EndPoint = new Point(1,0),
                RelativeTransform = new RotateTransform(45,0.5,0.5),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb((byte)(0.55*255),c.R,c.G,c.B), o - 0.75),
                    new GradientStop(Color.FromArgb((byte)(1.0*255),c.R,c.G,c.B), o),
                    new GradientStop(Color.FromArgb((byte)(0.55*255),c.R,c.G,c.B), o + 0.75)
                }
            };

            Pen pen = new Pen(brush, thickness);
            pen.Freeze();

            double radius = (double)TryFindResource(UiScaleKeys.ItemCornerRadius) + radiusOffset;

            dc.DrawRoundedRectangle(
                null,
                pen,
                rect,
                radius,
                radius);
        }

        // --------------- Кэширование кистей ---------------

        private static ImageBrush GetCachedImageBrush(ImageSource source, string shortcutFullPath)
        {
            string key = !string.IsNullOrEmpty(shortcutFullPath) 
                ? shortcutFullPath 
                : ("img_" + (source?.GetHashCode() ?? 0));
            
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

        // Очистка кэша кистей
        public static void ClearBrushCache()
        {
            lock (CacheLock)
                BrushCache.Clear();
        }
    }
}