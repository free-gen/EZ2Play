using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EZ2Play.App
{
    public class TabsController
    {
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

        public async void SwitchToGamelist()
        {
            if (_isSwitching || _currentTab == TabType.Gamelist) return;

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
                _isSwitching = false;
            }
        }

        public async void SwitchToLastPlayed()
        {
            if (_isSwitching || _currentTab == TabType.LastPlayed) return;

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
                _isSwitching = false;
            }
        }

        private async Task AnimateCarouselSwitch(Action sortAction, int direction)
        {
            if (_carouselWrapper == null) return;

            if (_carouselWrapper.CacheMode == null)
                _carouselWrapper.CacheMode = new BitmapCache();

            if (!(_carouselWrapper.RenderTransform is TranslateTransform))
                _carouselWrapper.RenderTransform = new TranslateTransform();

            var transform = (TranslateTransform)_carouselWrapper.RenderTransform;

            _carouselWrapper.BeginAnimation(UIElement.OpacityProperty, null);
            transform.BeginAnimation(TranslateTransform.XProperty, null);

            _carouselWrapper.IsHitTestVisible = false;
            transform.X = _getWindowWidth() * 0.05 * direction;
            _carouselWrapper.Opacity = 0;

            await _dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

            sortAction?.Invoke();

            await _dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

            var duration = TimeSpan.FromMilliseconds(150);

            var fadeIn = new DoubleAnimation(0, 1, duration);

            var slide = new DoubleAnimation(transform.X, 0, duration)
            {
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            fadeIn.Completed += (s, e) =>
            {
                _carouselWrapper.BeginAnimation(UIElement.OpacityProperty, null);
                transform.BeginAnimation(TranslateTransform.XProperty, null);

                _carouselWrapper.Opacity = 1;
                transform.X = 0;

                _carouselWrapper.CacheMode = null;
                _carouselWrapper.IsHitTestVisible = true;
            };

            _carouselWrapper.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            transform.BeginAnimation(TranslateTransform.XProperty, slide);
        }

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

            text.BeginAnimation(UIElement.OpacityProperty, animation);

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