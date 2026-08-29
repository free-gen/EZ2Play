using System;
using System.Windows;
using System.Windows.Media;

namespace EZ2Play.App
{
    public class ParticlesCanvas : FrameworkElement
    {
        public int ParticleCount { get; set; } = 300;

        private const int BrushLevels = 64;
        private const double MaxDelta = 0.05;

        private static readonly TimeSpan ParticleFrameInterval = TimeSpan.FromSeconds(1.0 / 60.0);

        private const double MinSpeed = 4.0;
        private const double MaxSpeed = 16.0;

        private const double ParticleFadeInDuration = 0.45;
        private const double ParticleFadeOutDuration = 0.45;

        private const double TwoPi = Math.PI * 2.0;

        private const byte ParticleDust = 0;
        private const byte ParticleGlow = 1;
        private const byte ParticleSpark = 2;

        private const double RespawnTimeMin = 8.0;
        private const double RespawnTimeMax = 24.0;

        private const double DustSizeMin = 0.50;
        private const double DustSizeMax = 1.50;

        private const double GlowSizeMin = 1.75;
        private const double GlowSizeMax = 3.25;

        private const double SparkSizeMin = 2.75;
        private const double SparkSizeMax = 5.75;

        private struct Particle
        {
            public double X;
            public double Y;
            public double SpeedX;
            public double SpeedY;
            public double Radius;
            public double Aspect;
            public double Depth;
            public double BaseOpacity;
            public double Opacity;
            public double Phase;
            public double DriftAmplitude;
            public double DriftFrequency;
            public double PulsePhase;
            public double PulseSpeed;
            public double FadeDelay;
            public double RespawnTimer;
            public bool FadingOut;
            public byte Type;
        }

        private Particle[] _particles;
        private readonly Random _random = new Random();
        private SolidColorBrush[] _accentBrushes;
        private SolidColorBrush[] _brightBrushes;
        private bool _isInitialized;
        private bool _isRunning;
        private bool _renderHooked;
        private TimeSpan _lastRenderingTime;
        private TimeSpan _nextRenderingTime;
        private double _time;
        private double _lastWidth;
        private double _lastHeight;
        private double _scale = 1.0;

        // Particle area coordinates are normalized to the screen size.
        private readonly Point[] _drawAreaNormalized =
        {
            // new Point(0.00, 0.48),
            new Point(0.00, 0.45),
            new Point(0.80, 1.00),
            new Point(1.20, 0.20)
        };

        private Point[] _drawArea;

        private double _targetOpacity;
        private double _currentOpacity;
        private double _globalFadeDuration = 0.5;

        public ParticlesCanvas()
        {
            IsHitTestVisible = false;

            // Keep particle motion on subpixel coordinates.
            SnapsToDevicePixels = false;

            Loaded += (s, e) => InitializeBrushes();
            Unloaded += (s, e) => Stop();
        }

        public void Start()
        {
            if (!_isInitialized)
                InitializeBrushes();

            if (!_isInitialized) return;

            if (_particles == null || _particles.Length != ParticleCount)
                InitializeParticles();

            if (_particles == null || _particles.Length == 0) return;
            if (_isRunning) return;

            _isRunning = true;
            _isRunning = true;
            _lastRenderingTime = TimeSpan.Zero;
            _nextRenderingTime = TimeSpan.Zero;

            if (!_renderHooked)
            {
                CompositionTarget.Rendering += OnRendering;
                _renderHooked = true;
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _lastRenderingTime = TimeSpan.Zero;
            _nextRenderingTime = TimeSpan.Zero;

            if (_renderHooked)
            {
                CompositionTarget.Rendering -= OnRendering;
                _renderHooked = false;
            }
        }

        public void SetParticlesVisible(bool visible, bool fade = true, double duration = 0.5)
        {
            _targetOpacity = visible ? 1.0 : 0.0;

            if (duration > 0)
                _globalFadeDuration = duration;

            if (!fade || duration <= 0)
            {
                _currentOpacity = _targetOpacity;

                if (visible)
                {
                    Start();
                }

                else
                {
                    Stop();
                    _particles = null;
                }

                InvalidateVisual();
                return;
            }

            if (visible)
                Start();
            else if (!_isRunning && _currentOpacity > 0)
                Start();
        }

        private void InitializeBrushes()
        {
            if (_isInitialized) return;

            var accentBrush = Application.Current?.Resources["AccentFillColorSecondaryBrush"] as SolidColorBrush;

            if (accentBrush == null) return;

            Color accent = accentBrush.Color;
            Color bright = MixWithWhite(accent, 0.58);

            _accentBrushes = CreateBrushRamp(accent);
            _brightBrushes = CreateBrushRamp(bright);

            _isInitialized = true;
        }

        private static SolidColorBrush[] CreateBrushRamp(Color color)
        {
            var brushes = new SolidColorBrush[BrushLevels];

            for (int i = 0; i < BrushLevels; i++)
            {
                byte alpha = (byte)Math.Round(255.0 * i / (BrushLevels - 1));

                var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));

                brush.Freeze();
                brushes[i] = brush;
            }

