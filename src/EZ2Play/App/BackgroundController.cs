using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

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

        private double _panOverflow;
        private double _panPosition;
        private double _panDirection = 1;
        private TimeSpan _panLastRenderTime;

        private TranslateTransform ImageTranslate => _image?.RenderTransform as TranslateTransform;
        private TranslateTransform PreviousTranslate => _previousImage?.RenderTransform as TranslateTransform;

        private bool UseImageBackground => _image?.Source != null;

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
        }

        public bool Load(string shortcutPath)
        {
            if (_image == null) return false;

            StopPan();

            _image.BeginAnimation(UIElement.OpacityProperty, null);
            _image.Source = null;
            _image.Visibility = Visibility.Collapsed;
            _image.Opacity = 0;

            ClearPrevious();

            var bitmap = LoadBitmap(shortcutPath);

            if (bitmap == null)
                return false;

            _image.Source = bitmap;

            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);

            RefreshPan();

            return true;
        }

        private BitmapImage LoadBitmap(string shortcutPath)
        {
            try
            {
                string backgroundPath = IconExtractor.GetCustomBackgroundPath(shortcutPath);

                if (string.IsNullOrWhiteSpace(backgroundPath) || !File.Exists(backgroundPath))
                    return null;

                var bitmap = new BitmapImage();

                using (var stream = new FileStream(backgroundPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }

                bitmap.Freeze();

                return bitmap;
            }

            catch
            {
                return null;
            }
        }

        public void TransitionTo(string shortcutPath, int direction = 0)
        {
            if (_image == null) return;

            var nextBitmap = LoadBitmap(shortcutPath);

            bool hasCurrentBackground = _image.Source != null;
            bool hasNextBackground = nextBitmap != null;

            if (hasCurrentBackground && hasNextBackground)
            {
                Crossfade(nextBitmap, direction);
                return;
            }

            if (hasCurrentBackground)
            {
                FadeToParticles();
                return;
            }

            if (hasNextBackground)
            {
                FadeFromParticles(nextBitmap);
                return;
            }

            ClearPrevious();
            _particles?.SetParticlesVisible(true, true, BackgroundTransitionDuration);
        }

        private void Crossfade(BitmapImage nextBitmap, int direction)
        {
            if (_previousImage == null)
            {
                LoadFromBitmap(nextBitmap);
                Show(true);
                return;
            }

            _image.BeginAnimation(Canvas.LeftProperty, null);
            _previousImage.BeginAnimation(Canvas.LeftProperty, null);

            Canvas.SetLeft(_image, 0);
            Canvas.SetLeft(_previousImage, 0);

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

            fadeOut.Completed += (s, e) => ClearPrevious();

            fadeIn.Completed += (s, e) =>
            {
                _image.BeginAnimation(Canvas.LeftProperty, null);
                Canvas.SetLeft(_image, 0);
            };

            _previousImage.BeginAnimation(Canvas.LeftProperty, previousSlide);
            _image.BeginAnimation(Canvas.LeftProperty, nextSlide);

            _previousImage.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            _image.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void FadeToParticles()
        {
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
                StopPan();

                _image.BeginAnimation(UIElement.OpacityProperty, null);
                _image.Source = null;
                _image.Visibility = Visibility.Collapsed;
                _image.Opacity = 0;
            };

            _image.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void FadeFromParticles(BitmapImage nextBitmap)
        {
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

            _image.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void LoadFromBitmap(BitmapImage bitmap)
        {
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
            _previousImage.BeginAnimation(Canvas.LeftProperty, null);

            _previousImage.Source = null;
            _previousImage.Visibility = Visibility.Collapsed;
            _previousImage.Opacity = 0;

            Canvas.SetLeft(_previousImage, 0);

            if (PreviousTranslate != null)
                PreviousTranslate.X = 0;
        }

        public void RefreshPan()
        {
            StopPan();

            if (!(_image?.Source is BitmapSource source) || _viewport == null || ImageTranslate == null)
                return;

            _viewport.UpdateLayout();

            double viewportWidth = _viewport.ActualWidth;
            double viewportHeight = _viewport.ActualHeight;

            if (viewportWidth <= 0 || viewportHeight <= 0 || source.PixelWidth <= 0 || source.PixelHeight <= 0)
                return;

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

            CompositionTarget.Rendering += Pan_Rendering;
        }

        private void StopPan()
        {
            CompositionTarget.Rendering -= Pan_Rendering;

            _panOverflow = 0;
            _panLastRenderTime = TimeSpan.Zero;

            if (ImageTranslate != null)
                ImageTranslate.X = 0;
        }

        private void Pan_Rendering(object sender, EventArgs e)
        {
            if (_panOverflow <= 0 || ImageTranslate == null) return;
            if (!(e is RenderingEventArgs renderingArgs)) return;

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

            _panPosition += BackgroundPanSpeed * edgeFactor * _panDirection * delta;

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
                    if (!visible)
                        _image.Visibility = Visibility.Collapsed;
                };

                _image.Visibility = Visibility.Visible;
                _image.BeginAnimation(UIElement.OpacityProperty, animation);
            }

            else
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
            StopPan();
        }
    }
}