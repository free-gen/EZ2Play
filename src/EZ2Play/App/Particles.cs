using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace EZ2Play.App
{
    // Canvas для отрисовки фоновых частиц с анимацией
    public class ParticlesCanvas : FrameworkElement
    {
        // --------------- Настройки и константы ---------------

        // Радиус размытия частиц (масштабируется автоматически)
        public double ParticleBlurRadius
        {
            get => _blurRadius;
            set
            {
                _blurRadius = value;
                UpdateBlurEffect();
            }
        }
        private double _blurRadius = 0;
        private BlurEffect _blurEffect;

        // Количество частиц
        public int ParticleCount { get; set; } = 192;

        // Базовые константы для масштабирования под разные разрешения
        private const double BaseMaxSpeed = 8;
        private const double BaseBlurRadius = 4;
        private const double BaseFadeMarginMax = 720;

        // Размеры частиц и веса вероятности для альфа-каналов
        private readonly double[] _sizes = { 3, 4, 6 };
        private readonly int[] _alphaValues = { 16, 64, 128, 224 };
        private readonly double[] _alphaWeights = { 0.60, 0.30, 0.09, 0.01 };

        // --------------- Структура частицы ---------------

        // Данные одной частицы
        private struct Particle
        {
            public double X;
            public double Y;
            public double SpeedX;
            public double SpeedY;
            public double Radius;
            public Brush Brush;
            public double Opacity;
            public bool FadingOut;
            public double FadeMargin;
        }

        // --------------- Поля класса ---------------

        private Particle[] _particles;
        private Brush[] _brushes;
        private readonly Random _random = new Random();
        private readonly DispatcherTimer _timer;
        private bool _needsRedraw = true;
        private bool _isInitialized = false;

        // Для отслеживания изменений размера
        private double _lastWidth;
        private double _lastHeight;

        // --------------- Конструктор и инициализация ---------------

        public ParticlesCanvas()
        {
            // Таймер анимации (~60 FPS)
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += OnTimerTick;

            // События жизненного цикла элемента
            Loaded += (s, e) =>
            {
                InitializeBrushes();
                ApplyScaleToBlur();
                Start();
            };
            Unloaded += (s, e) => Stop();
        }

        // Создание кистей на основе акцентного цвета
        private void InitializeBrushes()
        {
            if (_brushes != null) return;

            var accentBrush = Application.Current?.Resources["AccentFillColorSecondaryBrush"] as SolidColorBrush;
            if (accentBrush == null) return;

            var color = accentBrush.Color;
            var brushesList = new List<Brush>();

            // Создаём кисти с разной прозрачностью
            foreach (int alpha in _alphaValues)
            {
                var c = Color.FromArgb((byte)alpha, color.R, color.G, color.B);
                var brush = new SolidColorBrush(c);
                brush.Freeze(); // Оптимизация: замораживаем неизменяемую кисть
                brushesList.Add(brush);
            }

            _brushes = brushesList.ToArray();
            _isInitialized = true;
        }

        // Вычисление коэффициента масштабирования на основе высоты окна
        private double GetScaleFactor()
        {
            var w = Window.GetWindow(this);
            double h = w?.ActualHeight > 0 ? w.ActualHeight : 1080;
            return h / 1080.0;
        }

        // Применение масштабирования к размытию
        private void ApplyScaleToBlur()
        {
            ParticleBlurRadius = BaseBlurRadius * GetScaleFactor();
        }

        // --------------- Управление анимацией ---------------

        // Запуск анимации частиц
        public void Start()
        {
            if (!_isInitialized || _brushes?.Length == 0) return;
            
            if (_particles == null || _particles.Length == 0)
                InitializeParticles();

            if (_particles?.Length > 0)
                _timer.Start();
        }

        // Остановка анимации
        public void Stop()
        {
            _timer.Stop();
        }

        // --------------- Инициализация частиц ---------------

        // Создание и размещение всех частиц
        private void InitializeParticles()
        {
            if (_brushes?.Length == 0)
            {
                _particles = new Particle[0];
                return;
            }

            double width = ActualWidth > 0 ? ActualWidth : 800;
            double height = ActualHeight > 0 ? ActualHeight : 450;

            _lastWidth = width;
            _lastHeight = height;

            double scale = GetScaleFactor();
            double maxSpeed = BaseMaxSpeed * scale;
            double fadeMarginMax = BaseFadeMarginMax * scale;

            _particles = new Particle[ParticleCount];

            for (int i = 0; i < ParticleCount; i++)
            {
                double radius = _sizes[_random.Next(_sizes.Length)] * scale;
                int brushIndex = SelectWithWeights(_alphaWeights);

                // Распределение частиц 
                Point pos;
                bool isOutside;

                // Внутри в процентах
                if (_random.NextDouble() < 0.3)
                {
                    pos = GetRandomPointInTriangle(width, height);
                    isOutside = false;
                }
                else
                {
                    pos = GetRandomPointOutsideTriangle(width, height, fadeMarginMax);
                    isOutside = true;
                }

                // Настройка начального состояния
                _particles[i] = new Particle
                {
                    X = pos.X,
                    Y = pos.Y,
                    SpeedX = (_random.NextDouble() - 0.5) * 2 * maxSpeed,
                    SpeedY = (_random.NextDouble() - 0.5) * 2 * maxSpeed,
                    Radius = radius,
                    Brush = _brushes[brushIndex],
                    Opacity = isOutside ? 0.3 : 1.0,
                    FadingOut = isOutside,
                    FadeMargin = isOutside && _random.NextDouble() >= 0.7 
                        ? _random.NextDouble() * fadeMarginMax 
                        : 0
                };
            }

            _needsRedraw = true;
        }

        // Выбор индекса с учётом весов вероятности
        private int SelectWithWeights(double[] weights)
        {
            double randomValue = _random.NextDouble();
            double cumulative = 0;

            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (randomValue < cumulative)
                    return i;
            }

            return weights.Length - 1;
        }

        // --------------- Геометрия треугольника ---------------

        // Вершины треугольника для размещения частиц
        private Point GetP1(double width, double height) => new Point(0, height * 0.7);
        private Point GetP2(double width, double height) => new Point(width * 0.8, height);
        private Point GetP3(double width, double height) => new Point(width, height * 0.3);

        // Случайная точка внутри треугольника (метод барицентрических координат)
        private Point GetRandomPointInTriangle(double width, double height)
        {
            var p1 = GetP1(width, height);
            var p2 = GetP2(width, height);
            var p3 = GetP3(width, height);

            double r1 = _random.NextDouble();
            double r2 = _random.NextDouble();

            // Отражение для равномерного распределения
            if (r1 + r2 > 1)
            {
                r1 = 1 - r1;
                r2 = 1 - r2;
            }

            double x = p1.X + r1 * (p2.X - p1.X) + r2 * (p3.X - p1.X);
            double y = p1.Y + r1 * (p2.Y - p1.Y) + r2 * (p3.Y - p1.Y);

            return new Point(x, y);
        }

        // Проверка: точка внутри треугольника
        private bool IsPointInTriangle(Point pt, double width, double height)
        {
            var p1 = GetP1(width, height);
            var p2 = GetP2(width, height);
            var p3 = GetP3(width, height);

            double dX = pt.X - p3.X;
            double dY = pt.Y - p3.Y;
            double dX21 = p3.X - p2.X;
            double dY12 = p2.Y - p3.Y;
            double D = dY12 * (p1.X - p3.X) + dX21 * (p1.Y - p3.Y);
            double s = dY12 * dX + dX21 * dY;
            double t = (p3.Y - p1.Y) * dX + (p1.X - p3.X) * dY;

            if (D < 0) return s <= 0 && t <= 0 && s + t >= D;
            return s >= 0 && t >= 0 && s + t <= D;
        }

        // Случайная точка на границе треугольника
        private Point GetRandomPointOnTriangleBorder(double width, double height)
        {
            var p1 = GetP1(width, height);
            var p2 = GetP2(width, height);
            var p3 = GetP3(width, height);

            // Выбираем случайную сторону
            int side = _random.Next(3);
            double t = _random.NextDouble();

            switch (side)
            {
                case 0: // сторона p1-p2
                    return new Point(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
                case 1: // сторона p2-p3
                    return new Point(p2.X + t * (p3.X - p2.X), p2.Y + t * (p3.Y - p2.Y));
                default: // сторона p3-p1
                    return new Point(p3.X + t * (p1.X - p3.X), p3.Y + t * (p1.Y - p3.Y));
            }
        }

        // Случайная точка снаружи треугольника
        private Point GetRandomPointOutsideTriangle(double width, double height, double maxDistance)
        {
            // Пробуем найти точку снаружи до 10 раз
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var borderPoint = GetRandomPointOnTriangleBorder(width, height);
                
                // Случайное направление наружу
                double angle = _random.NextDouble() * 2 * Math.PI;
                double distance = _random.NextDouble() * maxDistance;
                
                var testPoint = new Point(
                    borderPoint.X + Math.Cos(angle) * distance,
                    borderPoint.Y + Math.Sin(angle) * distance
                );
                
                // Проверяем что точка действительно снаружи
                if (!IsPointInTriangle(testPoint, width, height))
                    return testPoint;
            }
            
            // Фолбек: точка далеко за пределами
            return new Point(
                width * 2 * (_random.NextDouble() - 0.5),
                height * 2 * (_random.NextDouble() - 0.5)
            );
        }

        // Расстояние от точки до ближайшей границы треугольника
        private double GetDistanceOutsideTriangle(Point pt, double width, double height)
        {
            var p1 = GetP1(width, height);
            var p2 = GetP2(width, height);
            var p3 = GetP3(width, height);

            double d1 = DistancePointToLine(pt, p1, p2);
            double d2 = DistancePointToLine(pt, p2, p3);
            double d3 = DistancePointToLine(pt, p3, p1);

            return Math.Min(d1, Math.Min(d2, d3));
        }

        // Расстояние от точки до линии
        private double DistancePointToLine(Point pt, Point a, Point b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            return Math.Abs(dy * pt.X - dx * pt.Y + b.X * a.Y - b.Y * a.X) / Math.Sqrt(dx * dx + dy * dy);
        }

        // --------------- Обновление и отрисовка ---------------

        // Таймер: обновление физики частиц
        private void OnTimerTick(object sender, EventArgs e)
        {
            if (!_isInitialized || _particles == null || _particles.Length == 0) 
                return;
            
            double delta = _timer.Interval.TotalSeconds;
            UpdateParticles(delta);
            
            if (_needsRedraw)
            {
                InvalidateVisual();
                _needsRedraw = false;
            }
        }

        // Обновление позиций и состояния частиц
        private void UpdateParticles(double delta)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            
            if (width <= 0 || height <= 0 || _particles == null || _particles.Length == 0)
                return;
            
            bool anyMoved = false;
            double fadeSpeed = delta / 0.2;
            double scale = GetScaleFactor();
            double maxSpeed = BaseMaxSpeed * scale;
            double fadeMarginMax = BaseFadeMarginMax * scale;

            for (int i = 0; i < _particles.Length; i++)
            {
                var p = _particles[i];

                // Движение
                p.X += p.SpeedX * delta;
                p.Y += p.SpeedY * delta;

                // Если частица вышла из треугольника и не затухает - начинаем затухание
                if (!p.FadingOut && !IsPointInTriangle(new Point(p.X, p.Y), width, height))
                {
                    p.FadingOut = true;
                    p.FadeMargin = _random.NextDouble() < 0.7 
                        ? 0 
                        : _random.NextDouble() * fadeMarginMax;
                }

                // Затухание
                if (p.FadingOut)
                {
                    double overDistance = GetDistanceOutsideTriangle(new Point(p.X, p.Y), width, height);

                    if (overDistance > p.FadeMargin)
                    {
                        p.Opacity -= fadeSpeed;
                        
                        // Полное затухание - респаун
                        if (p.Opacity <= 0)
                        {
                            Point pos;
                            bool isOutside;
                            
                            if (_random.NextDouble() < 0.3)
                            {
                                pos = GetRandomPointInTriangle(width, height);
                                isOutside = false;
                            } 
                            else
                            {
                                pos = GetRandomPointOutsideTriangle(width, height, fadeMarginMax);
                                isOutside = true;
                            }
                            
                            p.X = pos.X;
                            p.Y = pos.Y;
                            p.SpeedX = (_random.NextDouble() - 0.5) * 2 * maxSpeed;
                            p.SpeedY = (_random.NextDouble() - 0.5) * 2 * maxSpeed;
                            p.Radius = _sizes[_random.Next(_sizes.Length)] * scale;
                            p.Brush = _brushes[SelectWithWeights(_alphaWeights)];
                            p.Opacity = isOutside ? 0.3 : 0;
                            p.FadingOut = isOutside;
                            p.FadeMargin = isOutside && _random.NextDouble() >= 0.7 
                                ? _random.NextDouble() * fadeMarginMax 
                                : 0;
                        }
                    }
                }
                // Восстановление прозрачности при возвращении
                else if (p.Opacity < 1.0)
                {
                    p.Opacity += fadeSpeed;
                    if (p.Opacity > 1.0) p.Opacity = 1.0;
                }

                _particles[i] = p;
                if (p.Opacity > 0) anyMoved = true;
            }

            _needsRedraw = anyMoved;
        }

        // Отрисовка всех частиц
        protected override void OnRender(DrawingContext dc)
        {
            if (_particles == null || _brushes == null || _particles.Length == 0) 
                return;

            foreach (var p in _particles)
            {
                if (p.Brush != null && p.Opacity > 0)
                {
                    var brush = p.Brush.Clone();
                    brush.Opacity = p.Opacity;
                    brush.Freeze();
                    dc.DrawEllipse(brush, null, new Point(p.X + p.Radius, p.Y + p.Radius), p.Radius, p.Radius);
                }
            }
        }

        // --------------- Обработка изменений размера ---------------

        // При изменении размера - пересоздаём частицы
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            
            if (sizeInfo.NewSize.Width > 0 && sizeInfo.NewSize.Height > 0)
            {
                ApplyScaleToBlur();
                _lastWidth = 0;
                _lastHeight = 0;
                
                if (_particles != null && _particles.Length > 0 && _isInitialized)
                    InitializeParticles();
            }
        }

        // --------------- Эффекты ---------------

        // Обновление эффекта размытия
        private void UpdateBlurEffect()
        {
            if (_blurEffect == null)
                _blurEffect = new BlurEffect();

            _blurEffect.Radius = _blurRadius;
            this.Effect = _blurEffect;
        }
    }
}