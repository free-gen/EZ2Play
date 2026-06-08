using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EZ2Play.App
{
    public static class TabsAnimation
    {
        // Анимация переключения табов (Gamelist / LastPlayed)
        public static async Task AnimateCarouselSwitch(
            Grid carouselWrapper,
            Dispatcher dispatcher,
            double windowWidth,
            Action sortAction,
            int direction)
        {
            if (carouselWrapper == null)
                return;

            if (carouselWrapper.CacheMode == null)
                carouselWrapper.CacheMode = new BitmapCache();

            if (!(carouselWrapper.RenderTransform is TranslateTransform))
            {
                carouselWrapper.RenderTransform = new TranslateTransform();
            }

            var transform = (TranslateTransform)carouselWrapper.RenderTransform;

            carouselWrapper.BeginAnimation(UIElement.OpacityProperty, null);
            transform.BeginAnimation(TranslateTransform.XProperty, null);

            carouselWrapper.IsHitTestVisible = false;
            transform.X = windowWidth * 0.05 * direction;
            carouselWrapper.Opacity = 0;

            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

            sortAction?.Invoke();

            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

            var duration = TimeSpan.FromMilliseconds(150);
            var fadeIn = new DoubleAnimation(0, 1, duration);
            var slide = new DoubleAnimation(transform.X, 0, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            fadeIn.Completed += (s, e) =>
            {
                carouselWrapper.IsHitTestVisible = true;
            };

            carouselWrapper.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            transform.BeginAnimation(TranslateTransform.XProperty, slide);
        }

        // Анимация текста вкладки (активная/неактивная)
        public static void AnimateTabText(TextBlock text, bool active)
        {
            var anim = new DoubleAnimation
            {
                To = active ? 1.0 : 0.5,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            text.BeginAnimation(UIElement.OpacityProperty, anim);

            if (active)
            {
                text.SetResourceReference(
                    TextBlock.FontSizeProperty,
                    UiScaleKeys.TopInfoPrimalyFontSize);
            }
            else
            {
                text.SetResourceReference(
                    TextBlock.FontSizeProperty,
                    UiScaleKeys.TopInfoSecondaryFontSize);
            }

            text.FontWeight = active ? FontWeights.ExtraBold : FontWeights.Medium;
        }
    }
}