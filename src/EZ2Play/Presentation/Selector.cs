
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EZ2Play.App
{
    /// <summary>
    /// Draws an animated selection indicator around a target
    /// WPF element and follows its position in the visual tree.
    /// </summary>
    /// <remarks>
    /// The selector supports separate appearance profiles
    /// for media items and overlay controls.
    ///
    /// Its geometry is updated during WPF rendering,
    /// allowing the highlight to follow moving targets.
    ///
    /// The selector also supports clipping to a specified
    /// visual scope and an animated gradient glow.
    /// </remarks>
    public class Selector : Control
    {
        // Animated glow settings.
        private const double GlowStartOffset = -3;
        private const double GlowEndOffset = 3;
        private const double GlowInterval = 8;
        private const double GlowDuration = 2;

        // Rendering lifecycle and target state.
        private DateTime _startTime = DateTime.UtcNow;
        private bool _isRenderingHooked;

        private FrameworkElement _target;
        private SelectorProfile _profile;

        // Selection colors and drawing resources.
        private Color _selectionColor;
        private SolidColorBrush _backgroundBrush;

        private LinearGradientBrush _strokeGlowBrush;
        private LinearGradientBrush _backgroundGlowBrush;
        private Pen _strokePen;

        private double _cachedStrokeThickness = double.NaN;
        private bool _renderResourcesInitialized;
        private bool _selectionColorInitialized;

        // Cached target bounds and optional clipping scope.
        private Rect _targetBounds;
        private FrameworkElement _clipScope;

        private readonly RectangleGeometry _clipGeometry =
            new RectangleGeometry();

        /// <summary>
        /// Defines the visual geometry of a selection indicator.
        /// </summary>
        /// <remarks>
        /// Stroke and background dimensions are configured
        /// independently to support different selection styles.
        ///
        /// All supplied dimensions are restricted to
        /// non-negative values.
        /// </remarks>
        public sealed class SelectorProfile
        {
            public double StrokeInset { get; }
            public double StrokeOutset { get; }
            public double StrokeThickness { get; }
            public double StrokeRadius { get; }

            public double BackgroundInset { get; }
            public double BackgroundOutset { get; }
            public double BackgroundRadius { get; }

            public SelectorProfile(
                double strokeInset,
                double strokeOutset,
                double strokeThickness,
                double strokeRadius,
                double backgroundInset,
                double backgroundOutset,
                double backgroundRadius)
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

        /// <summary>
        /// Retrieves a scaled selector dimension from
        /// the current WPF resource dictionary.
        /// </summary>
        private double GetScaledValue(string key)
        {
            if (TryFindResource(key) is double value)
                return value;

            throw new InvalidOperationException(
                $"Selector resource '{key}' is not configured.");
        }

        /// <summary>
        /// Creates the selection profile used for media items,
        /// including game covers and artwork.
        /// </summary>
        public SelectorProfile CreateMediaProfile()
        {
            return new SelectorProfile(
                strokeInset: GetScaledValue(
                    UiScaleKeys.SelectorMediaStrokeInset),
                strokeOutset: GetScaledValue(
                    UiScaleKeys.SelectorMediaStrokeOutset),
                strokeThickness: GetScaledValue(
                    UiScaleKeys.SelectorMediaStrokeThickness),
                strokeRadius: GetScaledValue(
                    UiScaleKeys.SelectorMediaStrokeRadius),
                backgroundInset: GetScaledValue(
                    UiScaleKeys.SelectorMediaBackgroundInset),
                backgroundOutset: GetScaledValue(
                    UiScaleKeys.SelectorMediaBackgroundOutset),
                backgroundRadius: GetScaledValue(
                    UiScaleKeys.SelectorMediaBackgroundRadius));
        }

        /// <summary>
        /// Creates the selection profile used for
        /// interactive items inside overlays.
        /// </summary>
        public SelectorProfile CreateOverlayItemProfile()
        {
            return new SelectorProfile(
                strokeInset: GetScaledValue(
                    UiScaleKeys.SelectorOverlayItemStrokeInset),
                strokeOutset: GetScaledValue(
                    UiScaleKeys.SelectorOverlayItemStrokeOutset),
                strokeThickness: GetScaledValue(
                    UiScaleKeys.SelectorOverlayItemStrokeThickness),
                strokeRadius: GetScaledValue(
                    UiScaleKeys.SelectorOverlayItemStrokeRadius),
                backgroundInset: GetScaledValue(
                    UiScaleKeys.SelectorOverlayItemBackgroundInset),
                backgroundOutset: GetScaledValue(
                    UiScaleKeys.SelectorOverlayItemBackgroundOutset),
                backgroundRadius: GetScaledValue(
                    UiScaleKeys.SelectorOverlayItemBackgroundRadius));
        }

        /// <summary>
        /// Dependency property controlling the animated
        /// gradient position.
        /// </summary>
        public static readonly DependencyProperty GlowOffsetProperty =
            DependencyProperty.Register(
                nameof(GlowOffset),
                typeof(double),
                typeof(Selector),
                new FrameworkPropertyMetadata(
                    GlowStartOffset,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public double GlowOffset
        {
            get => (double)GetValue(GlowOffsetProperty);
            set => SetValue(GlowOffsetProperty, value);
        }

        /// <summary>
        /// Gets the element currently followed by the selector.
        /// </summary>
        public FrameworkElement Target => _target;

        public Selector()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            ClipToBounds = false;
        }

        /// <summary>
        /// Assigns the visual element whose bounds will
        /// restrict selector rendering.
        /// </summary>
        /// <remarks>
        /// The clipping geometry is updated in selector
        /// coordinates to account for the visual hierarchy.
        /// </remarks>
        public void SetClipScope(FrameworkElement scope)
        {
            if (ReferenceEquals(_clipScope, scope))
                return;

            _clipScope = scope;

            Clip = scope == null
                ? null
                : _clipGeometry;

            UpdateClipBounds();
        }

        /// <summary>
        /// Updates the clipping rectangle using the current
        /// position and dimensions of the clipping scope.
        /// </summary>
        private void UpdateClipBounds()
        {
            if (_clipScope == null)
                return;

            Rect bounds = Rect.Empty;

            if (_clipScope.IsVisible &&
                _clipScope.ActualWidth > 0 &&
                _clipScope.ActualHeight > 0)
            {
                try
                {
                    bounds = _clipScope.TransformToVisual(this)
                        .TransformBounds(
                            new Rect(
                                0,
                                0,
                                _clipScope.ActualWidth,
                                _clipScope.ActualHeight));
                }
                catch (InvalidOperationException)
                {
                }
            }

            // Avoid updating the geometry when its bounds
            // have not changed.
            if (!_clipGeometry.Rect.Equals(bounds))
                _clipGeometry.Rect = bounds;
        }

        /// <summary>
        /// Attaches the selector to a target element using
        /// the specified appearance profile.
        /// </summary>
        /// <remarks>
        /// Reattaching to the same target with an equivalent
        /// profile only refreshes the target bounds.
        ///
        /// Otherwise, the previous target is detached
        /// before subscribing to the new target's events.
        /// </remarks>
        public void Attach(
            FrameworkElement target,
            SelectorProfile profile)
        {
            if (profile == null)
                profile = CreateOverlayItemProfile();

            if (target == null)
            {
                Detach();
                return;
            }

            if (ReferenceEquals(_target, target) &&
                ProfilesEqual(_profile, profile))
            {
                UpdateTargetBounds();
                return;
            }

            Detach();

            _target = target;
            _profile = profile;

            _target.IsVisibleChanged += OnTargetVisibilityChanged;
            _target.Unloaded += OnTargetUnloaded;

            UpdateTargetBounds();
            HookRendering();
            InvalidateVisual();
        }

        /// <summary>
        /// Compares selector profiles by their geometry values.
        /// </summary>
        private static bool ProfilesEqual(
            SelectorProfile first,
            SelectorProfile second)
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

        /// <summary>
        /// Detaches the current target, stops rendering updates,
        /// and clears the selection geometry.
        /// </summary>
        public void Detach()
        {
            if (_target != null)
            {
                _target.IsVisibleChanged -= OnTargetVisibilityChanged;
                _target.Unloaded -= OnTargetUnloaded;
            }

            _target = null;
            _profile = null;
            _targetBounds = Rect.Empty;

            UnhookRendering();
            InvalidateVisual();
        }

        /// <summary>
        /// Subscribes to WPF rendering updates if
        /// the selector is not already subscribed.
        /// </summary>
        private void HookRendering()
        {
            if (_isRenderingHooked)
                return;

            CompositionTarget.Rendering += OnRendering;
            _isRenderingHooked = true;
        }

        /// <summary>
        /// Removes the WPF rendering subscription.
        /// </summary>
        private void UnhookRendering()
        {
            if (!_isRenderingHooked)
                return;

            CompositionTarget.Rendering -= OnRendering;
            _isRenderingHooked = false;
        }

        /// <summary>
        /// Refreshes selector geometry when the target
        /// visibility changes.
        /// </summary>
        private void OnTargetVisibilityChanged(
            object sender,
            DependencyPropertyChangedEventArgs e)
        {
            UpdateTargetBounds();
            InvalidateVisual();
        }

        /// <summary>
        /// Detaches the selector when its target
        /// leaves the visual tree.
        /// </summary>
        private void OnTargetUnloaded(
            object sender,
            RoutedEventArgs e)
        {
            Detach();
        }

        /// <summary>
        /// Tracks the target position and updates
        /// the animated glow during WPF rendering.
        /// </summary>
        /// <remarks>
        /// Target and clipping bounds are updated before
        /// processing the glow animation.
        ///
        /// The glow uses elapsed UTC time rather than
        /// restarting whenever the target changes.
        /// </remarks>
        private void OnRendering(object sender, EventArgs e)
        {
            if (_target == null)
                return;

            UpdateTargetBounds();
            UpdateClipBounds();

            double totalSeconds =
                (DateTime.UtcNow - _startTime).TotalSeconds;

            if (totalSeconds < GlowInterval)
            {
                SetGlowOffset(GlowStartOffset);
                InvalidateVisual();
                return;
            }

            double animationTime =
                (totalSeconds - GlowInterval) % GlowInterval;

            UpdateGlow(animationTime);
        }

        /// <summary>
        /// Calculates the gradient glow position for
        /// the current animation time.
        /// </summary>
        private void UpdateGlow(double animationTime)
        {
            if (_target == null)
                return;

            if (animationTime < GlowDuration)
            {
                double progress = animationTime / GlowDuration;

                // Smoothstep easing preserves the original
                // glow movement between its endpoints.
                progress =
                    progress * progress * (3 - 2 * progress);

                SetGlowOffset(
                    GlowStartOffset +
                    (GlowEndOffset - GlowStartOffset) * progress);
            }
            else
            {
                SetGlowOffset(GlowStartOffset);
            }

            InvalidateVisual();
        }

        /// <summary>
        /// Updates the glow offset and the positions
        /// of existing gradient stops.
        /// </summary>
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

        /// <summary>
        /// Initializes the gradient brushes and stroke pen
        /// when required by the current stroke thickness.
        /// </summary>
        /// <remarks>
        /// Drawing resources are reused while the configured
        /// stroke thickness remains unchanged.
        /// </remarks>
        private void EnsureRenderResources(double strokeThickness)
        {
            GetSelectionColor();

            if (_renderResourcesInitialized &&
                Math.Abs(_cachedStrokeThickness - strokeThickness) < 0.0001)
            {
                return;
            }

            _strokeGlowBrush = CreateGlowBrush(140, 255, 1.5);
            _backgroundGlowBrush = CreateGlowBrush(0, 32, 1.5);
            _strokePen = new Pen(_strokeGlowBrush, strokeThickness);

            _cachedStrokeThickness = strokeThickness;
            _renderResourcesInitialized = true;
        }

        /// <summary>
        /// Moves the gradient stops of an existing glow brush
        /// to match the current animation offset.
        /// </summary>
        private void UpdateGlowBrushOffsets(LinearGradientBrush brush)
        {
            if (brush == null)
                return;

            brush.GradientStops[0].Offset = GlowOffset - 1.5;
            brush.GradientStops[1].Offset = GlowOffset;
            brush.GradientStops[2].Offset = GlowOffset + 1.5;
        }

        /// <summary>
        /// Creates a gradient brush for the animated
        /// selector stroke or background highlight.
        /// </summary>
        private LinearGradientBrush CreateGlowBrush(
            byte sideAlpha,
            byte centerAlpha,
            double glowWidth)
        {
            Color baseColor = GetSelectionColor();

            return new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                RelativeTransform = new RotateTransform(
                    45,
                    0.5,
                    0.5),

                GradientStops = new GradientStopCollection
                {
                    new GradientStop(
                        Color.FromArgb(
                            sideAlpha,
                            baseColor.R,
                            baseColor.G,
                            baseColor.B),
                        GlowOffset - glowWidth),

                    new GradientStop(
                        Color.FromArgb(
                            centerAlpha,
                            baseColor.R,
                            baseColor.G,
                            baseColor.B),
                        GlowOffset),

                    new GradientStop(
                        Color.FromArgb(
                            sideAlpha,
                            baseColor.R,
                            baseColor.G,
                            baseColor.B),
                        GlowOffset + glowWidth)
                }
            };
        }

        /// <summary>
        /// Resolves the selector color from WPF resources
        /// and initializes the translucent background brush.
        /// </summary>
        /// <remarks>
        /// The selection color is cached after its first lookup.
        /// White is used when SelectionBrush is unavailable.
        /// </remarks>
        private Color GetSelectionColor()
        {
            if (_selectionColorInitialized)
                return _selectionColor;

            var brush =
                TryFindResource("SelectionBrush") as SolidColorBrush;

            _selectionColor = brush?.Color ?? Colors.White;

            _backgroundBrush = new SolidColorBrush(
                Color.FromArgb(
                    13,
                    _selectionColor.R,
                    _selectionColor.G,
                    _selectionColor.B));

            _backgroundBrush.Freeze();
            _selectionColorInitialized = true;

            return _selectionColor;
        }

        /// <summary>
        /// Draws the rounded selector outline
        /// using its animated gradient pen.
        /// </summary>
        private void DrawAnimatedStroke(
            DrawingContext drawingContext,
            Rect rect,
            double radius,
            double thickness)
        {
            EnsureRenderResources(thickness);

            drawingContext.DrawRoundedRectangle(
                null,
                _strokePen,
                rect,
                radius,
                radius);
        }

        /// <summary>
        /// Draws the translucent selection background
        /// and its animated gradient highlight.
        /// </summary>
        private void DrawAnimatedBackgroundHighlight(
            DrawingContext drawingContext,
            Rect rect,
            double radius)
        {
            drawingContext.DrawRoundedRectangle(
                _backgroundBrush,
                null,
                rect,
                radius,
                radius);

            drawingContext.DrawRoundedRectangle(
                _backgroundGlowBrush,
                null,
                rect,
                radius,
                radius);
        }

        /// <summary>
        /// Renders the selection background and outline
        /// around the current target bounds.
        /// </summary>
        /// <remarks>
        /// Stroke and background rectangles are calculated
        /// independently using the active SelectorProfile.
        ///
        /// The selector does not draw when its target
        /// is unavailable, hidden, or has invalid bounds.
        /// </remarks>
        protected override void OnRender(
            DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (_target == null ||
                _profile == null ||
                _targetBounds.IsEmpty ||
                !_target.IsVisible ||
                ActualWidth <= 0 ||
                ActualHeight <= 0)
            {
                return;
            }

            EnsureRenderResources(_profile.StrokeThickness);

            Rect targetRect = _targetBounds;

            Rect strokeEdgeRect = GetOffsetRect(
                targetRect,
                _profile.StrokeInset,
                _profile.StrokeOutset);

            Rect strokeRect = GetOffsetRect(
                strokeEdgeRect,
                _profile.StrokeThickness / 2.0,
                0);

            Rect backgroundRect = GetOffsetRect(
                targetRect,
                _profile.BackgroundInset,
                _profile.BackgroundOutset);

            if (backgroundRect.Width > 0 &&
                backgroundRect.Height > 0)
            {
                DrawAnimatedBackgroundHighlight(
                    drawingContext,
                    backgroundRect,
                    _profile.BackgroundRadius);
            }

            if (strokeRect.Width > 0 &&
                strokeRect.Height > 0 &&
                _profile.StrokeThickness > 0)
            {
                DrawAnimatedStroke(
                    drawingContext,
                    strokeRect,
                    _profile.StrokeRadius,
                    _profile.StrokeThickness);
            }
        }

        /// <summary>
        /// Updates the target bounds in the coordinate system
        /// of the selector's visual parent.
        /// </summary>
        /// <remarks>
        /// A tolerance of 0.01 is used when comparing bounds
        /// to avoid unnecessary redraws for insignificant changes.
        ///
        /// Invalid visual transformations clear the cached
        /// bounds so an outdated selection is not drawn.
        /// </remarks>
        private void UpdateTargetBounds()
        {
            if (_target == null)
                return;

            var parent = Parent as Visual;

            if (parent == null)
                return;

            try
            {
                Rect bounds = _target
                    .TransformToAncestor(parent)
                    .TransformBounds(
                        new Rect(
                            0,
                            0,
                            _target.ActualWidth,
                            _target.ActualHeight));

                if (AreClose(_targetBounds.X, bounds.X) &&
                    AreClose(_targetBounds.Y, bounds.Y) &&
                    AreClose(_targetBounds.Width, bounds.Width) &&
                    AreClose(_targetBounds.Height, bounds.Height))
                {
                    return;
                }

                _targetBounds = bounds;
                InvalidateVisual();
            }
            catch (InvalidOperationException)
            {
                if (!_targetBounds.IsEmpty)
                {
                    _targetBounds = Rect.Empty;
                    InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Expands or contracts a rectangle using
        /// the specified inset and outset values.
        /// </summary>
        private static Rect GetOffsetRect(
            Rect rect,
            double inset,
            double outset)
        {
            double offset = outset - inset;

            return new Rect(
                rect.X - offset,
                rect.Y - offset,
                Math.Max(0, rect.Width + offset * 2),
                Math.Max(0, rect.Height + offset * 2));
        }

        /// <summary>
        /// Compares two coordinate values using the original
        /// selector geometry tolerance.
        /// </summary>
        private static bool AreClose(
            double first,
            double second)
        {
            return Math.Abs(first - second) < 0.01;
        }
    }
}
