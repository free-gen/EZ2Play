using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace EZ2Play.App
{
    public class BackgroundController : IDisposable
    {
        private readonly Grid _viewport;
        private readonly Image _previousImage;
        private readonly Image _image;
        private readonly ParticlesCanvas _particles;

        private const double BackgroundOpacity = 0.9;
        private const double BackgroundStartPosition = 0.25;
        private const double BackgroundPanSpeed = 5;
        private const double BackgroundPanEdgeZone = 50;
        private const double BackgroundTransitionDuration = 0.5;
        private const int BackgroundPanStartDelayMs = 1000;
        private const double BackgroundPanRampDuration = 1.0;

        private double _panOverflow;
        private double _panPosition;
        private double _panDirection = 1;
        private double _lastPanViewportWidth;
        private double _lastPanViewportHeight;
        private BitmapSource _lastPanSource;
        private TimeSpan _panLastRenderTime;
        private double _panRampElapsed;
        private readonly DispatcherTimer _panStartTimer;

        private int _backgroundRequestId;
        private int _lifecycleGeneration;
        private int _backgroundTransitionGeneration;
        private bool _backgroundTransitionInProgress;
        private bool _disposed;

        private readonly Dictionary<string, BitmapImage> _backgroundCache =
            new Dictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);

        private readonly LinkedList<string> _backgroundCacheLru =
            new LinkedList<string>();

        private const long BackgroundCacheBudgetBytes = 64L * 1024 * 1024;
        private readonly object _backgroundCacheLock = new object();
        private int _backgroundCacheGeneration;
        private long _backgroundCacheBytes;
        private Task _backgroundWorkerTask;
        private string _pendingShortcutPath;
        private int _pendingDirection;
        private int _pendingDecodePixelHeight;
        private int _pendingRequestId;
        private int _pendingLifecycleGeneration;
        private bool _isActive = true;

        private TranslateTransform ImageTranslate =>
            (_image?.RenderTransform as TransformGroup)?.Children[0] as TranslateTransform;
        private TranslateTransform PreviousTranslate =>
            (_previousImage?.RenderTransform as TransformGroup)?.Children[0] as TranslateTransform;
        private TranslateTransform ImageSlide =>
            (_image?.RenderTransform as TransformGroup)?.Children[1] as TranslateTransform;
        private TranslateTransform PreviousSlide =>
            (_previousImage?.RenderTransform as TransformGroup)?.Children[1] as TranslateTransform;

        private bool UseImageBackground => _image?.Source != null;

        private int GetDecodePixelHeight()
        {
            if (_viewport == null || _viewport.ActualHeight <= 0)
                return 2160;

            double scale = VisualTreeHelper.GetDpi(_viewport).DpiScaleY;
            return Math.Max(1, (int)Math.Ceiling(_viewport.ActualHeight * scale));
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

            var bitmap = LoadBitmap(shortcutPath, GetDecodePixelHeight());

            if (bitmap == null)
                return false;

            _image.Source = bitmap;

            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);

            RefreshPan();

            return true;
        }

        public void InvalidateBackgroundCache(string shortcutPath)
        {
            if (string.IsNullOrWhiteSpace(shortcutPath))
                return;

            string backgroundPath = IconExtractor.GetCustomBackgroundPath(shortcutPath);

            if (string.IsNullOrWhiteSpace(backgroundPath))
                return;

            string cachePath = Path.GetFullPath(backgroundPath);
            var keysToRemove = new List<string>();

            lock (_backgroundCacheLock)
            {
                ++_backgroundCacheGeneration;

                foreach (var key in _backgroundCache.Keys)
                {
                    if (key.StartsWith(cachePath + "|", StringComparison.OrdinalIgnoreCase))
                        keysToRemove.Add(key);
                }

                foreach (var key in keysToRemove)
                {
                    var bitmap = _backgroundCache[key];
                    _backgroundCache.Remove(key);
                    _backgroundCacheLru.Remove(key);
                    _backgroundCacheBytes -= (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
                }
            }
        }

        private BitmapImage LoadBitmap(string shortcutPath, int decodePixelHeight)
        {
            try
            {
                string backgroundPath = IconExtractor.GetCustomBackgroundPath(shortcutPath);

                if (string.IsNullOrWhiteSpace(backgroundPath) || !File.Exists(backgroundPath))
                    return null;

                string cachePath = Path.GetFullPath(backgroundPath);
                var fileInfo = new FileInfo(cachePath);
                string cacheKey = cachePath + "|" + fileInfo.Length + "|" +
                    fileInfo.LastWriteTimeUtc.Ticks + "|" + decodePixelHeight;

                int cacheGeneration;

                lock (_backgroundCacheLock)
                {
                    cacheGeneration = _backgroundCacheGeneration;

                    if (_backgroundCache.TryGetValue(cacheKey, out var cachedBitmap))
                    {
                        _backgroundCacheLru.Remove(cacheKey);
                        _backgroundCacheLru.AddLast(cacheKey);
                        return cachedBitmap;
                    }
                }

                var bitmap = new BitmapImage();

                using (var stream = new FileStream(backgroundPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelHeight = decodePixelHeight;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }

                bitmap.Freeze();

                long bitmapBytes = (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;

                if (bitmapBytes <= BackgroundCacheBudgetBytes)
                {
                    lock (_backgroundCacheLock)
                    {
                        if (_disposed || cacheGeneration != _backgroundCacheGeneration)
                            return bitmap;

                        if (_backgroundCache.TryGetValue(cacheKey, out var existing))
                        {
                            _backgroundCacheLru.Remove(cacheKey);
                            _backgroundCacheLru.AddLast(cacheKey);
                            return existing;
                        }

                        while (_backgroundCacheLru.Count > 0 &&
                            _backgroundCacheBytes + bitmapBytes > BackgroundCacheBudgetBytes)
                        {
                            string oldestKey = _backgroundCacheLru.First.Value;
                            _backgroundCacheLru.RemoveFirst();

                            var oldestBitmap = _backgroundCache[oldestKey];
                            _backgroundCache.Remove(oldestKey);
                            _backgroundCacheBytes -= (long)oldestBitmap.PixelWidth * oldestBitmap.PixelHeight * 4;
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

        public void TransitionTo(string shortcutPath, int direction = 0)
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

        private void StartPendingBackgroundQueue()
        {
            if (_disposed || !_isActive || _backgroundTransitionInProgress ||
                _pendingShortcutPath == null)
                return;

            if (_backgroundWorkerTask == null || _backgroundWorkerTask.IsCompleted)
                _backgroundWorkerTask = ProcessBackgroundQueueAsync();
        }

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

                var nextBitmap = await Task.Run(() => LoadBitmap(shortcutPath, decodePixelHeight));

                if (_disposed || !_isActive ||
                    lifecycleGeneration != _lifecycleGeneration ||
                    requestId != _backgroundRequestId)
                {
                    continue;
                }

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

                ++_backgroundTransitionGeneration;
                ClearPrevious();
                _particles?.SetParticlesVisible(true, true, BackgroundTransitionDuration);
            }
        }

        private void ResetSlides()
        {
            if (ImageSlide != null)
            {
                ImageSlide.BeginAnimation(TranslateTransform.XProperty, null);
                ImageSlide.X = 0;
            }

            if (PreviousSlide != null)
            {
                PreviousSlide.BeginAnimation(TranslateTransform.XProperty, null);
                PreviousSlide.X = 0;
            }
        }

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
            _previousImage.Opacity = previousOpacity > 0 ? previousOpacity : BackgroundOpacity;

            if (PreviousTranslate != null)
                PreviousTranslate.X = previousX;

            StopPan();

            _image.Source = nextBitmap;
            _image.Visibility = Visibility.Visible;
            _image.Opacity = 0;

            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);

            RefreshPan();

            _particles?.SetParticlesVisible(false, true, BackgroundTransitionDuration);

            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(BackgroundTransitionDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = BackgroundOpacity,
                Duration = TimeSpan.FromSeconds(BackgroundTransitionDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            double slide =
                (double)_viewport.FindResource(UiScaleKeys.BackgroundTransitionSlide)
                * Math.Sign(direction);

            var previousSlide = new DoubleAnimation
            {
                From = 0,
                To = -slide,
                Duration = TimeSpan.FromSeconds(BackgroundTransitionDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var nextSlide = new DoubleAnimation
            {
                From = slide,
                To = 0,
                Duration = TimeSpan.FromSeconds(BackgroundTransitionDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            fadeOut.Completed += (s, e) =>
            {
                if (_disposed || transitionGeneration != _backgroundTransitionGeneration)
                    return;

                ClearPrevious();
            };

            fadeIn.Completed += (s, e) =>
            {
                if (_disposed || transitionGeneration != _backgroundTransitionGeneration)
                    return;

                if (ImageSlide != null)
                {
                    ImageSlide.BeginAnimation(TranslateTransform.XProperty, null);
                    ImageSlide.X = 0;
                }

                ClearPrevious();
                _backgroundTransitionInProgress = false;

                StartPendingBackgroundQueue();
            };

            PreviousSlide.BeginAnimation(TranslateTransform.XProperty, previousSlide);
            ImageSlide.BeginAnimation(TranslateTransform.XProperty, nextSlide);

            _previousImage.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            _image.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void FadeToParticles()
        {
            int transitionGeneration = ++_backgroundTransitionGeneration;
            _backgroundTransitionInProgress = true;

            ClearPrevious();

            ResetSlides();
            
            double currentOpacity = _image.Opacity;

            _image.BeginAnimation(UIElement.OpacityProperty, null);
            _image.Opacity = currentOpacity > 0 ? currentOpacity : BackgroundOpacity;

            _particles?.SetParticlesVisible(true, true, BackgroundTransitionDuration);

            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(BackgroundTransitionDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            fadeOut.Completed += (s, e) =>
            {
                if (_disposed || transitionGeneration != _backgroundTransitionGeneration)
                    return;

                StopPan();
                
                _image.BeginAnimation(UIElement.OpacityProperty, null);
                _image.Source = null;
                _image.Visibility = Visibility.Collapsed;
                _image.Opacity = 0;

                _backgroundTransitionInProgress = false;

                StartPendingBackgroundQueue();
            };

            _image.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

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

            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);

            RefreshPan();

            _particles?.SetParticlesVisible(false, true, BackgroundTransitionDuration);

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = BackgroundOpacity,
                Duration = TimeSpan.FromSeconds(BackgroundTransitionDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            fadeIn.Completed += (s, e) =>
            {
                if (_disposed ||
                    transitionGeneration != _backgroundTransitionGeneration)
                    return;

                _backgroundTransitionInProgress = false;
                StartPendingBackgroundQueue();
            };

            _image.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void LoadFromBitmap(BitmapImage bitmap)
        {
            ++_backgroundTransitionGeneration;

            ResetSlides();

            StopPan();

            _image.BeginAnimation(UIElement.OpacityProperty, null);
            _image.Source = bitmap;
            _image.Visibility = Visibility.Collapsed;
            _image.Opacity = 0;

            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);

            RefreshPan();
        }

        private void ClearPrevious()
        {
            if (_previousImage == null) return;

            _previousImage.BeginAnimation(UIElement.OpacityProperty, null);

            if (PreviousSlide != null)
            {
                PreviousSlide.BeginAnimation(TranslateTransform.XProperty, null);
                PreviousSlide.X = 0;
            }

            _previousImage.Source = null;
            _previousImage.Visibility = Visibility.Collapsed;
            _previousImage.Opacity = 0;

            if (PreviousTranslate != null)
                PreviousTranslate.X = 0;
        }

        public void RefreshPan()
        {
            if (!(_image?.Source is BitmapSource source) || _viewport == null || ImageTranslate == null)
                return;

            double viewportWidth = _viewport.ActualWidth;
            double viewportHeight = _viewport.ActualHeight;

            if (viewportWidth <= 0 || viewportHeight <= 0 || source.PixelWidth <= 0 || source.PixelHeight <= 0)
                return;

            if (ReferenceEquals(source, _lastPanSource) &&
                Math.Abs(viewportWidth - _lastPanViewportWidth) < 0.1 &&
                Math.Abs(viewportHeight - _lastPanViewportHeight) < 0.1)
                return;

            StopPan();

            _lastPanSource = source;
            _lastPanViewportWidth = viewportWidth;
            _lastPanViewportHeight = viewportHeight;

            double aspect = (double)source.PixelWidth / source.PixelHeight;
            double renderedHeight = viewportHeight;
            double renderedWidth = renderedHeight * aspect;

            _image.Width = renderedWidth;
            _image.Height = renderedHeight;

            _panOverflow = Math.Max(0, renderedWidth - viewportWidth);

            if (_panOverflow <= 0)
            {
                ImageTranslate.X = 0;
                return;
            }

            _panPosition = _panOverflow * BackgroundStartPosition;
            _panDirection = 1;
            _panLastRenderTime = TimeSpan.Zero;

            ImageTranslate.X = -_panPosition;

            _panStartTimer.Stop();
            _panStartTimer.Start();
        }

        private void PanStartTimer_Tick(object sender, EventArgs e)
        {
            _panStartTimer.Stop();

            if (!_isActive || _panOverflow <= 0 || ImageTranslate == null)
                return;

            _panLastRenderTime = TimeSpan.Zero;
            _panRampElapsed = 0;

            CompositionTarget.Rendering -= Pan_Rendering;
            CompositionTarget.Rendering += Pan_Rendering;
        }

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

        private void Pan_Rendering(object sender, EventArgs e)
        {
            if (!_isActive || _panOverflow <= 0 || ImageTranslate == null)
                return;

            if (!(e is RenderingEventArgs renderingArgs))
                return;

            TimeSpan renderTime = renderingArgs.RenderingTime;

            if (_panLastRenderTime == TimeSpan.Zero)
            {
                _panLastRenderTime = renderTime;
                return;
            }

            double delta = (renderTime - _panLastRenderTime).TotalSeconds;
            _panLastRenderTime = renderTime;

            if (delta <= 0 || delta > 0.1) return;

            double distanceToEdge = _panDirection > 0
                ? _panOverflow - _panPosition
                : _panPosition;

            double edgeFactor = Math.Min(1.0, Math.Max(0.08, distanceToEdge / BackgroundPanEdgeZone));

            _panRampElapsed = Math.Min(BackgroundPanRampDuration, _panRampElapsed + delta);

            double rampProgress = _panRampElapsed / BackgroundPanRampDuration;
            double rampFactor = rampProgress * rampProgress * (3.0 - 2.0 * rampProgress);

            _panPosition += BackgroundPanSpeed * edgeFactor * rampFactor * _panDirection * delta;

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
                    _particles?.SetParticlesVisible(false, true, 0.2);

                var animation = new DoubleAnimation
                {
                    To = visible ? BackgroundOpacity : 0,
                    Duration = TimeSpan.FromSeconds(0.2),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                animation.Completed += (s, e) =>
                {
                    if (!visible && !_isActive)
                        _image.Visibility = Visibility.Collapsed;
                };

                _image.Visibility = Visibility.Visible;
                _image.BeginAnimation(UIElement.OpacityProperty, animation);
            }

            if (!visible)
                CompositionTarget.Rendering -= Pan_Rendering;

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
                    _image.BeginAnimation(UIElement.OpacityProperty, null);
                    _image.Visibility = Visibility.Collapsed;
                    _image.Opacity = 0;
                }

                _particles?.SetParticlesVisible(visible, true, 0.2);
            }
        }

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