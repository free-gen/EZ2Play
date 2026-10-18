
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EZ2Play.App
{
    /// <summary>
    /// Manages game background images, transitions,
    /// image caching, and automatic horizontal panning.
    /// </summary>
    /// <remarks>
    /// Background images are decoded asynchronously and
    /// cached using a size-limited LRU cache.
    ///
    /// Request and lifecycle generations prevent outdated
    /// asynchronous results and animation callbacks from
    /// changing the current background.
    ///
    /// When no custom image is available, the controller
    /// displays the particle background instead.
    /// </remarks>
    public class BackgroundController : IDisposable
    {
        // UI elements used to display background transitions.
        private readonly Grid _viewport;
        private readonly Image _previousImage;
        private readonly Image _image;
        private readonly ParticlesCanvas _particles;

        // Background appearance and transition settings.
        private const double BackgroundOpacity = 0.9;
        private const double BackgroundStartPosition = 0.25;
        private const double BackgroundPanSpeed = 5;
        private const double BackgroundPanEdgeZone = 50;
        private const double BackgroundTransitionDuration = 0.5;
        private const int BackgroundPanStartDelayMs = 1000;
        private const double BackgroundPanRampDuration = 1.0;

        // Automatic horizontal panning state.
        private double _panOverflow;
        private double _panPosition;
        private double _panDirection = 1;

        private double _lastPanViewportWidth;
        private double _lastPanViewportHeight;
        private BitmapSource _lastPanSource;

        private TimeSpan _panLastRenderTime;
        private double _panRampElapsed;

        private readonly DispatcherTimer _panStartTimer;

        // Request and lifecycle state.
        private int _backgroundRequestId;
        private int _lifecycleGeneration;
        private int _backgroundTransitionGeneration;

        private bool _backgroundTransitionInProgress;
        private bool _disposed;

        // Background image cache.
        private readonly Dictionary<string, BitmapImage> _backgroundCache =
            new Dictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);

        private readonly LinkedList<string> _backgroundCacheLru =
            new LinkedList<string>();

        private const long BackgroundCacheBudgetBytes = 64L * 1024 * 1024;

        private readonly object _backgroundCacheLock = new object();

        private int _backgroundCacheGeneration;
        private long _backgroundCacheBytes;

        // Asynchronous background request queue.
        private Task _backgroundWorkerTask;

        private string _pendingShortcutPath;
        private int _pendingDirection;
        private int _pendingDecodePixelHeight;
        private int _pendingRequestId;
        private int _pendingLifecycleGeneration;

        private bool _isActive = true;

        // The first transform in each TransformGroup handles
        // background panning, while the second handles slide transitions.
        private TranslateTransform ImageTranslate =>
            (_image?.RenderTransform as TransformGroup)?.Children[0]
                as TranslateTransform;

        private TranslateTransform PreviousTranslate =>
            (_previousImage?.RenderTransform as TransformGroup)?.Children[0]
                as TranslateTransform;

        private TranslateTransform ImageSlide =>
            (_image?.RenderTransform as TransformGroup)?.Children[1]
                as TranslateTransform;

        private TranslateTransform PreviousSlide =>
            (_previousImage?.RenderTransform as TransformGroup)?.Children[1]
                as TranslateTransform;

        private bool UseImageBackground => _image?.Source != null;

        /// <summary>
        /// Determines the image decoding height from the
        /// actual viewport height and display DPI.
        /// </summary>
        private int GetDecodePixelHeight()
        {
            if (_viewport == null || _viewport.ActualHeight <= 0)
                return 2160;

            double scale = VisualTreeHelper.GetDpi(_viewport).DpiScaleY;

            return Math.Max(
                1,
                (int)Math.Ceiling(_viewport.ActualHeight * scale));
        }

        public BackgroundController(
            Grid viewport,
            Image previousImage,
            Image image,
            ParticlesCanvas particles)
        {
            _viewport = viewport;
            _previousImage = previousImage;
            _image = image;
            _particles = particles;

            _panStartTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(BackgroundPanStartDelayMs)
            };

            _panStartTimer.Tick += PanStartTimer_Tick;
        }

        /// <summary>
        /// Loads a background synchronously and initializes
        /// its display and panning state.
        /// </summary>
        /// <remarks>
        /// Resets pending requests and transitions before
        /// loading the image.
        /// </remarks>
        public bool Load(string shortcutPath)
        {
            if (_image == null || _disposed)
                return false;

            ++_backgroundRequestId;
            ++_lifecycleGeneration;
            ++_backgroundTransitionGeneration;

            _backgroundTransitionInProgress = false;

            _pendingShortcutPath = null;
            _pendingRequestId = 0;
            _pendingLifecycleGeneration = 0;

            StopPan();

            ResetSlides();

            _image.BeginAnimation(UIElement.OpacityProperty, null);
            _image.Source = null;
            _image.Visibility = Visibility.Collapsed;
            _image.Opacity = 0;

            _lastPanSource = null;

            ClearPrevious();

            var bitmap = LoadBitmap(
                shortcutPath,
                GetDecodePixelHeight());

            if (bitmap == null)
                return false;

            _image.Source = bitmap;

            RenderOptions.SetBitmapScalingMode(
                _image,
                BitmapScalingMode.HighQuality);

            RefreshPan();

            return true;
        }

        /// <summary>
        /// Removes cached background images associated
        /// with the specified shortcut.
        /// </summary>
        /// <remarks>
        /// The cache generation is incremented so that
        /// an image currently being decoded cannot reinsert
        /// an invalidated cache entry.
        /// </remarks>
        public void InvalidateBackgroundCache(string shortcutPath)
        {
            if (string.IsNullOrWhiteSpace(shortcutPath))
                return;

            string backgroundPath =
                IconExtractor.GetCustomBackgroundPath(shortcutPath);

            if (string.IsNullOrWhiteSpace(backgroundPath))
                return;

            string cachePath = Path.GetFullPath(backgroundPath);
            var keysToRemove = new List<string>();

            lock (_backgroundCacheLock)
            {
                ++_backgroundCacheGeneration;

                foreach (var key in _backgroundCache.Keys)
                {
                    if (key.StartsWith(
                        cachePath + "|",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        keysToRemove.Add(key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    var bitmap = _backgroundCache[key];

                    _backgroundCache.Remove(key);
                    _backgroundCacheLru.Remove(key);

                    _backgroundCacheBytes -=
                        (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
                }
            }
        }

        /// <summary>
        /// Loads a background bitmap from disk or the image cache.
        /// </summary>
        /// <remarks>
        /// Cache keys include the file path, file length,
        /// modification timestamp, and decoding height.
        ///
        /// Images are fully decoded before closing the file
        /// stream and frozen for use across threads.
        ///
        /// Cache entries are evicted using least-recently-used
        /// ordering when the memory budget would be exceeded.
        /// </remarks>
        private BitmapImage LoadBitmap(
            string shortcutPath,
            int decodePixelHeight)
        {
            try
            {
                string backgroundPath =
                    IconExtractor.GetCustomBackgroundPath(shortcutPath);

                if (string.IsNullOrWhiteSpace(backgroundPath) ||
                    !File.Exists(backgroundPath))
                {
                    return null;
                }

                string cachePath = Path.GetFullPath(backgroundPath);
                var fileInfo = new FileInfo(cachePath);

                string cacheKey =
                    cachePath + "|" +
                    fileInfo.Length + "|" +
                    fileInfo.LastWriteTimeUtc.Ticks + "|" +
                    decodePixelHeight;

                int cacheGeneration;

                lock (_backgroundCacheLock)
                {
                    cacheGeneration = _backgroundCacheGeneration;

                    if (_backgroundCache.TryGetValue(
                        cacheKey,
                        out var cachedBitmap))
                    {
                        // Move the accessed entry to the end
                        // of the least-recently-used list.
                        _backgroundCacheLru.Remove(cacheKey);
                        _backgroundCacheLru.AddLast(cacheKey);

                        return cachedBitmap;
                    }
                }

                var bitmap = new BitmapImage();

                using (var stream = new FileStream(
                    backgroundPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelHeight = decodePixelHeight;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }

                bitmap.Freeze();

                // Estimate decoded bitmap memory usage using
                // four bytes per pixel.
                long bitmapBytes =
                    (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;

                if (bitmapBytes <= BackgroundCacheBudgetBytes)
                {
                    lock (_backgroundCacheLock)
                    {
                        if (_disposed ||
                            cacheGeneration != _backgroundCacheGeneration)
                        {
                            return bitmap;
                        }

                        // Another request may have cached
                        // the same image while decoding.
                        if (_backgroundCache.TryGetValue(
                            cacheKey,
                            out var existing))
                        {
                            _backgroundCacheLru.Remove(cacheKey);
                            _backgroundCacheLru.AddLast(cacheKey);

                            return existing;
                        }

                        // Remove the oldest cached images until
                        // the new image fits within the budget.
                        while (_backgroundCacheLru.Count > 0 &&
                            _backgroundCacheBytes + bitmapBytes >
                            BackgroundCacheBudgetBytes)
                        {
                            string oldestKey =
                                _backgroundCacheLru.First.Value;

                            _backgroundCacheLru.RemoveFirst();

                            var oldestBitmap =
                                _backgroundCache[oldestKey];

                            _backgroundCache.Remove(oldestKey);

                            _backgroundCacheBytes -=
                                (long)oldestBitmap.PixelWidth *
                                oldestBitmap.PixelHeight * 4;
                        }

                        _backgroundCache[cacheKey] = bitmap;
                        _backgroundCacheLru.AddLast(cacheKey);
                        _backgroundCacheBytes += bitmapBytes;
                    }
                }

                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Requests a transition to the background associated
        /// with the specified game shortcut.
        /// </summary>
        /// <remarks>
        /// The latest pending request replaces earlier pending
        /// requests that have not yet been processed.
        /// </remarks>
        public void TransitionTo(
            string shortcutPath,
            int direction = 0)
        {
            if (_image == null || _disposed || !_isActive)
                return;

            int requestId = ++_backgroundRequestId;

            int lifecycleGeneration = _lifecycleGeneration;

            _pendingShortcutPath = shortcutPath;
            _pendingDirection = direction;
            _pendingDecodePixelHeight = GetDecodePixelHeight();
            _pendingRequestId = requestId;
            _pendingLifecycleGeneration = lifecycleGeneration;

            StartPendingBackgroundQueue();
        }

        /// <summary>
        /// Starts the background loading worker when there
        /// is a pending request and no active transition.
        /// </summary>
        private void StartPendingBackgroundQueue()
        {
            if (_disposed ||
                !_isActive ||
                _backgroundTransitionInProgress ||
                _pendingShortcutPath == null)
            {
                return;
            }

            if (_backgroundWorkerTask == null ||
                _backgroundWorkerTask.IsCompleted)
            {
                _backgroundWorkerTask = ProcessBackgroundQueueAsync();
            }
        }

        /// <summary>
        /// Processes pending background requests asynchronously.
        /// </summary>
        /// <remarks>
        /// Image decoding runs on a worker thread.
        ///
        /// After decoding, request and lifecycle identifiers
        /// are checked to discard outdated results.
        ///
        /// Depending on the current and next backgrounds,
        /// the controller selects a crossfade or a transition
        /// between images and particles.
        /// </remarks>
        private async Task ProcessBackgroundQueueAsync()
        {
            while (!_disposed)
            {
                if (_backgroundTransitionInProgress)
                    return;

                string shortcutPath = _pendingShortcutPath;
                int direction = _pendingDirection;
                int decodePixelHeight = _pendingDecodePixelHeight;
                int requestId = _pendingRequestId;
                int lifecycleGeneration = _pendingLifecycleGeneration;

                _pendingShortcutPath = null;

                if (shortcutPath == null)
                    return;

                var nextBitmap = await Task.Run(
                    () => LoadBitmap(shortcutPath, decodePixelHeight));

                // Ignore results belonging to an outdated request
                // or a previous background lifecycle.
                if (_disposed ||
                    !_isActive ||
                    lifecycleGeneration != _lifecycleGeneration ||
                    requestId != _backgroundRequestId)
                {
                    continue;
                }

                // A newer background request takes priority.
                if (_pendingShortcutPath != null)
                {
                    continue;
                }

                bool hasCurrentBackground = _image.Source != null;
                bool hasNextBackground = nextBitmap != null;

                if (hasCurrentBackground && hasNextBackground)
                {
                    Crossfade(nextBitmap, direction);
                    continue;
                }

                if (hasCurrentBackground)
                {
                    FadeToParticles();
                    continue;
                }

                if (hasNextBackground)
                {
                    FadeFromParticles(nextBitmap);
                    continue;
                }

                // Neither the current nor the requested game
                // has an image background.
                ++_backgroundTransitionGeneration;
                ClearPrevious();

                _particles?.SetParticlesVisible(
                    true,
                    true,
                    BackgroundTransitionDuration);
            }
        }

        /// <summary>
        /// Stops active horizontal slide animations and
        /// restores both slide transforms to zero.
        /// </summary>
        private void ResetSlides()
        {
            if (ImageSlide != null)
            {
                ImageSlide.BeginAnimation(
                    TranslateTransform.XProperty,
                    null);

                ImageSlide.X = 0;
            }

            if (PreviousSlide != null)
            {
                PreviousSlide.BeginAnimation(
                    TranslateTransform.XProperty,
                    null);

                PreviousSlide.X = 0;
            }
        }

        /// <summary>
        /// Crossfades between the current and next background
        /// while applying a directional slide.
        /// </summary>
        /// <remarks>
        /// The previous image is displayed on a separate
        /// Image element during the transition.
        ///
        /// Animation callbacks verify their generation before
        /// changing the current background state.
        /// </remarks>
        private void Crossfade(BitmapImage nextBitmap, int direction)
        {
            if (_previousImage == null)
            {
                LoadFromBitmap(nextBitmap);
                Show(true);
                return;
            }

            int transitionGeneration = ++_backgroundTransitionGeneration;
            _backgroundTransitionInProgress = true;

            ResetSlides();

            double previousOpacity = _image.Opacity;
            double previousX = ImageTranslate?.X ?? 0;

            _image.BeginAnimation(UIElement.OpacityProperty, null);

            _previousImage.BeginAnimation(UIElement.OpacityProperty, null);
            _previousImage.Source = _image.Source;
            _previousImage.Width = _image.Width;
            _previousImage.Height = _image.Height;
            _previousImage.Visibility = Visibility.Visible;

            _previousImage.Opacity = previousOpacity > 0
                ? previousOpacity
                : BackgroundOpacity;

            if (PreviousTranslate != null)
                PreviousTranslate.X = previousX;

            StopPan();

            _image.Source = nextBitmap;
            _image.Visibility = Visibility.Visible;
            _image.Opacity = 0;

            RenderOptions.SetBitmapScalingMode(
                _image,
                BitmapScalingMode.HighQuality);

            RefreshPan();

            _particles?.SetParticlesVisible(
                false,
                true,
                BackgroundTransitionDuration);

            // Fade out the previous background.
            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(
                    BackgroundTransitionDuration),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseInOut
                }
            };

            // Fade in the newly loaded background.
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = BackgroundOpacity,
                Duration = TimeSpan.FromSeconds(
                    BackgroundTransitionDuration),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseInOut
                }
            };

            // Calculate the horizontal slide from the
            // dynamically scaled WPF resource.
            double slide =
                (double)_viewport.FindResource(
                    UiScaleKeys.BackgroundTransitionSlide)
                * Math.Sign(direction);

            var previousSlide = new DoubleAnimation
            {
                From = 0,
                To = -slide,
                Duration = TimeSpan.FromSeconds(
                    BackgroundTransitionDuration),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseInOut
                }
            };

            var nextSlide = new DoubleAnimation
            {
                From = slide,
                To = 0,
                Duration = TimeSpan.FromSeconds(
                    BackgroundTransitionDuration),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            fadeOut.Completed += (s, e) =>
            {
                if (_disposed ||
                    transitionGeneration != _backgroundTransitionGeneration)
                {
                    return;
                }

                ClearPrevious();
            };

            fadeIn.Completed += (s, e) =>
            {
                if (_disposed ||
                    transitionGeneration != _backgroundTransitionGeneration)
                {
                    return;
                }

                if (ImageSlide != null)
                {
                    ImageSlide.BeginAnimation(
                        TranslateTransform.XProperty,
                        null);

                    ImageSlide.X = 0;
                }

                ClearPrevious();
                _backgroundTransitionInProgress = false;

                StartPendingBackgroundQueue();
            };

            // Preserve the original animation start order.
            PreviousSlide.BeginAnimation(
                TranslateTransform.XProperty,
                previousSlide);

            ImageSlide.BeginAnimation(
                TranslateTransform.XProperty,
                nextSlide);

            _previousImage.BeginAnimation(
                UIElement.OpacityProperty,
                fadeOut);

            _image.BeginAnimation(
                UIElement.OpacityProperty,
                fadeIn);
        }

        /// <summary>
        /// Fades out the current image and reveals
        /// the particle background.
        /// </summary>
        private void FadeToParticles()
        {
            int transitionGeneration = ++_backgroundTransitionGeneration;
            _backgroundTransitionInProgress = true;

            ClearPrevious();

            ResetSlides();

            double currentOpacity = _image.Opacity;

            _image.BeginAnimation(UIElement.OpacityProperty, null);

            _image.Opacity = currentOpacity > 0
                ? currentOpacity
                : BackgroundOpacity;

            _particles?.SetParticlesVisible(
                true,
                true,
                BackgroundTransitionDuration);

            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(
                    BackgroundTransitionDuration),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseInOut
                }
            };

            fadeOut.Completed += (s, e) =>
            {
                if (_disposed ||
                    transitionGeneration != _backgroundTransitionGeneration)
                {
                    return;
                }

                StopPan();

                _image.BeginAnimation(UIElement.OpacityProperty, null);
                _image.Source = null;
                _image.Visibility = Visibility.Collapsed;
                _image.Opacity = 0;

                _backgroundTransitionInProgress = false;

                StartPendingBackgroundQueue();
            };

            _image.BeginAnimation(
                UIElement.OpacityProperty,
                fadeOut);
        }

        /// <summary>
        /// Fades in an image background while hiding
        /// the particle background.
        /// </summary>
        private void FadeFromParticles(BitmapImage nextBitmap)
        {
            int transitionGeneration = ++_backgroundTransitionGeneration;
            _backgroundTransitionInProgress = true;

            ResetSlides();

            StopPan();
            ClearPrevious();

            _image.BeginAnimation(UIElement.OpacityProperty, null);
            _image.Source = nextBitmap;
            _image.Visibility = Visibility.Visible;
            _image.Opacity = 0;

            RenderOptions.SetBitmapScalingMode(
                _image,
                BitmapScalingMode.HighQuality);

            RefreshPan();

            _particles?.SetParticlesVisible(
                false,
                true,
                BackgroundTransitionDuration);

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = BackgroundOpacity,
                Duration = TimeSpan.FromSeconds(
                    BackgroundTransitionDuration),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseInOut
                }
            };

            fadeIn.Completed += (s, e) =>
            {
                if (_disposed ||
                    transitionGeneration != _backgroundTransitionGeneration)
                {
                    return;
                }

                _backgroundTransitionInProgress = false;

                StartPendingBackgroundQueue();
            };

            _image.BeginAnimation(
                UIElement.OpacityProperty,
                fadeIn);
        }

        /// <summary>
        /// Replaces the current background bitmap without
        /// starting a crossfade transition.
        /// </summary>
        private void LoadFromBitmap(BitmapImage bitmap)
        {
            ++_backgroundTransitionGeneration;

            ResetSlides();

            StopPan();

            _image.BeginAnimation(UIElement.OpacityProperty, null);
            _image.Source = bitmap;
            _image.Visibility = Visibility.Collapsed;
            _image.Opacity = 0;

            RenderOptions.SetBitmapScalingMode(
                _image,
                BitmapScalingMode.HighQuality);

            RefreshPan();
        }

        /// <summary>
        /// Clears the image element used to display
        /// the previous background during transitions.
        /// </summary>
        private void ClearPrevious()
        {
            if (_previousImage == null)
                return;

            _previousImage.BeginAnimation(UIElement.OpacityProperty, null);

            if (PreviousSlide != null)
            {
                PreviousSlide.BeginAnimation(
                    TranslateTransform.XProperty,
                    null);

                PreviousSlide.X = 0;
            }

            _previousImage.Source = null;
            _previousImage.Visibility = Visibility.Collapsed;
            _previousImage.Opacity = 0;

            if (PreviousTranslate != null)
                PreviousTranslate.X = 0;
        }

        /// <summary>
        /// Recalculates the image dimensions and initializes
        /// automatic horizontal panning when the image exceeds
        /// the viewport width.
        /// </summary>
        /// <remarks>
        /// Recalculation is skipped when the image reference
        /// and viewport dimensions have not meaningfully changed.
        /// </remarks>
        public void RefreshPan()
        {
            if (!(_image?.Source is BitmapSource source) ||
                _viewport == null ||
                ImageTranslate == null)
            {
                return;
            }

            double viewportWidth = _viewport.ActualWidth;
            double viewportHeight = _viewport.ActualHeight;

            if (viewportWidth <= 0 ||
                viewportHeight <= 0 ||
                source.PixelWidth <= 0 ||
                source.PixelHeight <= 0)
            {
                return;
            }

            if (ReferenceEquals(source, _lastPanSource) &&
                Math.Abs(viewportWidth - _lastPanViewportWidth) < 0.1 &&
                Math.Abs(viewportHeight - _lastPanViewportHeight) < 0.1)
            {
                return;
            }

            StopPan();

            _lastPanSource = source;
            _lastPanViewportWidth = viewportWidth;
            _lastPanViewportHeight = viewportHeight;

            // Fit the image to the viewport height while
            // preserving its original aspect ratio.
            double aspect =
                (double)source.PixelWidth / source.PixelHeight;

            double renderedHeight = viewportHeight;
            double renderedWidth = renderedHeight * aspect;

            _image.Width = renderedWidth;
            _image.Height = renderedHeight;

            _panOverflow = Math.Max(
                0,
                renderedWidth - viewportWidth);

            if (_panOverflow <= 0)
            {
                ImageTranslate.X = 0;
                return;
            }

            // Begin at 25% of the horizontal overflow.
            _panPosition = _panOverflow * BackgroundStartPosition;
            _panDirection = 1;
            _panLastRenderTime = TimeSpan.Zero;

            ImageTranslate.X = -_panPosition;

            // Delay the start of continuous panning.
            _panStartTimer.Stop();
            _panStartTimer.Start();
        }

        /// <summary>
        /// Starts per-frame panning after the initial delay.
        /// </summary>
        private void PanStartTimer_Tick(object sender, EventArgs e)
        {
            _panStartTimer.Stop();

            if (!_isActive ||
                _panOverflow <= 0 ||
                ImageTranslate == null)
            {
                return;
            }

            _panLastRenderTime = TimeSpan.Zero;
            _panRampElapsed = 0;

            CompositionTarget.Rendering -= Pan_Rendering;
            CompositionTarget.Rendering += Pan_Rendering;
        }

        /// <summary>
        /// Stops automatic background panning and resets
        /// its timing, position, and image transform state.
        /// </summary>
        private void StopPan()
        {
            _panStartTimer.Stop();
            CompositionTarget.Rendering -= Pan_Rendering;

            _panOverflow = 0;
            _lastPanSource = null;
            _panLastRenderTime = TimeSpan.Zero;
            _panRampElapsed = 0;

            if (ImageTranslate != null)
                ImageTranslate.X = 0;
        }

        /// <summary>
        /// Advances background panning during WPF rendering.
        /// </summary>
        /// <remarks>
        /// Movement uses frame delta time, gradual acceleration,
        /// and reduced speed near the image edges.
        ///
        /// The direction reverses whenever the image reaches
        /// either end of its available horizontal range.
        /// </remarks>
        private void Pan_Rendering(object sender, EventArgs e)
        {
            if (!_isActive ||
                _panOverflow <= 0 ||
                ImageTranslate == null)
            {
                return;
            }

            if (!(e is RenderingEventArgs renderingArgs))
                return;

            TimeSpan renderTime = renderingArgs.RenderingTime;

            if (_panLastRenderTime == TimeSpan.Zero)
            {
                _panLastRenderTime = renderTime;
                return;
            }

            double delta =
                (renderTime - _panLastRenderTime).TotalSeconds;

            _panLastRenderTime = renderTime;

            // Ignore invalid or unusually large frame intervals.
            if (delta <= 0 || delta > 0.1)
                return;

            double distanceToEdge = _panDirection > 0
                ? _panOverflow - _panPosition
                : _panPosition;

            // Reduce movement speed near image boundaries.
            double edgeFactor = Math.Min(
                1.0,
                Math.Max(
                    0.08,
                    distanceToEdge / BackgroundPanEdgeZone));

            // Gradually accelerate the image movement.
            _panRampElapsed = Math.Min(
                BackgroundPanRampDuration,
                _panRampElapsed + delta);

            double rampProgress =
                _panRampElapsed / BackgroundPanRampDuration;

            double rampFactor =
                rampProgress * rampProgress *
                (3.0 - 2.0 * rampProgress);

            _panPosition +=
                BackgroundPanSpeed *
                edgeFactor *
                rampFactor *
                _panDirection *
                delta;

            // Reverse direction at the horizontal boundaries.
            if (_panPosition >= _panOverflow)
            {
                _panPosition = _panOverflow;
                _panDirection = -1;
            }
            else if (_panPosition <= 0)
            {
                _panPosition = 0;
                _panDirection = 1;
            }

            ImageTranslate.X = -_panPosition;
        }

        /// <summary>
        /// Shows or hides the background controller's
        /// image and particle presentation.
        /// </summary>
        /// <remarks>
        /// Hiding the background invalidates pending lifecycle
        /// operations and stops rendering-driven panning.
        ///
        /// Showing the background resumes its visual state
        /// using the original visibility and animation rules.
        /// </remarks>
        public void Show(bool visible)
        {
            _isActive = visible;

            if (!visible)
            {
                ++_lifecycleGeneration;
                ++_backgroundTransitionGeneration;

                _backgroundTransitionInProgress = false;

                _pendingShortcutPath = null;
                _pendingRequestId = 0;
                _pendingLifecycleGeneration = 0;

                _panStartTimer.Stop();

                ClearPrevious();
            }

            if (visible && _backgroundTransitionInProgress)
                return;

            if (UseImageBackground)
            {
                if (visible)
                {
                    _particles?.SetParticlesVisible(
                        false,
                        true,
                        0.2);
                }

                var animation = new DoubleAnimation
                {
                    To = visible ? BackgroundOpacity : 0,
                    Duration = TimeSpan.FromSeconds(0.2),
                    EasingFunction = new CubicEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                };

                animation.Completed += (s, e) =>
                {
                    if (!visible && !_isActive)
                        _image.Visibility = Visibility.Collapsed;
                };

                _image.Visibility = Visibility.Visible;

                _image.BeginAnimation(
                    UIElement.OpacityProperty,
                    animation);
            }

            if (!visible)
            {
                CompositionTarget.Rendering -= Pan_Rendering;
            }
            else if (_panOverflow > 0 && ImageTranslate != null)
            {
                _panLastRenderTime = TimeSpan.Zero;
                _panRampElapsed = 0;

                CompositionTarget.Rendering -= Pan_Rendering;
                CompositionTarget.Rendering += Pan_Rendering;
            }
            else if (!UseImageBackground)
            {
                if (_image != null)
                {
                    _image.BeginAnimation(
                        UIElement.OpacityProperty,
                        null);

                    _image.Visibility = Visibility.Collapsed;
                    _image.Opacity = 0;
                }

                _particles?.SetParticlesVisible(
                    visible,
                    true,
                    0.2);
            }
        }

        /// <summary>
        /// Stops background processing and releases
        /// rendering subscriptions and cached images.
        /// </summary>
        /// <remarks>
        /// Repeated calls have no effect after disposal.
        /// Generation counters invalidate outstanding work
        /// without changing the original asynchronous workflow.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _backgroundTransitionInProgress = false;

            ++_backgroundRequestId;
            ++_lifecycleGeneration;
            ++_backgroundTransitionGeneration;

            _pendingShortcutPath = null;
            _pendingRequestId = 0;
            _pendingLifecycleGeneration = 0;

            _backgroundWorkerTask = null;

            StopPan();

            _panStartTimer.Tick -= PanStartTimer_Tick;

            lock (_backgroundCacheLock)
            {
                ++_backgroundCacheGeneration;

                _backgroundCache.Clear();
                _backgroundCacheLru.Clear();
                _backgroundCacheBytes = 0;
            }
        }
    }
}
