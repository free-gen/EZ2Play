using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EZ2Play.App
{
    public class Selector : Control
    {
        private const double GlowStartOffset = -3;
        private const double GlowEndOffset = 3;
        private const double GlowInterval = 8;
        private const double GlowDuration = 2;

        private DateTime _startTime = DateTime.UtcNow;
        private bool _isRenderingHooked;
        private FrameworkElement _target;
        private SelectorProfile _profile;
        private Color _selectionColor;
        private SolidColorBrush _backgroundBrush;

        private LinearGradientBrush _strokeGlowBrush;
        private LinearGradientBrush _backgroundGlowBrush;
        private Pen _strokePen;
        private double _cachedStrokeThickness = double.NaN;
        private bool _renderResourcesInitialized;

        private bool _selectionColorInitialized;
        private Rect _targetBounds;

        public sealed class SelectorProfile
        {
            public double StrokeInset { get; }
            public double StrokeOutset { get; }
            public double StrokeThickness { get; }
            public double StrokeRadius { get; }
            public double BackgroundInset { get; }
            public double BackgroundOutset { get; }
            public double BackgroundRadius { get; }

            public SelectorProfile(double strokeInset, double strokeOutset, 
                double strokeThickness, double strokeRadius, double backgroundInset, 
                double backgroundOutset, double backgroundRadius)
            {
                StrokeInset = Math.Max(0, strokeInset);
                StrokeOutset = Math.Max(0, strokeOutset);
                StrokeThickness = Math.Max(0, strokeThickness);
                StrokeRadius = Math.Max(0, strokeRadius);
                BackgroundInset = Math.Max(0, backgroundInset);
                BackgroundOutset = Math.Max(0, backgroundOutset);
                BackgroundRadius = Math.Max(0, backgroundRadius);
            }
        }

        private double GetScaledValue(string key)
        {
            if (TryFindResource(key) is double value)
                return value;

            throw new InvalidOperationException($"Selector resource '{key}' is not configured.");
        }

        public SelectorProfile CreateMediaProfile()
        {
            return new SelectorProfile(
                strokeInset: GetScaledValue(UiScaleKeys.SelectorMediaStrokeInset),
                strokeOutset: GetScaledValue(UiScaleKeys.SelectorMediaStrokeOutset),
                strokeThickness: GetScaledValue(UiScaleKeys.SelectorMediaStrokeThickness),
                strokeRadius: GetScaledValue(UiScaleKeys.SelectorMediaStrokeRadius),
                backgroundInset: GetScaledValue(UiScaleKeys.SelectorMediaBackgroundInset),
                backgroundOutset: GetScaledValue(UiScaleKeys.SelectorMediaBackgroundOutset),
                backgroundRadius: GetScaledValue(UiScaleKeys.SelectorMediaBackgroundRadius));
        }

        public SelectorProfile CreateOverlayItemProfile()
        {
            return new SelectorProfile(
                strokeInset: GetScaledValue(UiScaleKeys.SelectorOverlayItemStrokeInset),
                strokeOutset: GetScaledValue(UiScaleKeys.SelectorOverlayItemStrokeOutset),
                strokeThickness: GetScaledValue(UiScaleKeys.SelectorOverlayItemStrokeThickness),
                strokeRadius: GetScaledValue(UiScaleKeys.SelectorOverlayItemStrokeRadius),
                backgroundInset: GetScaledValue(UiScaleKeys.SelectorOverlayItemBackgroundInset),
                backgroundOutset: GetScaledValue(UiScaleKeys.SelectorOverlayItemBackgroundOutset),
                backgroundRadius: GetScaledValue(UiScaleKeys.SelectorOverlayItemBackgroundRadius));
        }

        public static readonly DependencyProperty GlowOffsetProperty =
            DependencyProperty.Register(nameof(GlowOffset), typeof(double), typeof(Selector), 
            new FrameworkPropertyMetadata(GlowStartOffset, FrameworkPropertyMetadataOptions.AffectsRender));

        public double GlowOffset
        {
            get => (double)GetValue(GlowOffsetProperty);
            set => SetValue(GlowOffsetProperty, value);
        }

        public FrameworkElement Target => _target;

        public Selector()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            ClipToBounds = false;
        }

        public void Attach(FrameworkElement target)
        {
            Attach(target, CreateOverlayItemProfile());
        }

        public void Attach(FrameworkElement target, SelectorProfile profile)
        {
            if (profile == null)
                profile = CreateOverlayItemProfile();

            if (target == null)
            {
                Detach();
                return;
            }

            if (ReferenceEquals(_target, target) && ProfilesEqual(_profile, profile))
            {
                UpdateTargetBounds();
                return;
            }

            Detach();

            _target = target;
            _profile = profile;

            _target.LayoutUpdated += OnTargetLayoutUpdated;
            _target.IsVisibleChanged += OnTargetVisibilityChanged;
            _target.Unloaded += OnTargetUnloaded;

            UpdateTargetBounds();
            HookRendering();
            InvalidateVisual();
        }

        private static bool ProfilesEqual(SelectorProfile first, SelectorProfile second)
        {
            if (ReferenceEquals(first, second))
                return true;

            if (first == null || second == null)
                return false;

            return first.StrokeInset == second.StrokeInset &&
                first.StrokeOutset == second.StrokeOutset &&
                first.StrokeThickness == second.StrokeThickness &&
                first.StrokeRadius == second.StrokeRadius &&
                first.BackgroundInset == second.BackgroundInset &&
                first.BackgroundOutset == second.BackgroundOutset &&
                first.BackgroundRadius == second.BackgroundRadius;
        }

        public void Detach()
        {
            if (_target != null)
            {
                _target.LayoutUpdated -= OnTargetLayoutUpdated;
                _target.IsVisibleChanged -= OnTargetVisibilityChanged;
                _target.Unloaded -= OnTargetUnloaded;
            }

            _target = null;
            _profile = null;
            _targetBounds = Rect.Empty;

            UnhookRendering();
            InvalidateVisual();
        }

        private void HookRendering()
        {
            if (_isRenderingHooked)
                return;

            CompositionTarget.Rendering += OnRendering;
            _isRenderingHooked = true;
        }

        private void UnhookRendering()
        {
            if (!_isRenderingHooked)
                return;

            CompositionTarget.Rendering -= OnRendering;
            _isRenderingHooked = false;
        }

        private void OnTargetLayoutUpdated(object sender, EventArgs e)
        {
            UpdateTargetBounds();
        }

        private void OnTargetVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateTargetBounds();
        }

        private void OnTargetUnloaded(object sender, RoutedEventArgs e)
        {
            Detach();
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (_target == null)
                return;

            double totalSeconds = (DateTime.UtcNow - _startTime).TotalSeconds;

            if (totalSeconds < GlowInterval)
            {
                SetGlowOffset(GlowStartOffset);
                InvalidateVisual();
                return;
            }

            double animationTime = (totalSeconds - GlowInterval) % GlowInterval;
            UpdateGlow(animationTime);
        }

        private void UpdateGlow(double animationTime)
        {
            if (_target == null)
                return;

            if (animationTime < GlowDuration)
            {
                double progress = animationTime / GlowDuration;
                progress = progress * progress * (3 - 2 * progress);

                SetGlowOffset(GlowStartOffset + (GlowEndOffset - GlowStartOffset) * progress);
            }
            else
            {
                SetGlowOffset(GlowStartOffset);
            }

            InvalidateVisual();
        }

        private void SetGlowOffset(double value)
        {
            if (Math.Abs(GlowOffset - value) < 0.0001)
                return;

            GlowOffset = value;

            if (!_renderResourcesInitialized)
                return;

            UpdateGlowBrushOffsets(_strokeGlowBrush);
            UpdateGlowBrushOffsets(_backgroundGlowBrush);
        }

        private void EnsureRenderResources(double strokeThickness)
        {
            GetSelectionColor();

            if (_renderResourcesInitialized &&
                Math.Abs(_cachedStrokeThickness - strokeThickness) < 0.0001)
                return;

            _strokeGlowBrush = CreateGlowBrush(140, 255, 1.5);
            _backgroundGlowBrush = CreateGlowBrush(0, 32, 1.5);
            _strokePen = new Pen(_strokeGlowBrush, strokeThickness);

            _cachedStrokeThickness = strokeThickness;
            _renderResourcesInitialized = true;
        }

        private void UpdateGlowBrushOffsets(LinearGradientBrush brush)
        {
            if (brush == null)
                return;

            brush.GradientStops[0].Offset = GlowOffset - 1.5;
            brush.GradientStops[1].Offset = GlowOffset;
            brush.GradientStops[2].Offset = GlowOffset + 1.5;
        }

        private LinearGradientBrush CreateGlowBrush(byte sideAlpha, byte centerAlpha, double glowWidth)
        {
            Color baseColor = GetSelectionColor();

            return new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                RelativeTransform = new RotateTransform(45, 0.5, 0.5),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(
                        sideAlpha, baseColor.R, baseColor.G, baseColor.B), GlowOffset - glowWidth),
                    new GradientStop(Color.FromArgb(
                        centerAlpha, baseColor.R, baseColor.G, baseColor.B), GlowOffset),
                    new GradientStop(Color.FromArgb(
                        sideAlpha, baseColor.R, baseColor.G, baseColor.B), GlowOffset + glowWidth)
                }
            };
        }

        private Color GetSelectionColor()
        {
            if (_selectionColorInitialized)
                return _selectionColor;

            var brush = TryFindResource("SelectionBrush") as SolidColorBrush;

            _selectionColor = brush?.Color ?? Colors.White;

            _backgroundBrush = new SolidColorBrush(
                Color.FromArgb(13, _selectionColor.R, _selectionColor.G, _selectionColor.B));
            
            _backgroundBrush.Freeze();
            _selectionColorInitialized = true;

            return _selectionColor;
        }

        private void DrawAnimatedStroke(
            DrawingContext drawingContext, Rect rect, double radius, double thickness)
        {
            EnsureRenderResources(thickness);
            drawingContext.DrawRoundedRectangle(null, _strokePen, rect, radius, radius);
        }

        private void DrawAnimatedBackgroundHighlight(
            DrawingContext drawingContext, Rect rect, double radius)
        {
            drawingContext.DrawRoundedRectangle(_backgroundBrush, null, rect, radius, radius);

            drawingContext.DrawRoundedRectangle(
                _backgroundGlowBrush, null, rect, radius, radius);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (_target == null ||
                _profile == null ||
                !_target.IsVisible ||
                ActualWidth <= 0 ||
                ActualHeight <= 0)
            {
                return;
            }

            EnsureRenderResources(_profile.StrokeThickness);

            Rect targetRect = _targetBounds;
            Rect strokeEdgeRect = GetOffsetRect(targetRect, _profile.StrokeInset, _profile.StrokeOutset);
            Rect strokeRect = GetOffsetRect(strokeEdgeRect, _profile.StrokeThickness / 2.0, 0);
            Rect backgroundRect = GetOffsetRect(targetRect, _profile.BackgroundInset, _profile.BackgroundOutset);

            if (backgroundRect.Width > 0 && backgroundRect.Height > 0)
            {
                DrawAnimatedBackgroundHighlight(drawingContext, backgroundRect, _profile.BackgroundRadius);
            }

            if (strokeRect.Width > 0 && strokeRect.Height > 0 && _profile.StrokeThickness > 0)
            {
                DrawAnimatedStroke(
                    drawingContext, strokeRect, _profile.StrokeRadius, _profile.StrokeThickness);
            }
        }

        private void UpdateTargetBounds()
        {
            if (_target == null)
                return;

            var parent = Parent as Visual;

            if (parent == null)
                return;

            try
            {
                Rect bounds = _target.TransformToAncestor(parent).TransformBounds(
                    new Rect(0, 0, _target.ActualWidth, _target.ActualHeight));

                if (AreClose(_targetBounds.X, bounds.X) &&
                    AreClose(_targetBounds.Y, bounds.Y) &&
                    AreClose(_targetBounds.Width, bounds.Width) &&
                    AreClose(_targetBounds.Height, bounds.Height))
                    return;

                _targetBounds = bounds;
                InvalidateVisual();
            }
            catch (InvalidOperationException)
            {
            }
        }

        private Rect GetVisualBounds()
        {
            if (_profile == null || _targetBounds.IsEmpty)
                return Rect.Empty;

            Rect strokeBounds = GetOffsetRect(
                _targetBounds, _profile.StrokeInset, _profile.StrokeOutset);
            strokeBounds = GetOffsetRect(strokeBounds, _profile.StrokeThickness / 2.0, 0);

            Rect backgroundBounds = GetOffsetRect(
                _targetBounds, _profile.BackgroundInset, _profile.BackgroundOutset);

            return Union(strokeBounds, backgroundBounds);
        }

        private static Rect GetOffsetRect(Rect rect, double inset, double outset)
        {
            double offset = outset - inset;

            return new Rect(rect.X - offset, rect.Y - offset, Math.Max(0, rect.Width + offset * 2), 
                Math.Max(0, rect.Height + offset * 2));
        }

        private static Rect Union(Rect first, Rect second)
        {
            if (first.IsEmpty)
                return second;

            if (second.IsEmpty)
                return first;

            double left = Math.Min(first.Left, second.Left);
            double top = Math.Min(first.Top, second.Top);
            double right = Math.Max(first.Right, second.Right);
            double bottom = Math.Max(first.Bottom, second.Bottom);

            return new Rect(left, top, right - left, bottom - top);
        }

        private static bool AreClose(double first, double second)
        {
            return Math.Abs(first - second) < 0.01;
        }
    }
}