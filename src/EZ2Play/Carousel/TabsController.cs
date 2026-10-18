
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EZ2Play.App
{
    /// <summary>
    /// Manages switching between the game library
    /// and recently played games tabs.
    /// </summary>
    /// <remarks>
    /// Coordinates game sorting, tab appearance, and carousel
    /// transition animations.
    ///
    /// Repeated switching is blocked until the current
    /// transition finishes or is interrupted.
    /// </remarks>
    public class TabsController
    {
        /// <summary>
        /// Defines the available game library tabs.
        /// </summary>
        private enum TabType
        {
            Gamelist,
            LastPlayed
        }

        private readonly TextBlock _gamelistText;
        private readonly TextBlock _lastPlayedText;
        private readonly Grid _carouselWrapper;
        private readonly Dispatcher _dispatcher;
        private readonly Launcher _launcher;
        private readonly Sound _sound;
        private readonly Func<double> _getWindowWidth;

        private TabType _currentTab = TabType.Gamelist;
        private bool _isSwitching;

        public bool IsLastPlayed => _currentTab == TabType.LastPlayed;

        public TabsController(
            TextBlock gamelistText,
            TextBlock lastPlayedText,
            Grid carouselWrapper,
            Dispatcher dispatcher,
            Launcher launcher,
            Sound sound,
            Func<double> getWindowWidth)
        {
            _gamelistText = gamelistText;
            _lastPlayedText = lastPlayedText;
            _carouselWrapper = carouselWrapper;
            _dispatcher = dispatcher;
            _launcher = launcher;
            _sound = sound;
            _getWindowWidth = getWindowWidth;
        }

        /// <summary>
        /// Switches to the default game library ordering.
        /// </summary>
        public async void SwitchToGamelist()
        {
            if (_isSwitching || _currentTab == TabType.Gamelist)
                return;

            _sound?.PlayTabSound();
            _isSwitching = true;

            try
            {
                _currentTab = TabType.Gamelist;

                AnimateTabText(_gamelistText, true);
                AnimateTabText(_lastPlayedText, false);

                await AnimateCarouselSwitch(
                    () => _launcher.SortDefault(),
                    -1);
            }
            finally
            {
                // Always allow subsequent tab changes after
                // the current operation has finished.
                _isSwitching = false;
            }
        }

        /// <summary>
        /// Switches to games ordered by their last launch time.
        /// </summary>
        public async void SwitchToLastPlayed()
        {
            if (_isSwitching || _currentTab == TabType.LastPlayed)
                return;

            _sound?.PlayTabSound();
            _isSwitching = true;

            try
            {
                _currentTab = TabType.LastPlayed;

                AnimateTabText(_lastPlayedText, true);
                AnimateTabText(_gamelistText, false);

                await AnimateCarouselSwitch(
                    () => _launcher.SortByLastPlayed(),
                    1);
            }
            finally
            {
                // Release the switching lock even when
                // the animation does not complete normally.
                _isSwitching = false;
            }
        }

        /// <summary>
        /// Animates the carousel transition while applying
        /// the requested game sorting operation.
        /// </summary>
        /// <param name="sortAction">
        /// Action that updates the game's display ordering.
        /// </param>
        /// <param name="direction">
        /// Horizontal direction of the transition.
        /// </param>
        /// <remarks>
        /// The carousel is first moved and hidden, then its
        /// content is updated before the entrance animation.
        ///
        /// Dispatcher priorities and completion handlers are
        /// preserved to maintain the original WPF timing.
        /// </remarks>
        private async Task AnimateCarouselSwitch(
            Action sortAction,
            int direction)
        {
            if (_carouselWrapper == null)
                return;

            // Reuse an existing translation transform when possible.
            if (!(_carouselWrapper.RenderTransform is TranslateTransform))
                _carouselWrapper.RenderTransform = new TranslateTransform();

            var transform = (TranslateTransform)_carouselWrapper.RenderTransform;

            // Stop existing animations before setting the
            // initial state of the new transition.
            _carouselWrapper.BeginAnimation(UIElement.OpacityProperty, null);
            transform.BeginAnimation(TranslateTransform.XProperty, null);

            // Prevent interaction while the carousel is switching.
            _carouselWrapper.IsHitTestVisible = false;

            try
            {
                // Prepare the carousel outside its normal position.
                transform.X = _getWindowWidth() * 0.05 * direction;
                _carouselWrapper.Opacity = 0;

                // Wait for the rendering stage before changing content.
                await _dispatcher.InvokeAsync(
                    () => { },
                    DispatcherPriority.Render);

                if (!_carouselWrapper.IsVisible)
                    return;

                sortAction?.Invoke();

                // Allow WPF to process the changed item collection.
                await _dispatcher.InvokeAsync(
                    () => { },
                    DispatcherPriority.Background);

                if (!_carouselWrapper.IsVisible)
                    return;

                // Animate opacity and horizontal translation together.
                var duration = TimeSpan.FromMilliseconds(150);

                var fadeIn = new DoubleAnimation(0, 1, duration);

                var slide = new DoubleAnimation(transform.X, 0, duration)
                {
                    EasingFunction = new CubicEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                };

                // Complete the task when the animation finishes
                // or the carousel becomes unavailable.
                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                EventHandler onCompleted = (s, e) =>
                    completion.TrySetResult(true);

                RoutedEventHandler onUnloaded = (s, e) =>
                    completion.TrySetResult(false);

                DependencyPropertyChangedEventHandler onVisibilityChanged = (s, e) =>
                {
                    if (!(bool)e.NewValue)
                        completion.TrySetResult(false);
                };

                fadeIn.Completed += onCompleted;
                _carouselWrapper.Unloaded += onUnloaded;
                _carouselWrapper.IsVisibleChanged += onVisibilityChanged;

                try
                {
                    _carouselWrapper.BeginAnimation(
                        UIElement.OpacityProperty,
                        fadeIn);

                    transform.BeginAnimation(
                        TranslateTransform.XProperty,
                        slide);

                    await completion.Task;
                }
                finally
                {
                    // Remove handlers after completion or interruption
                    // to avoid retaining obsolete event subscriptions.
                    fadeIn.Completed -= onCompleted;
                    _carouselWrapper.Unloaded -= onUnloaded;
                    _carouselWrapper.IsVisibleChanged -= onVisibilityChanged;
                }
            }
            finally
            {
                // Restore the carousel's normal visual and input state.
                _carouselWrapper.BeginAnimation(
                    UIElement.OpacityProperty,
                    null);

                transform.BeginAnimation(
                    TranslateTransform.XProperty,
                    null);

                _carouselWrapper.Opacity = 1;
                transform.X = 0;
                _carouselWrapper.IsHitTestVisible = true;
            }
        }

        /// <summary>
        /// Updates a tab heading's opacity, font size,
        /// and font weight according to its active state.
        /// </summary>
        private void AnimateTabText(TextBlock text, bool active)
        {
            var animation = new DoubleAnimation
            {
                To = active ? 1.0 : 0.8,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            text.BeginAnimation(
                UIElement.OpacityProperty,
                animation);

            // Keep the font size linked to the scaled UI resources.
            if (active)
            {
                text.SetResourceReference(
                    TextBlock.FontSizeProperty,
                    UiScaleKeys.TopInfoPrimaryFontSize);
            }
            else
            {
                text.SetResourceReference(
                    TextBlock.FontSizeProperty,
                    UiScaleKeys.TopInfoSecondaryFontSize);
            }

            text.FontWeight = active
                ? FontWeights.ExtraBold
                : FontWeights.Medium;
        }
    }
}
