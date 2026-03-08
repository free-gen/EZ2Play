using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace EZ2Play.App
{
    public class ParticlesCanvas : FrameworkElement
    {
        // НАСТРОЙКИ
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

        private System.Windows.Media.Effects.BlurEffect _blurEffect;

        public int ParticleCount { get; set; } = 128;
        
        private Brush[] _brushes;
        private readonly double[] _sizes = { 4, 6, 8 }; // базовые размеры частиц (масштабируются от разрешения)
        private readonly int[] _alphaValues = { 16, 64, 128, 224 };
        private readonly double[] _alphaWeights = { 0.60, 0.30, 0.09, 0.01 };
        private const double BaseMaxSpeed = 8;
        private const double BaseBlurRadius = 4;
        private const double BaseFadeMarginMax = 200;
        
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

        // ВНУТРЕННИЕ ДАННЫЕ
        private Particle[] _particles;
        private readonly Random _random = new Random();
        private readonly DispatcherTimer _timer;
        private bool _needsRedraw = true;
        
        private double _lastWidth;
        private double _lastHeight;
        private bool _isInitialized = false;

        // КОНСТРУКТОР
        public ParticlesCanvas()
        {
            _brushes = null;

            Loaded += (s, e) => 
            {
                InitializeBrushes();
                ApplyScaleToBlur();
                Start();
            };
            
            Unloaded += (s, e) => Stop();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += OnTimerTick;
        }

        // ИНИЦИАЛИЗАЦИЯ КИСТЕЙ
        private void InitializeBrushes()
        {
            if (_brushes != null) return;

            var accentBrush = Application.Current?.Resources["AccentFillColorSecondaryBrush"] as SolidColorBrush;
                if (accentBrush == null)
                    return;

            var color = accentBrush.Color;

            var brushesList = new System.Collections.Generic.List<Brush>();

            foreach (int alpha in _alphaValues)
            {
                var c = Color.FromArgb((byte)alpha, color.R, color.G, color.B);
                var brush = new SolidColorBrush(c);
                brush.Freeze();
                brushesList.Add(brush);
            }

            _brushes = brushesList.ToArray();
            _isInitialized = true;
        }

        //  Коэффициент масштаба от высоты окна (размеры частиц, скорость, блюр). 
        private double GetScaleFactor()
        {
            var w = Window.GetWindow(this);
            double h = w != null ? w.ActualHeight : 0;
            if (h <= 0) h = LayoutScaler.ReferenceHeight;
            return LayoutScaler.GetScaleFactor(h);
        }

        private void ApplyScaleToBlur()
        {
            ParticleBlurRadius = BaseBlurRadius * GetScaleFactor();
        }

        // ВСПОМОГАТЕЛЬНЫЙ МЕТОД ДЛЯ ВЫБОРА С ВЕСАМИ
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
            
            return weights.Length - 1; // на всякий случай
        }

        // START / STOP
        public void Start()
        {
            if (!_isInitialized || _brushes == null || _brushes.Length == 0) 
                return;
            
            if (_particles == null || _particles.Length == 0)
                InitializeParticles();

            if (_particles != null && _particles.Length > 0)
                _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        // ИНИЦИАЛИЗАЦИЯ ЧАСТИЦ
        private void InitializeParticles()
        {
            if (_brushes == null || _brushes.Length == 0) 
            {
                _particles = new Particle[0];
                return;
            }
            
            double width = ActualWidth;
            double height = ActualHeight;

            if (width <= 0 || height <= 0)
            {
                width = 640;
                height = 360;
            }

            _lastWidth = width;
            _lastHeight = height;

            double scale = GetScaleFactor();
            double maxSpeed = BaseMaxSpeed * scale;

            _particles = new Particle[ParticleCount];

            for (int i = 0; i < ParticleCount; i++)
            {
                double radius = _sizes[_random.Next(_sizes.Length)] * scale;
                int alphaIndex = SelectWithWeights(_alphaWeights);
                int brushIndex = alphaIndex;
                var pos = GetRandomPointInTriangle(width, height);

                _particles[i] = new Particle
                {
                    X = pos.X,
                    Y = pos.Y,
                    SpeedX = (_random.NextDouble() - 0.5) * 2 * maxSpeed,
                    SpeedY = (_random.NextDouble() - 0.5) * 2 * maxSpeed,
                    Radius = radius,
                    Brush = _brushes[brushIndex],
                    Opacity = 1.0,
                    FadingOut = false
                };
            }
            
            _needsRedraw = true;
        }

        // ТАЙМЕР
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

        // ОБНОВЛЕНИЕ ПОЗИЦИЙ
        private void UpdateParticles(double delta)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            
            if (width <= 0 || height <= 0 || _particles == null || _particles.Length == 0)
                return;
            
            bool anyMoved = false;
            double fadeSpeed = delta / 0.2; // за 0.2 сек Opacity изменится полностью

            for (int i = 0; i < _particles.Length; i++)
            {
                var p = _particles[i];

                // движение
                p.X += p.SpeedX * delta;
                p.Y += p.SpeedY * delta;

                // проверяем выход за треугольник
                if (!p.FadingOut && !IsPointInTriangle(new Point(p.X, p.Y), width, height))
                {
                    p.FadingOut = true;

                    double fadeMarginMax = BaseFadeMarginMax * GetScaleFactor();
                    if (_random.NextDouble() < 0.7)
                        p.FadeMargin = 0;
                    else
                        p.FadeMargin = _random.NextDouble() * fadeMarginMax;
                }

                if (p.FadingOut)
                {
                    double overDistance = GetDistanceOutsideTriangle(new Point(p.X, p.Y), width, height);

                    if (overDistance > p.FadeMargin)
                    {
                        p.Opacity -= fadeSpeed;
                        if (p.Opacity <= 0)
                        {
                            double scale = GetScaleFactor();
                            double maxSpeed = BaseMaxSpeed * scale;
                            var pos = GetRandomPointInTriangle(width, height);
                            int radiusIndex = _random.Next(_sizes.Length);
                            int alphaIndex = SelectWithWeights(_alphaWeights);
                            int brushIndex = alphaIndex;

                            p.X = pos.X;
                            p.Y = pos.Y;
                            p.SpeedX = (_random.NextDouble() - 0.5) * 2 * maxSpeed;
                            p.SpeedY = (_random.NextDouble() - 0.5) * 2 * maxSpeed;
                            p.Radius = _sizes[radiusIndex] * scale;
                            p.Brush = _brushes[brushIndex];
                            p.Opacity = 0;
                            p.FadingOut = false;
                            p.FadeMargin = 0;
                        }
                    }
                }
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

        // ОТРИСОВКА
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

        // ОБРАБОТКА ИЗМЕНЕНИЯ РАЗМЕРА — пересчёт блюра и переинициализация частиц под новый масштаб
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

        // Область концентрации
        private Point GetRandomPointInTriangle(double width, double height)
        {
            // Вершины треугольника
            Point p1 = new Point(0, height * 0.7);          // левый край 70% от верха
            Point p2 = new Point(width * 0.8, height);      // низ 80% от левого
            Point p3 = new Point(width, height * 0.3);      // правый край 30%

            // Барицентрическая генерация (правильный способ)
            double r1 = _random.NextDouble();
            double r2 = _random.NextDouble();

            if (r1 + r2 > 1)
            {
                r1 = 1 - r1;
                r2 = 1 - r2;
            }

            double x = p1.X + r1 * (p2.X - p1.X) + r2 * (p3.X - p1.X);
            double y = p1.Y + r1 * (p2.Y - p1.Y) + r2 * (p3.Y - p1.Y);

            return new Point(x, y);
        }

        private bool IsPointInTriangle(Point pt, double width, double height)
        {
            Point p1 = new Point(0, height * 0.7);
            Point p2 = new Point(width * 0.8, height);
            Point p3 = new Point(width, height * 0.3);

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

        private double GetDistanceOutsideTriangle(Point pt, double width, double height)
        {
            Point p1 = new Point(0, height * 0.7);
            Point p2 = new Point(width * 0.8, height);
            Point p3 = new Point(width, height * 0.3);

            // приближенно: считаем min расстояние до линий треугольника
            double d1 = DistancePointToLine(pt, p1, p2);
            double d2 = DistancePointToLine(pt, p2, p3);
            double d3 = DistancePointToLine(pt, p3, p1);

            return Math.Min(d1, Math.Min(d2, d3));
        }

        private double DistancePointToLine(Point pt, Point a, Point b)
        {
            // стандартная формула расстояния от точки до прямой
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            return Math.Abs(dy * pt.X - dx * pt.Y + b.X * a.Y - b.Y * a.X) / Math.Sqrt(dx * dx + dy * dy);
        }

        private void UpdateBlurEffect()
        {
            if (_blurEffect == null)
                _blurEffect = new System.Windows.Media.Effects.BlurEffect();

            _blurEffect.Radius = _blurRadius;
            this.Effect = _blurEffect;
        }
    }
}