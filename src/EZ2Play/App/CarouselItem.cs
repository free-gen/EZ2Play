using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EZ2Play.App
{
    // --------------- Элемент карусели для рендеринга иконки ярлыка ---------------

    public class CarouselItem : ContentControl
    {
        // --------------- Константы анимации ---------------

        private const double GlowStartOffset = -0.5;
        private const double GlowEndOffset = 1.5;
        private const double GlowAnimationDurationSeconds = 0.8;
        private const double GlowDelaySeconds = 3.5;

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

        private DispatcherTimer _glowTimer;
        private bool _glowAnimationActive = false;

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

            InitializeGlowTimer();

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
        }

        // --------------- Инициализация ---------------

        private void InitializeGlowTimer()
        {
            _glowTimer = new DispatcherTimer();
            _glowTimer.Interval = TimeSpan.FromSeconds(GlowDelaySeconds);
            
            _glowTimer.Tick += (s, e) =>
            {
                _glowTimer.Stop();
                if (_glowAnimationActive)
                {
                    PlayOneShotGlowAnimation();
                }
            };
        }

        // --------------- Обработчики событий ---------------

        // Изменение свойства IsSelected
        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var item = d as CarouselItem;
            if (item == null) return;

            item.UpdateContent();

            if ((bool)e.NewValue)
            {
                item.StartGlowWithDelay();
            }
            else
            {
                item.StopGlow();
            }

            item.InvalidateVisual();
        }

        // Изменение DataContext
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateContent();
        }

        // --------------- Управление анимацией ---------------

        private void StartGlowWithDelay()
        {
            StopGlow();
            _glowAnimationActive = true;
            _glowTimer.Start();
        }

        private void StopGlow()
        {
            _glowAnimationActive = false;
            _glowTimer?.Stop();
            this.BeginAnimation(GlowOffsetProperty, null);
            GlowOffset = GlowStartOffset;
        }

        private void PlayOneShotGlowAnimation()
        {
            if (!IsConnectedToVisualTree())
                return;

            var forwardAnimation = new DoubleAnimation
            {
                From = GlowStartOffset,
                To = GlowEndOffset,
                Duration = TimeSpan.FromSeconds(GlowAnimationDurationSeconds),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            forwardAnimation.Completed += (s, e) =>
            {
                if (_glowAnimationActive)
                {
                    GlowOffset = GlowStartOffset;
                    _glowTimer.Start();
                }
            };
            
            this.BeginAnimation(GlowOffsetProperty, forwardAnimation);
        }

        private bool IsConnectedToVisualTree()
        {
            return PresentationSource.FromVisual(this) != null;
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

        public static void ClearBrushCache()
        {
            lock (CacheLock)
                BrushCache.Clear();
        }
    }
}