
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EZ2Play.App
{
    /// <summary>
    /// Manages the application's startup splash screen,
    /// including logo loading and entrance animations.
    /// </summary>
    /// <remarks>
    /// The splash sequence uses DispatcherTimer delays
    /// and WPF opacity animations.
    ///
    /// The completion callback is invoked after the
    /// sequence finishes or immediately when the splash
    /// screen is skipped.
    /// </remarks>
    public class SplashScreen
    {
        // Visual elements controlled by the splash screen.
        private readonly Image _logoImage;
        private readonly Grid _splashOverlay;
        private readonly Grid _mainScreenGrid;

        public SplashScreen(
            Image logoImage,
            Grid splashOverlay,
            Grid mainScreenGrid)
        {
            _logoImage = logoImage;
            _splashOverlay = splashOverlay;
            _mainScreenGrid = mainScreenGrid;
        }

        /// <summary>
        /// Runs the logo animation sequence and invokes
        /// the completion callback when it finishes.
        /// </summary>
        /// <remarks>
        /// The original sequence consists of:
        ///
        /// 1. Initial delay of 500 ms.
        /// 2. Logo fade-in over 1000 ms.
        /// 3. Delay of 500 ms.
        /// 4. Logo fade-out over 500 ms.
        /// 5. Final delay of 1000 ms.
        ///
        /// Each subsequent operation starts only after
        /// the previous operation has completed.
        /// </remarks>
        public void RunSequence(Action onComplete)
        {
            if (_logoImage == null)
            {
                onComplete?.Invoke();
                return;
            }

            // Load the configured logo before starting animations.
            TryLoadLogoInto(_logoImage);

            // Animate an element from transparent to fully visible.
            void AnimateIn(UIElement el, int durationMs, Action after)
            {
                var anim = new DoubleAnimation(
                    0,
                    1,
                    TimeSpan.FromMilliseconds(durationMs))
                {
                    EasingFunction = new CubicEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                };

                anim.Completed += (s, e) => after?.Invoke();

                el.BeginAnimation(
                    UIElement.OpacityProperty,
                    anim);
            }

            // Animate an element from visible to transparent.
            void AnimateOut(UIElement el, int durationMs, Action after)
            {
                var anim = new DoubleAnimation(
                    1,
                    0,
                    TimeSpan.FromMilliseconds(durationMs))
                {
                    EasingFunction = new CubicEase
                    {
                        EasingMode = EasingMode.EaseIn
                    }
                };

                anim.Completed += (s, e) => after?.Invoke();

                el.BeginAnimation(
                    UIElement.OpacityProperty,
                    anim);
            }

            // Execute an action after the specified delay.
            void Delay(int ms, Action after)
            {
                var t = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(ms)
                };

                t.Tick += (s, e) =>
                {
                    t.Stop();
                    after?.Invoke();
                };

                t.Start();
            }

            // Preserve the original nested animation sequence.
            Delay(500, () =>
                AnimateIn(_logoImage, 1000, () =>
                    Delay(500, () =>
                        AnimateOut(_logoImage, 500, () =>
                            Delay(1000, () => onComplete?.Invoke())))));
        }

        /// <summary>
        /// Displays the application window and optionally
        /// runs the startup splash animation.
        /// </summary>
        /// <param name="skipSplash">
        /// When true, skips the splash sequence and invokes
        /// the completion callback immediately.
        /// </param>
        /// <param name="onAfterSplash">
        /// Callback invoked after the splash sequence finishes
        /// or when the sequence is skipped.
        /// </param>
        /// <remarks>
        /// The main screen is initially collapsed.
        ///
        /// When the splash is enabled, the application
        /// window fades in before the logo sequence begins.
        ///
        /// The splash overlay is hidden before invoking
        /// the completion callback.
        /// </remarks>
        public void ShowWithAnimation(
            bool skipSplash,
            Action onAfterSplash)
        {
            // Hide the main interface during startup.
            if (_mainScreenGrid != null)
                _mainScreenGrid.Visibility = Visibility.Collapsed;

            if (_splashOverlay != null)
                _splashOverlay.Visibility = Visibility.Collapsed;

            var window = Application.Current?.MainWindow;

            if (window != null)
                window.Visibility = Visibility.Visible;

            // Skip the splash sequence when requested.
            if (skipSplash)
            {
                if (window != null)
                    window.Opacity = 1.0;

                onAfterSplash?.Invoke();
                return;
            }

            // Prepare the window for its entrance animation.
            if (window != null)
                window.Opacity = 0.0;

            if (_splashOverlay != null)
                _splashOverlay.Visibility = Visibility.Visible;

            // Fade in the application window.
            var animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(800),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            // Start the logo sequence after the window
            // entrance animation has completed.
            animation.Completed += (s, e) =>
            {
                RunSequence(() =>
                {
                    if (_splashOverlay != null)
                        _splashOverlay.Visibility = Visibility.Hidden;

                    onAfterSplash?.Invoke();
                });
            };

            if (window != null)
            {
                window.BeginAnimation(
                    UIElement.OpacityProperty,
                    animation);
            }
        }

        /// <summary>
        /// Loads the splash logo from the external UI pack,
        /// falling back to the embedded application asset.
        /// </summary>
        /// <remarks>
        /// The external Logo.png has priority.
        ///
        /// BitmapCacheOption.OnLoad allows the external
        /// stream to be closed after image initialization.
        ///
        /// Loading failures are ignored to preserve
        /// the original startup behavior.
        /// </remarks>
        private static void TryLoadLogoInto(Image image)
        {
            if (image == null) return;

            // First try to load the logo from ui.pack.
            try
            {
                using (var logoFromPack =
                    PackLoader.LoadFromPack("Logo.png"))
                {
                    if (logoFromPack != null)
                    {
                        var bitmap = new BitmapImage();

                        bitmap.BeginInit();
                        bitmap.StreamSource = logoFromPack;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();

                        image.Source = bitmap;

                        RenderOptions.SetBitmapScalingMode(
                            image,
                            BitmapScalingMode.HighQuality);

                        return;
                    }
                }
            }
            catch
            {
            }

            // Fall back to the embedded application logo.
            try
            {
                var uri = new Uri(
                    "pack://application:,,,/Assets/Logo.png",
                    UriKind.Absolute);

                var bitmap = new BitmapImage(uri);

                RenderOptions.SetBitmapScalingMode(
                    image,
                    BitmapScalingMode.HighQuality);

                image.Source = bitmap;

                return;
            }
            catch
            {
            }
        }
    }
}