            return brushes;
        }

        private SolidColorBrush GetAccentBrush(double opacity)
        {
            return GetBrush(_accentBrushes, opacity);
        }

        private SolidColorBrush GetBrightBrush(double opacity)
        {
            return GetBrush(_brightBrushes, opacity);
        }

        private static SolidColorBrush GetBrush(SolidColorBrush[] brushes, double opacity)
        {
            opacity = Clamp01(opacity);

            int index = (int)Math.Round(opacity * (BrushLevels - 1));

            if (index <= 0) return null;

            return brushes[index];
        }

        private void InitializeParticles()
        {
            double width = ActualWidth;
            double height = ActualHeight;

            if (width <= 0 || height <= 0) return;

            _lastWidth = width;
            _lastHeight = height;
            _scale = GetScaleFactor();

            UpdateDrawArea(width, height);

            int count = Math.Max(0, ParticleCount);
            _particles = new Particle[count];

            for (int i = 0; i < count; i++)
                SpawnParticle(ref _particles[i], true);
        }

        private void SpawnParticle(ref Particle p, bool initialSpawn)
        {
            Point position = GetRandomPointInDrawArea();

            // Keep most particles in the background.
            p.Depth = 0.10 + Math.Pow(_random.NextDouble(), 1.50) * 0.90;

            double typeRoll = _random.NextDouble();

            if (typeRoll < 0.55)
                p.Type = ParticleDust;
            else if (typeRoll < 0.92)
                p.Type = ParticleGlow;
            else
                p.Type = ParticleSpark;

            double baseRadius;

            if (p.Type == ParticleDust)
                baseRadius = RandomRange(DustSizeMin, DustSizeMax);
            else if (p.Type == ParticleGlow)
                baseRadius = RandomRange(GlowSizeMin, GlowSizeMax);
            else
                baseRadius = RandomRange(SparkSizeMin, SparkSizeMax);

            double depthScale = 0.65 + p.Depth * 0.70;

            p.Radius = baseRadius * depthScale * _scale;
            p.Aspect = 1.0;

            double speed = Lerp(MinSpeed, MaxSpeed, p.Depth);
            speed *= 0.85 + _random.NextDouble() * 0.30;
            speed *= _scale;

            // Main particle flow moves to the right.
            p.SpeedX = speed * (0.92 + _random.NextDouble() * 0.16);

            // Add slight vertical variation.
            p.SpeedY = speed * (-0.08 + (_random.NextDouble() - 0.5) * 0.50);

            p.DriftAmplitude = Lerp(2.0, 9.0, p.Depth) * _scale * (0.80 + _random.NextDouble() * 0.40);
            p.DriftFrequency = 0.30 + _random.NextDouble() * 0.60;
            p.Phase = _random.NextDouble() * TwoPi;

            p.PulsePhase = _random.NextDouble() * TwoPi;
            p.PulseSpeed = 0.35 + _random.NextDouble() * 0.85;

            if (p.Type == ParticleDust)
                p.BaseOpacity = 0.10 + p.Depth * 0.17 + _random.NextDouble() * 0.05;
            else if (p.Type == ParticleGlow)
                p.BaseOpacity = 0.22 + p.Depth * 0.28 + _random.NextDouble() * 0.07;
            else
                p.BaseOpacity = 0.45 + p.Depth * 0.32 + _random.NextDouble() * 0.08;

            p.BaseOpacity = Clamp01(p.BaseOpacity);

            p.X = position.X;
            p.Y = position.Y;

            p.Opacity = initialSpawn ? 0.35 + _random.NextDouble() * 0.65 : 0;
            p.FadingOut = false;
            p.FadeDelay = 0;
            p.RespawnTimer = RandomRange(RespawnTimeMin, RespawnTimeMax);
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (!_isRunning) return;

            var renderingArgs = e as RenderingEventArgs;

            if (renderingArgs == null) return;

            TimeSpan renderingTime = renderingArgs.RenderingTime;

            if (_lastRenderingTime == TimeSpan.Zero)
            {
                _lastRenderingTime = renderingTime;
                _nextRenderingTime = renderingTime + ParticleFrameInterval;

                InvalidateVisual();
                return;
            }

            // Max 60 FPS fix
            if (renderingTime < _nextRenderingTime)
                return;

            double delta = (renderingTime - _lastRenderingTime).TotalSeconds;
            _lastRenderingTime = renderingTime;

            do
            {
                _nextRenderingTime += ParticleFrameInterval;
            }
            while (_nextRenderingTime <= renderingTime);

            if (delta <= 0) return;

            if (delta > MaxDelta)
                delta = MaxDelta;

            UpdateGlobalOpacity(delta);

            if (!_isRunning)
            {
                InvalidateVisual();
                return;
            }

            if (_particles != null && _particles.Length > 0)
                UpdateParticles(delta);

            InvalidateVisual();
        }

