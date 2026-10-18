
using System;
using System.IO;
using System.Threading;

using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using DrawingImaging = System.Drawing.Imaging;

namespace EZ2Play.App
{
    /// <summary>
    /// Validates and saves downloaded cover and background images.
    /// </summary>
    /// <remarks>
    /// All methods perform synchronous work on the calling thread.
    /// ParserOverlay owns Task.Run, cancellation, UI updates, and cleanup.
    ///
    /// The temporary path callback updates the parser's existing local
    /// variable at the original assignment and reset points. This allows
    /// the parser's existing finally block to clean up unfinished files.
    /// </remarks>
    internal static class ArtworkFileStore
    {
        // Original source image validation limits.
        private const int MaxSourceImageDimension = 16384;
        private const long MaxSourceImagePixels = 40_000_000L;

        /// <summary>
        /// Validates the PNG or JPEG header and source dimensions before decoding.
        /// </summary>
        /// <param name="bytes">Downloaded image data.</param>
        internal static void ValidateSourceImageHeader(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 24)
                throw new InvalidDataException("Invalid image header.");

            long width = 0;
            long height = 0;

            // PNG: signature + IHDR.
            if (bytes[0] == 137 && bytes[1] == 80 &&
                bytes[2] == 78 && bytes[3] == 71 &&
                bytes[4] == 13 && bytes[5] == 10 &&
                bytes[6] == 26 && bytes[7] == 10)
            {
                if (bytes[8] != 0 || bytes[9] != 0 ||
                    bytes[10] != 0 || bytes[11] != 13 ||
                    bytes[12] != 73 || bytes[13] != 72 ||
                    bytes[14] != 68 || bytes[15] != 82)
                {
                    throw new InvalidDataException(
                        "Invalid PNG header.");
                }

                width =
                    ((long)bytes[16] << 24) |
                    ((long)bytes[17] << 16) |
                    ((long)bytes[18] << 8) |
                    bytes[19];

                height =
                    ((long)bytes[20] << 24) |
                    ((long)bytes[21] << 16) |
                    ((long)bytes[22] << 8) |
                    bytes[23];
            }
            // JPEG: find Start Of Frame marker.
            else if (bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                int offset = 2;
                bool found = false;

                while (offset < bytes.Length)
                {
                    if (bytes[offset++] != 0xFF)
                        break;

                    while (offset < bytes.Length &&
                           bytes[offset] == 0xFF)
                    {
                        offset++;
                    }

                    if (offset >= bytes.Length)
                        break;

                    int marker = bytes[offset++];

                    if (marker == 0xD9 || marker == 0xDA)
                        break;

                    if (marker == 0x01 || marker == 0xD8 ||
                        (marker >= 0xD0 && marker <= 0xD7))
                    {
                        continue;
                    }

                    if (offset > bytes.Length - 2)
                        break;

                    int length =
                        (bytes[offset] << 8) |
                        bytes[offset + 1];

                    if (length < 2 ||
                        length > bytes.Length - offset)
                    {
                        break;
                    }

                    bool isSof =
                        marker >= 0xC0 &&
                        marker <= 0xCF &&
                        marker != 0xC4 &&
                        marker != 0xC8 &&
                        marker != 0xCC;

                    if (isSof)
                    {
                        if (length < 7)
                            break;

                        height =
                            (bytes[offset + 3] << 8) |
                            bytes[offset + 4];

                        width =
                            (bytes[offset + 5] << 8) |
                            bytes[offset + 6];

                        found = true;
                        break;
                    }

                    offset += length;
                }

                if (!found)
                    throw new InvalidDataException("Invalid JPEG header.");
            }
            else
            {
                throw new InvalidDataException(
                    "Unsupported image format.");
            }

            if (width <= 0 ||
                height <= 0 ||
                width > MaxSourceImageDimension ||
                height > MaxSourceImageDimension ||
                width * height > MaxSourceImagePixels)
            {
                throw new InvalidDataException(
                    $"Image dimensions exceed supported limits: {width}x{height}.");
            }
        }

        /// <summary>
        /// Checks dimensions of an image that has already been decoded.
        /// </summary>
        private static void ValidateSourceImageDimensions(Drawing.Image image)
        {
            int width = image.Width;
            int height = image.Height;

            if (width <= 0 ||
                height <= 0 ||
                width > MaxSourceImageDimension ||
                height > MaxSourceImageDimension ||
                (long)width * height > MaxSourceImagePixels)
            {
                throw new InvalidDataException(
                    $"Image dimensions exceed supported limits: {width}x{height}.");
            }
        }

        /// <summary>
        /// Center-crops an image, saves a 512x512 PNG, and verifies it.
        /// </summary>
        /// <param name="bytes">Downloaded source image bytes.</param>
        /// <param name="shortcutName">The shortcut's Name, not DisplayName.</param>
        /// <param name="cancellationToken">The parser session cancellation token.</param>
        /// <param name="setTemporaryPath">
        /// Updates the parser's temporary path for cleanup in its finally block.
        /// </param>
        /// <remarks>
        /// Called inside ParserOverlay's existing Task.Run. Do not move
        /// the task boundary or its cancellation token into this class.
        /// </remarks>
        internal static void SaveCover(
            byte[] bytes,
            string shortcutName,
            CancellationToken cancellationToken,
            Action<string> setTemporaryPath)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateSourceImageHeader(bytes);

