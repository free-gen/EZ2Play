using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EZ2Play.App
{
    public class CoverAnimation
    {
        private const double NormalSize = 220.0;
        private const double SelectedSize = 256.0;
        private const double AnimationDuration = 0.3;

        /// Анимирует увеличение обложки при выборе
        public static void AnimateSelection(Image coverImage, Border opacityMaskBorder, bool isSelected)
        {
            if (coverImage == null) return;

            double targetSize = isSelected ? SelectedSize : NormalSize;

            // Анимация размера изображения
            var sizeAnimation = new DoubleAnimation
            {
                To = targetSize,
                Duration = TimeSpan.FromSeconds(AnimationDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // Применяем анимацию только к изображению
            coverImage.BeginAnimation(FrameworkElement.WidthProperty, sizeAnimation);
            coverImage.BeginAnimation(FrameworkElement.HeightProperty, sizeAnimation);
            
            // Синхронизируем размер маски прозрачности (если она есть)
            if (opacityMaskBorder != null)
            {
                opacityMaskBorder.BeginAnimation(FrameworkElement.WidthProperty, sizeAnimation);
                opacityMaskBorder.BeginAnimation(FrameworkElement.HeightProperty, sizeAnimation);
            }
        }

        /// Быстрая анимация без плавности (для мгновенного изменения)
        public static void SetSizeInstant(Image coverImage, Border opacityMaskBorder, bool isSelected)
        {
            if (coverImage == null) return;

            double size = isSelected ? SelectedSize : NormalSize;

            coverImage.Width = size;
            coverImage.Height = size;
            
            if (opacityMaskBorder != null)
            {
                opacityMaskBorder.Width = size;
                opacityMaskBorder.Height = size;
            }
        }

        /// Останавливает все анимации для элемента
        public static void StopAnimations(Image coverImage, Border opacityMaskBorder)
        {
            if (coverImage != null)
            {
                coverImage.BeginAnimation(FrameworkElement.WidthProperty, null);
                coverImage.BeginAnimation(FrameworkElement.HeightProperty, null);
            }

            if (opacityMaskBorder != null)
            {
                opacityMaskBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                opacityMaskBorder.BeginAnimation(FrameworkElement.HeightProperty, null);
            }
        }
    }
} 