        private void UpdateGlobalOpacity(double delta)
        {
            if (Math.Abs(_currentOpacity - _targetOpacity) < 0.0001)
            {
                _currentOpacity = _targetOpacity;
                return;
            }

            double duration = Math.Max(0.01, _globalFadeDuration);
            double step = delta / duration;

            if (_targetOpacity > _currentOpacity)
            {
                _currentOpacity += step;

                if (_currentOpacity >= _targetOpacity)
                    _currentOpacity = _targetOpacity;
            }

            else
            {
                _currentOpacity -= step;

                if (_currentOpacity <= _targetOpacity)
                    _currentOpacity = _targetOpacity;
            }

            if (_targetOpacity <= 0 && _currentOpacity <= 0)
            {
                _currentOpacity = 0;
                _particles = null;
                Stop();
            }
        }

        private void UpdateParticles(double delta)
        {
            double width = ActualWidth;
            double height = ActualHeight;

            if (width <= 0 || height <= 0 || _particles == null) return;

            if (Math.Abs(width - _lastWidth) > 0.01 || Math.Abs(height - _lastHeight) > 0.01)
            {
                _lastWidth = width;
                _lastHeight = height;
                UpdateDrawArea(width, height);
            }

            _time += delta;

            double fadeInSpeed = delta / ParticleFadeInDuration;
            double fadeOutSpeed = delta / ParticleFadeOutDuration;

            for (int i = 0; i < _particles.Length; i++)
            {
                Particle p = _particles[i];

                double normalizedX = width > 0 ? p.X / width : 0;

                // A shared wave keeps the particles moving as one calm flow.
                double fieldWave = Math.Sin(_time * 0.48 + normalizedX * 3.8 + p.Phase);

                // Individual drift prevents particles from moving in formation.
                double localWave = Math.Sin(_time * p.DriftFrequency + p.Phase);
                double secondWave = Math.Cos(_time * p.DriftFrequency * 0.71 + p.Phase * 1.31);

                double velocityX = p.SpeedX + localWave * p.DriftAmplitude * 0.10;
                double velocityY = p.SpeedY + fieldWave * p.DriftAmplitude + secondWave * p.DriftAmplitude * 0.25;

                p.X += velocityX * delta;
                p.Y += velocityY * delta;

                if (!p.FadingOut)
                {
                    p.RespawnTimer -= delta;

                    if (p.RespawnTimer <= 0)
                    {
                        p.FadingOut = true;
                        p.FadeDelay = 0;
                    }
                }

                if (!p.FadingOut && !IsPointInDrawArea(new Point(p.X, p.Y)))
                {
                    p.FadingOut = true;
                    p.FadeDelay = _random.NextDouble() * 0.07;
                }

                if (p.FadingOut)
                {
                    if (p.FadeDelay > 0)
                    {
                        p.FadeDelay -= delta;
                    }

                    else
                    {
                        p.Opacity -= fadeOutSpeed;

                        if (p.Opacity <= 0)
                            SpawnParticle(ref p, false);
                    }
                }

                else if (p.Opacity < 1.0)
                {
                    p.Opacity += fadeInSpeed;

                    if (p.Opacity > 1.0)
                        p.Opacity = 1.0;
                }

                _particles[i] = p;
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_particles == null || !_isInitialized || _currentOpacity <= 0.001) return;

            for (int i = 0; i < _particles.Length; i++)
                DrawParticle(dc, _particles[i]);
        }

