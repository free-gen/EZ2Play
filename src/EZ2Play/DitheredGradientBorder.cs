using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EZ2Play.App
{
    public class DitheredGradientBorder : Border
    {
        private static readonly Color StartColor = Color.FromRgb(0x30, 0x33, 0x3D);
        private static readonly Color EndColor = Color.FromRgb(0x1A, 0x1E, 0x23);

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

        private int _gradientPixelWidth;
        private int _gradientPixelHeight;

        public DitheredGradientBorder()
        {
            SizeChanged += DitheredGradientBorder_SizeChanged;
        }

        private void DitheredGradientBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            var dpi = VisualTreeHelper.GetDpi(this);
            int pixelWidth = Math.Max(1, (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX));
            int pixelHeight = Math.Max(1, (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY));

            if (pixelWidth == _gradientPixelWidth && pixelHeight == _gradientPixelHeight) return;

            _gradientPixelWidth = pixelWidth;
            _gradientPixelHeight = pixelHeight;

            Background = CreateBrush(pixelWidth, pixelHeight, 96.0 * dpi.DpiScaleX, 96.0 * dpi.DpiScaleY);
        }

        private static ImageBrush CreateBrush(int pixelWidth, int pixelHeight, double dpiX, double dpiY)
        {
            int stride = pixelWidth * 4;
            byte[] pixels = new byte[stride * pixelHeight];

            for (int y = 0; y < pixelHeight; y++)
            {
                double normalizedY = pixelHeight > 1 ? (double)y / (pixelHeight - 1) : 0;

                for (int x = 0; x < pixelWidth; x++)
                {
                    double normalizedX = pixelWidth > 1 ? (double)x / (pixelWidth - 1) : 0;

                    // Gradient direction: bottom-left -> top-right.
                    double t = (normalizedX + (1.0 - normalizedY)) * 0.5;
                    int threshold = Bayer8[y & 7, x & 7];

                    double red = StartColor.R + (EndColor.R - StartColor.R) * t;
                    double green = StartColor.G + (EndColor.G - StartColor.G) * t;
                    double blue = StartColor.B + (EndColor.B - StartColor.B) * t;

                    int offset = y * stride + x * 4;

                    pixels[offset] = Quantize(blue, threshold);
                    pixels[offset + 1] = Quantize(green, threshold);
                    pixels[offset + 2] = Quantize(red, threshold);
                    pixels[offset + 3] = 255;
                }
            }

            var bitmap = BitmapSource.Create(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Bgra32, null, pixels, stride);
            bitmap.Freeze();

            var brush = new ImageBrush(bitmap) { Stretch = Stretch.Fill };
            brush.Freeze();

            return brush;
        }

        private static byte Quantize(double value, int thresholdIndex)
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