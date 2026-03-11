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
        // --------------- Настройки ---------------

        // Включение XAML контура выделения (для отладки)
        public static bool EnableXamlSelectionOutline { get; set; } = true;

        // --------------- Зависимости ---------------

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

        // --------------- Поля класса ---------------

        private readonly Rectangle _cover;

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

            _cover = new Rectangle
            {
                SnapsToDevicePixels = true
            };

            Content = _cover;
            DataContextChanged += OnDataContextChanged;
        }

        // --------------- Обработчики событий ---------------

        // Изменение свойства IsSelected
        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as CarouselItem)?.UpdateContent();
        }

        // Изменение DataContext
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateContent();
        }

        // --------------- Обновление контента ---------------

        // Обновляет содержимое элемента (иконку ярлыка)
        private void UpdateContent()
        {
            var shortcut = DataContext as ShortcutInfo;
            _cover.Fill = GetCachedImageBrush(shortcut?.Icon, shortcut?.FullPath);
            _cover.Stroke = null;
            _cover.StrokeThickness = 0;
        }

        // --------------- Измерение и компоновка ---------------

        // Переопределение измерения для квадратного элемента
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

        // --------------- Кэширование кистей ---------------

        // Получение кисти из кэша или создание новой
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

        // Очистка кэша кистей (например при смене списка ярлыков)
        public static void ClearBrushCache()
        {
            lock (CacheLock)
                BrushCache.Clear();
        }
    }
}