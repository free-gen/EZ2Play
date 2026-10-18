
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EZ2Play.App
{
    /// <summary>
    /// A WPF Border that generates a dithered gradient
    /// background using an 8x8 Bayer threshold matrix.
    /// </summary>
    /// <remarks>
    /// The gradient is generated as a bitmap rather than
    /// using a standard WPF gradient brush.
    ///
    /// The resulting image is cached as the Border background
    /// and regenerated when its pixel dimensions change.
    /// </remarks>
    public class DitheredGradientBorder : Border
    {
        // Gradient colors loaded from application resources.
        private static Color StartColor;
        private static Color EndColor;
        private static bool _colorsInitialized;

        // Ordered dithering threshold matrix (8x8 Bayer).
        // The original values and their order must be preserved.
        private static readonly byte[,] Bayer8 =
        {
            {  0, 48, 12, 60,  3, 51, 15, 63 },
            { 32, 16, 44, 28, 35, 19, 47, 31 },
            {  8, 56,  4, 52, 11, 59,  7, 55 },
            { 40, 24, 36, 20, 43, 27, 39, 23 },
            {  2, 50, 14, 62,  1, 49, 13, 61 },
            { 34, 18, 46, 30, 33, 17, 45, 29 },
            { 10, 58,  6, 54,  9, 57,  5, 53 },
            { 42, 26, 38, 22, 41, 25, 37, 21 }
        };

        // Last generated gradient dimensions in physical pixels.
        private int _gradientPixelWidth;
        private int _gradientPixelHeight;

        public DitheredGradientBorder()
        {
            SizeChanged += DitheredGradientBorder_SizeChanged;
        }

        /// <summary>
        /// Loads gradient colors from the application's
        /// WPF resource dictionary.
        /// </summary>
        /// <remarks>
        /// Colors are initialized once and shared by all
        /// DitheredGradientBorder instances.
        /// </remarks>
        private static void EnsureColorsInitialized()
        {
            if (_colorsInitialized) return;

            StartColor = ((SolidColorBrush)Application.Current
                .Resources["OverlayGradientStartBrush"]).Color;

            EndColor = ((SolidColorBrush)Application.Current
                .Resources["OverlayGradientEndBrush"]).Color;

            _colorsInitialized = true;
        }

        /// <summary>
        /// Recreates the gradient background when the Border
        /// changes its physical pixel dimensions.
        /// </summary>
        /// <remarks>
        /// The actual WPF size is multiplied by the current
        /// display DPI scale to determine the bitmap dimensions.
        ///
        /// Repeated size changes that result in the same pixel
        /// dimensions do not regenerate the background.
        /// </remarks>
        private void DitheredGradientBorder_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;

            var dpi = VisualTreeHelper.GetDpi(this);

            int pixelWidth = Math.Max(
                1,
                (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX));

            int pixelHeight = Math.Max(
                1,
                (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY));

            if (pixelWidth == _gradientPixelWidth &&
                pixelHeight == _gradientPixelHeight)
            {
                return;
            }

            _gradientPixelWidth = pixelWidth;
            _gradientPixelHeight = pixelHeight;

            Background = CreateBrush(
                pixelWidth,
                pixelHeight,
                96.0 * dpi.DpiScaleX,
                96.0 * dpi.DpiScaleY);
        }

        /// <summary>
        /// Generates a dithered gradient bitmap and returns
        /// a frozen ImageBrush containing the result.
        /// </summary>
        /// <remarks>
        /// The gradient runs diagonally from the bottom-left
        /// corner toward the top-right corner.
        ///
        /// Each color channel is interpolated separately
        /// and quantized using the Bayer threshold matrix.
        ///
        /// Pixel data uses the BGRA32 format.
        /// </remarks>
        private static ImageBrush CreateBrush(
            int pixelWidth,
            int pixelHeight,
            double dpiX,
            double dpiY)
        {
            EnsureColorsInitialized();

            // Four bytes per pixel: blue, green, red, alpha.
            int stride = pixelWidth * 4;
            byte[] pixels = new byte[stride * pixelHeight];

            for (int y = 0; y < pixelHeight; y++)
            {
                double normalizedY = pixelHeight > 1
                    ? (double)y / (pixelHeight - 1)
                    : 0;

                for (int x = 0; x < pixelWidth; x++)
                {
                    double normalizedX = pixelWidth > 1
                        ? (double)x / (pixelWidth - 1)
                        : 0;

                    // Gradient direction: bottom-left -> top-right.
                    double t = (normalizedX + (1.0 - normalizedY)) * 0.5;

                    // Repeat the 8x8 Bayer matrix across the bitmap.
                    int threshold = Bayer8[y & 7, x & 7];

                    // Interpolate each RGB channel independently.
                    double red =
                        StartColor.R + (EndColor.R - StartColor.R) * t;

                    double green =
                        StartColor.G + (EndColor.G - StartColor.G) * t;

                    double blue =
                        StartColor.B + (EndColor.B - StartColor.B) * t;

                    int offset = y * stride + x * 4;

                    // BGRA32 channel order.
                    pixels[offset] = Quantize(blue, threshold);
                    pixels[offset + 1] = Quantize(green, threshold);
                    pixels[offset + 2] = Quantize(red, threshold);
                    pixels[offset + 3] = 255;
                }
            }

            // Create and freeze the generated bitmap.
            var bitmap = BitmapSource.Create(
                pixelWidth,
                pixelHeight,
                dpiX,
                dpiY,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride);

            bitmap.Freeze();

            // Use the bitmap as the Border background.
            var brush = new ImageBrush(bitmap)
            {
                Stretch = Stretch.Fill
            };

            brush.Freeze();

            return brush;
        }

        /// <summary>
        /// Quantizes a color channel using the specified
        /// Bayer matrix threshold.
        /// </summary>
        /// <remarks>
        /// The fractional part of the channel value is
        /// compared against a normalized threshold.
        ///
        /// The result is restricted to the byte range
        /// from 0 to 255.
        /// </remarks>
        private static byte Quantize(
            double value,
            int thresholdIndex)
        {
            double lower = Math.Floor(value);
            double fraction = value - lower;
            double threshold = (thresholdIndex + 0.5) / 64.0;

            int result = (int)lower;

            if (fraction > threshold)
                result++;

            return (byte)Math.Max(0, Math.Min(255, result));
        }
    }
}