        private void DrawParticle(DrawingContext dc, Particle p)
        {
            if (p.Opacity <= 0) return;

            double pulseWave = 0.5 + 0.5 * Math.Sin(_time * p.PulseSpeed + p.PulsePhase);

            double pulse;

            if (p.Type == ParticleDust)
                pulse = 0.90 + pulseWave * 0.10;
            else if (p.Type == ParticleGlow)
                pulse = 0.80 + pulseWave * 0.20;
            else
                pulse = 0.72 + pulseWave * 0.28;

            double flash = p.Type == ParticleSpark ? pulseWave * pulseWave : 0;
            double opacity = Clamp01(p.BaseOpacity * p.Opacity * pulse * _currentOpacity);

            if (opacity <= 0.003) return;

            Point center = new Point(p.X, p.Y);
            double radiusX = p.Radius * p.Aspect;
            double radiusY = p.Radius;

            if (p.Type == ParticleDust)
            {
                var brush = GetAccentBrush(opacity);

                if (brush != null)
                    dc.DrawEllipse(brush, null, center, radiusX, radiusY);

                return;
            }

            if (p.Type == ParticleGlow)
            {
                var glow1 = GetAccentBrush(opacity * 0.035);
                var glow2 = GetAccentBrush(opacity * 0.070);
                var glow3 = GetAccentBrush(opacity * 0.130);
                var glow4 = GetAccentBrush(opacity * 0.240);
                var core = GetBrightBrush(opacity * 0.95);

                if (glow1 != null)
                    dc.DrawEllipse(glow1, null, center, radiusX * 2.50, radiusY * 2.50);

                if (glow2 != null)
                    dc.DrawEllipse(glow2, null, center, radiusX * 2.00, radiusY * 2.00);

                if (glow3 != null)
                    dc.DrawEllipse(glow3, null, center, radiusX * 1.50, radiusY * 1.50);

                if (glow4 != null)
                    dc.DrawEllipse(glow4, null, center, radiusX * 1.00, radiusY * 1.00);

                if (core != null)
                    dc.DrawEllipse(core, null, center, radiusX * 0.48, radiusY * 0.48);

                return;
            }

            double sparkScale = 1.0 + flash * 0.25;

            var spark1 = GetAccentBrush(opacity * (0.008 + flash * 0.012));
            var spark2 = GetAccentBrush(opacity * (0.016 + flash * 0.018));
            var spark3 = GetAccentBrush(opacity * (0.032 + flash * 0.026));
            var spark4 = GetAccentBrush(opacity * (0.064 + flash * 0.035));
            var spark5 = GetAccentBrush(opacity * (0.128 + flash * 0.045));
            var spark6 = GetAccentBrush(opacity * (0.256 + flash * 0.060));
            var spark7 = GetBrightBrush(opacity * (0.512 + flash * 0.080));
            var sparkCore = GetBrightBrush(Clamp01(opacity * (1.0 + flash * 0.35)));

            if (spark1 != null)
                dc.DrawEllipse(spark1, null, center, radiusX * 3.10 * sparkScale, radiusY * 3.10 * sparkScale);

            if (spark2 != null)
                dc.DrawEllipse(spark2, null, center, radiusX * 2.75 * sparkScale, radiusY * 2.75 * sparkScale);

            if (spark3 != null)
                dc.DrawEllipse(spark3, null, center, radiusX * 2.40 * sparkScale, radiusY * 2.40 * sparkScale);

            if (spark4 != null)
                dc.DrawEllipse(spark4, null, center, radiusX * 2.05 * sparkScale, radiusY * 2.05 * sparkScale);

            if (spark5 != null)
                dc.DrawEllipse(spark5, null, center, radiusX * 1.70 * sparkScale, radiusY * 1.70 * sparkScale);

            if (spark6 != null)
                dc.DrawEllipse(spark6, null, center, radiusX * 1.35 * sparkScale, radiusY * 1.35 * sparkScale);

            if (spark7 != null)
                dc.DrawEllipse(spark7, null, center, radiusX * 1.00 * sparkScale, radiusY * 1.00 * sparkScale);

            if (sparkCore != null)
                dc.DrawEllipse(sparkCore, null, center, radiusX * 0.75, radiusY * 0.75);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (sizeInfo.NewSize.Width <= 0 || sizeInfo.NewSize.Height <= 0) return;

            _lastWidth = sizeInfo.NewSize.Width;
            _lastHeight = sizeInfo.NewSize.Height;
            _scale = GetScaleFactor();

            UpdateDrawArea(_lastWidth, _lastHeight);

            if (_particles != null && _particles.Length > 0 && _isInitialized)
                InitializeParticles();

            InvalidateVisual();
        }