            string coversDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "shortcuts",
                "covers");

            Directory.CreateDirectory(coversDirectory);

            // Use the actual shortcut name instead of DisplayName.
            string coverPath = Path.Combine(
                coversDirectory,
                shortcutName + ".png");

            string coverTempPath = Path.Combine(
                coversDirectory,
                shortcutName + "." +
                Guid.NewGuid().ToString("N") + ".tmp");

            setTemporaryPath(coverTempPath);

            using (var input = new MemoryStream(bytes))
            using (var sourceImage =
                Drawing.Image.FromStream(input, true, true))
            using (var resizedImage = new Drawing.Bitmap(
                512,
                512,
                DrawingImaging.PixelFormat.Format32bppArgb))
            {
                ValidateSourceImageDimensions(sourceImage);

                using (var graphics = Drawing.Graphics.FromImage(resizedImage))
                {
                    graphics.Clear(Drawing.Color.Transparent);

                    graphics.CompositingQuality =
                        Drawing2D.CompositingQuality.HighQuality;

                    graphics.InterpolationMode =
                        Drawing2D.InterpolationMode.HighQualityBicubic;

                    graphics.SmoothingMode =
                        Drawing2D.SmoothingMode.HighQuality;

                    graphics.PixelOffsetMode =
                        Drawing2D.PixelOffsetMode.HighQuality;

                    float sourceSize = Math.Min(
                        sourceImage.Width,
                        sourceImage.Height);

                    float sourceX =
                        (sourceImage.Width - sourceSize) / 2f;

                    float sourceY =
                        (sourceImage.Height - sourceSize) / 2f;

                    var destRect = new Drawing.RectangleF(0, 0, 512, 512);
                    var sourceRect = new Drawing.RectangleF(
                        sourceX, sourceY, sourceSize, sourceSize);

                    graphics.DrawImage(
                        sourceImage,
                        destRect,
                        sourceRect,
                        Drawing.GraphicsUnit.Pixel);
                }

                cancellationToken.ThrowIfCancellationRequested();

                using (var output = new FileStream(
                    coverTempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    resizedImage.Save(
                        output,
                        DrawingImaging.ImageFormat.Png);
                }

                using (var verifyStream = new FileStream(
                    coverTempPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                using (var verifyImage = Drawing.Image.FromStream(
                    verifyStream, true, true))
                {
                    if (verifyImage.Width != 512 ||
                        verifyImage.Height != 512)
                    {
                        throw new InvalidDataException(
                            "Saved cover has invalid dimensions.");
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(coverPath))
                    File.Replace(coverTempPath, coverPath, null);
                else
                    File.Move(coverTempPath, coverPath);

                setTemporaryPath(null);
            }
        }

        /// <summary>
        /// Verifies and saves the original background bytes without resizing.
        /// </summary>
        /// <remarks>
        /// Called inside ParserOverlay's existing Task.Run. The original
        /// validation, save, verification, and old-extension cleanup order
        /// is retained. The caller still owns cleanup in its finally block.
        /// </remarks>
        internal static void SaveBackground(
            byte[] bytes,
            string shortcutName,
            CancellationToken cancellationToken,
            Action<string> setTemporaryPath)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateSourceImageHeader(bytes);

            string extension;

            // Validate the image before writing it.
            using (var input = new MemoryStream(bytes))
            using (var sourceImage = Drawing.Image.FromStream(input, true, true))
            {
                ValidateSourceImageDimensions(sourceImage);

                if (sourceImage.Width < 3840)
                {
                    throw new InvalidDataException(
                        $"Background width is only {sourceImage.Width}px. Minimum required width is 3840px.");
                }

                extension = sourceImage.RawFormat.Guid ==
                    DrawingImaging.ImageFormat.Png.Guid ? ".png" : ".jpg";
            }

            string backgroundsDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "shortcuts",
                "backgrounds");

            Directory.CreateDirectory(backgroundsDirectory);

            string backgroundPath = Path.Combine(
                backgroundsDirectory,
                shortcutName + extension);

            string tempPath = Path.Combine(
                backgroundsDirectory,
                shortcutName + "." +
                Guid.NewGuid().ToString("N") + ".tmp");

            setTemporaryPath(tempPath);

            // Preserve the downloaded image bytes.
            using (var output = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                output.Write(bytes, 0, bytes.Length);
            }

            // Verify the saved temporary image.
            using (var verifyStream = new FileStream(
                tempPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            using (var verifyImage = Drawing.Image.FromStream(
                verifyStream, true, true))
            {
                ValidateSourceImageDimensions(verifyImage);

                if (verifyImage.Width < 3840)
                {
                    throw new InvalidDataException(
                        "Saved background has invalid dimensions.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(backgroundPath))
                File.Replace(tempPath, backgroundPath, null);
            else
                File.Move(tempPath, backgroundPath);

            setTemporaryPath(null);

            string[] extensions =
            {
                ".png",
                ".jpg",
                ".jpeg"
            };

            foreach (string oldExtension in extensions)
            {
                string oldPath = Path.Combine(
                    backgroundsDirectory,
                    shortcutName + oldExtension);

                if (!string.Equals(
                        oldPath,
                        backgroundPath,
                        StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(oldPath))
                {
                    try
                    {
                        File.Delete(oldPath);
                    }
                    catch (Exception ex)
                    {
                        DebugLog.Error(
                            "Parser",
                            ex,
                            $"Failed to remove old background '{oldPath}'.");
                    }
                }
            }
        }
    }
}
