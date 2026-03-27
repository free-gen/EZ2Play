using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace EZ2Play.App
{
    public class ParticlesCanvas : FrameworkElement
    {
        // ----------------- НАСТРОЙКИ -----------------

        public int ParticleCount { get; set; } = 160;

        private readonly double[] _sizes = { 2, 4, 6, 8 };
        private readonly int[] _alphaValues = { 16, 64, 128, 224 };
        private readonly double[] _alphaWeights = { 0.58, 0.30, 0.1, 0.02 };

        private const double BaseMaxSpeed = 16;
        private const double BaseFadeMarginMax = 50;

        // ----------------- СТРУКТУРЫ -----------------

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

        // ----------------- ВНУТРЕННИЕ ДАННЫЕ -----------------

        private Particle[] _particles;
        private Brush[] _brushes;
        private readonly Random _random = new Random();
        private readonly DispatcherTimer _timer;
        private bool _needsRedraw = true;
        private bool _isInitialized = false;
        private double _lastWidth;
        private double _lastHeight;

        // Вершины треугольника (кэшируются при изменении размеров)
        private Point _triP1, _triP2, _triP3;

        private DispatcherTimer _fadeTimer;
        private double _targetOpacity = 1.0;
        private double _currentOpacity = 0.0;

        // ----------------- КОНСТРУКТОР -----------------

        public ParticlesCanvas()
        {
            Loaded += (s, e) =>
            {
                InitializeBrushes();
                // Start();
            };
            Unloaded += (s, e) => Stop();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _timer.Tick += OnTimerTick;

            _fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _fadeTimer.Tick += OnFadeTick;
        }

        // ----------------- PUBLIC API -----------------

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

        // ----------------- ИНИЦИАЛИЗАЦИЯ -----------------

        private void InitializeBrushes()
        {
            if (_brushes != null)
                return;

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

            // Обновляем треугольник для новых размеров
            UpdateTriangle(width, height);

            double scale = GetScaleFactor();
            double maxSpeed = BaseMaxSpeed * scale;
            _particles = new Particle[ParticleCount];

            for (int i = 0; i < ParticleCount; i++)
            {
                double radius = _sizes[_random.Next(_sizes.Length)] * scale;
                int alphaIndex = SelectWithWeights(_alphaWeights);
                var pos = GetRandomPointInTriangle(); // используем кэшированные вершины
                _particles[i] = new Particle
                {
                    X = pos.X,
                    Y = pos.Y,
                    SpeedX = (_random.NextDouble() - 0.5) * 2 * maxSpeed,
                    SpeedY = (_random.NextDouble() - 0.5) * 2 * maxSpeed,
                    Radius = radius,
                    Brush = _brushes[alphaIndex],
                    Opacity = 1.0,
                    FadingOut = false
                };
            }
            _needsRedraw = true;
        }

        // ----------------- ТАЙМЕР -----------------

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

        // ----------------- ОБНОВЛЕНИЕ ЧАСТИЦ -----------------

        private void UpdateParticles(double delta)
        {
            double width = ActualWidth;
            double height = ActualHeight;

            if (width <= 0 || height <= 0 || _particles == null)
                return;

            // Если размеры изменились, обновляем треугольник
            if (Math.Abs(width - _lastWidth) > 0.01 || Math.Abs(height - _lastHeight) > 0.01)
            {
                _lastWidth = width;
                _lastHeight = height;
                UpdateTriangle(width, height);
            }

            bool anyVisible = false;
            double fadeSpeed = delta / 0.2;

            for (int i = 0; i < _particles.Length; i++)
            {
                var p = _particles[i];

                p.X += p.SpeedX * delta;
                p.Y += p.SpeedY * delta;

                if (!p.FadingOut && !IsPointInTriangle(new Point(p.X, p.Y))) // используем кэш
                {
                    p.FadingOut = true;
                    double fadeMarginMax = BaseFadeMarginMax * GetScaleFactor();

                    if (_random.NextDouble() < 0.5)
                        p.FadeMargin = 0;
                    else
                        p.FadeMargin = _random.NextDouble() * fadeMarginMax;
                }

                if (p.FadingOut)
                {
                    double overDistance = GetDistanceOutsideTriangle(new Point(p.X, p.Y)); // используем кэш
                    if (overDistance > p.FadeMargin)
                    {
                        p.Opacity -= fadeSpeed;

                        if (p.Opacity <= 0)
                        {
                            RespawnParticle(ref p, width, height);
                        }
                    }
                }
                else if (p.Opacity < 1.0)
                {
                    p.Opacity += fadeSpeed;
                    if (p.Opacity > 1)
                        p.Opacity = 1;
                }

                _particles[i] = p;

                if (p.Opacity > 0)
                    anyVisible = true;
            }

            _needsRedraw = anyVisible;
        }

        private void RespawnParticle(ref Particle p, double width, double height)
        {
            double scale = GetScaleFactor();
            double maxSpeed = BaseMaxSpeed * scale;
            var pos = GetRandomPointInTriangle(); // используем кэш
            int radiusIndex = _random.Next(_sizes.Length);
            int alphaIndex = SelectWithWeights(_alphaWeights);

            p.X = pos.X;
            p.Y = pos.Y;
            p.SpeedX = (_random.NextDouble() - 0.5) * 2 * maxSpeed;
            p.SpeedY = (_random.NextDouble() - 0.5) * 2 * maxSpeed;
            p.Radius = _sizes[radiusIndex] * scale;
            p.Brush = _brushes[alphaIndex];
            p.Opacity = 0;
            p.FadingOut = false;
            p.FadeMargin = 0;
        }

        // ----------------- ОТРИСОВКА -----------------

        protected override void OnRender(DrawingContext dc)
        {
            if (_particles == null || _brushes == null || _currentOpacity <= 0) return;

            foreach (var p in _particles)
            {
                if (p.Brush == null || p.Opacity <= 0) continue;

                var brush = p.Brush.Clone();
                brush.Opacity = p.Opacity * _currentOpacity;
                brush.Freeze();
                dc.DrawEllipse(brush, null, new Point(p.X + p.Radius, p.Y + p.Radius), p.Radius, p.Radius);
            }
        }

        private void OnFadeTick(object sender, EventArgs e)
        {
            if (_currentOpacity == _targetOpacity)
            {
                _fadeTimer.Stop();
                if (_targetOpacity == 0) { Stop(); _particles = new Particle[0]; }
                return;
            }

            _currentOpacity += (_targetOpacity > _currentOpacity ? 0.05 : -0.05);
            if (Math.Abs(_currentOpacity - _targetOpacity) < 0.01) _currentOpacity = _targetOpacity;
            
            _needsRedraw = true;
            InvalidateVisual();
        }

        public void SetParticlesVisible(bool visible, bool fade = true, double duration = 0.5)
        {
            _targetOpacity = visible ? 1.0 : 0.0;
            
            if (!fade || duration <= 0)
            {
                _currentOpacity = _targetOpacity;
                _fadeTimer.Stop();
                if (!visible) { Stop(); _particles = new Particle[0]; }
                else Start();
            }
            else
            {
                if (visible) Start();
                _fadeTimer.Start();
            }
            
            _needsRedraw = true;
            InvalidateVisual();
        }

        // ----------------- RESIZE -----------------

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (sizeInfo.NewSize.Width <= 0 || sizeInfo.NewSize.Height <= 0)
                return;

            _lastWidth = 0;
            _lastHeight = 0;

            if (_particles != null && _particles.Length > 0 && _isInitialized)
                InitializeParticles();
        }

        // ----------------- МАТЕМАТИКА / ГЕОМЕТРИЯ -----------------

        private double GetScaleFactor()
        {
            var w = Window.GetWindow(this);
            double h = w != null ? w.ActualHeight : 0;

            if (h <= 0)
                h = LayoutScaler.ReferenceHeight;

            return LayoutScaler.GetScaleFactor(h);
        }

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

        // Обновляет вершины треугольника на основе текущих размеров canvas
        private void UpdateTriangle(double width, double height)
        {
            _triP1 = new Point(0, height * 0.5);
            _triP2 = new Point(width * 0.9, height);
            _triP3 = new Point(width, height * 0.3);
        }

        // Генерирует случайную точку внутри треугольника (использует кэшированные вершины)
        private Point GetRandomPointInTriangle()
        {
            double r1 = _random.NextDouble();
            double r2 = _random.NextDouble();

            if (r1 + r2 > 1)
            {
                r1 = 1 - r1;
                r2 = 1 - r2;
            }

            double x = _triP1.X + r1 * (_triP2.X - _triP1.X) + r2 * (_triP3.X - _triP1.X);
            double y = _triP1.Y + r1 * (_triP2.Y - _triP1.Y) + r2 * (_triP3.Y - _triP1.Y);

            return new Point(x, y);
        }

        // Проверяет, находится ли точка внутри треугольника (использует кэшированные вершины)
        private bool IsPointInTriangle(Point pt)
        {
            double dX = pt.X - _triP3.X;
            double dY = pt.Y - _triP3.Y;
            double dX21 = _triP3.X - _triP2.X;
            double dY12 = _triP2.Y - _triP3.Y;
            double D = dY12 * (_triP1.X - _triP3.X) + dX21 * (_triP1.Y - _triP3.Y);
            double s = dY12 * dX + dX21 * dY;
            double t = (_triP3.Y - _triP1.Y) * dX + (_triP1.X - _triP3.X) * dY;

            if (D < 0)
                return s <= 0 && t <= 0 && s + t >= D;

            return s >= 0 && t >= 0 && s + t <= D;
        }

        // Вычисляет расстояние от точки до границ треугольника (использует кэшированные вершины)
        private double GetDistanceOutsideTriangle(Point pt)
        {
            double d1 = DistancePointToLine(pt, _triP1, _triP2);
            double d2 = DistancePointToLine(pt, _triP2, _triP3);
            double d3 = DistancePointToLine(pt, _triP3, _triP1);

            return Math.Min(d1, Math.Min(d2, d3));
        }

        private double DistancePointToLine(Point pt, Point a, Point b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;

            return Math.Abs(dy * pt.X - dx * pt.Y + b.X * a.Y - b.Y * a.X)
                   / Math.Sqrt(dx * dx + dy * dy);
        }
    }
}