        private double GetScaleFactor()
        {
            Window window = Window.GetWindow(this);
            double height = window != null ? window.ActualHeight : 0;

            if (height <= 0)
                height = LayoutScaler.ReferenceHeight;

            return LayoutScaler.GetScaleFactor(height);
        }

        private void UpdateDrawArea(double width, double height)
        {
            _drawArea = new Point[_drawAreaNormalized.Length];

            for (int i = 0; i < _drawAreaNormalized.Length; i++)
            {
                _drawArea[i] = new Point(
                    _drawAreaNormalized[i].X * width,
                    _drawAreaNormalized[i].Y * height);
            }
        }

        private Point GetRandomPointInDrawArea()
        {
            if (_drawArea == null || _drawArea.Length < 3)
                return new Point(0, 0);

            double minX = _drawArea[0].X;
            double maxX = _drawArea[0].X;
            double minY = _drawArea[0].Y;
            double maxY = _drawArea[0].Y;

            for (int i = 1; i < _drawArea.Length; i++)
            {
                minX = Math.Min(minX, _drawArea[i].X);
                maxX = Math.Max(maxX, _drawArea[i].X);
                minY = Math.Min(minY, _drawArea[i].Y);
                maxY = Math.Max(maxY, _drawArea[i].Y);
            }

            for (int attempt = 0; attempt < 100; attempt++)
            {
                Point point = new Point(
                    RandomRange(minX, maxX),
                    RandomRange(minY, maxY));

                if (IsPointInDrawArea(point))
                    return point;
            }

            // Fall back to the center if the area is too narrow for random sampling.
            double centerX = 0;
            double centerY = 0;

            for (int i = 0; i < _drawArea.Length; i++)
            {
                centerX += _drawArea[i].X;
                centerY += _drawArea[i].Y;
            }

            return new Point(centerX / _drawArea.Length, centerY / _drawArea.Length);
        }

        private bool IsPointInDrawArea(Point point)
        {
            if (_drawArea == null || _drawArea.Length < 3) return false;

            bool inside = false;

            for (int i = 0, j = _drawArea.Length - 1; i < _drawArea.Length; j = i++)
            {
                Point pi = _drawArea[i];
                Point pj = _drawArea[j];

                bool intersects =
                    ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                    (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X);

                if (intersects)
                    inside = !inside;
            }

            return inside;
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        private double RandomRange(double min, double max)
        {
            return min + _random.NextDouble() * (max - min);
        }

        private static double Clamp01(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;

            return value;
        }

        private static Color MixWithWhite(Color color, double amount)
        {
            amount = Clamp01(amount);

            byte r = (byte)(color.R + (255 - color.R) * amount);
            byte g = (byte)(color.G + (255 - color.G) * amount);
            byte b = (byte)(color.B + (255 - color.B) * amount);

            return Color.FromArgb(255, r, g, b);
        }
    }
}