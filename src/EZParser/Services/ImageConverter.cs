using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace EZParser.Services
{
    public class ImageConverter
    {
        private readonly string _saveDirectory;

        public ImageConverter(string saveDirectory = "icons")
        {
            _saveDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, saveDirectory);
            EnsureDirectoryExists();
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_saveDirectory))
            {
                Directory.CreateDirectory(_saveDirectory);
            }
        }

        public async Task<(bool success, string filename, string message)> DownloadAndConvertToIcoAsync(HttpClient session, string url, string gameName)
        {
            try
            {
                var response = await session.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var imgBytes = await response.Content.ReadAsByteArrayAsync();

                using (var imgData = new MemoryStream(imgBytes))
                using (var originalImg = Image.FromStream(imgData))
                {
                    var icoBytes = CreateIconFile(originalImg);
                    var filename = GenerateFilename(gameName, ".ico");
                    var filepath = Path.Combine(_saveDirectory, filename);

                    File.WriteAllBytes(filepath, icoBytes);

                    return (true, filename, $"Скачано: {filename}");
                }
            }
            catch (Exception e)
            {
                return (false, null, $"Ошибка ICO: {e.Message}");
            }
        }

        public async Task<(bool success, string filename, string message)> DownloadPngAsync(HttpClient session, string url, string gameName)
        {
            try
            {
                var response = await session.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var imgBytes = await response.Content.ReadAsByteArrayAsync();

                using (var imgData = new MemoryStream(imgBytes))
                using (var originalImg = Image.FromStream(imgData))
                {
                    using (var output = new MemoryStream())
                    {
                        originalImg.Save(output, ImageFormat.Png);

                        var filename = GenerateFilename(gameName, ".png");
                        var filepath = Path.Combine(_saveDirectory, filename);

                        File.WriteAllBytes(filepath, output.ToArray());

                        return (true, filename, $"Скачано: {filename}");
                    }
                }
            }
            catch (Exception e)
            {
                return (false, null, $"Ошибка PNG: {e.Message}");
            }
        }

        /// <summary>
        /// Создаёт правильный ICO файл с несколькими размерами
        /// </summary>
        private byte[] CreateIconFile(Image sourceImage)
        {
            var sizes = new[] { 256, 128, 64, 48, 32, 24, 16 };
            var iconEntries = new List<byte[]>();
            var iconOffsets = new List<int>();

            using (var ms = new MemoryStream())
            {
                // Резервируем место под заголовок (6 байт) и_entries (16 байт * количество размеров)
                int headerSize = 6 + (sizes.Length * 16);
                int currentOffset = headerSize;

                foreach (var size in sizes)
                {
                    using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        graphics.DrawImage(sourceImage, 0, 0, size, size);

                        // Конвертируем в BMP формат для ICO
                        var bmpBytes = BitmapToIconBitmap(bitmap, size);
                        iconEntries.Add(bmpBytes);
                        iconOffsets.Add(currentOffset);
                        currentOffset += bmpBytes.Length;
                    }
                }

                // Пишем ICO заголовок
                ms.Write(new byte[] { 0, 0, 1, 0, (byte)sizes.Length, 0 }, 0, 6);

                // Пишем entries
                for (int i = 0; i < sizes.Length; i++)
                {
                    var size = sizes[i];
                    var entry = new byte[16];

                    // Width (0 = 256)
                    entry[0] = size >= 256 ? (byte)0 : (byte)size;
                    // Height
                    entry[1] = size >= 256 ? (byte)0 : (byte)size;
                    // Color palette (0 = no palette)
                    entry[2] = 0;
                    // Reserved
                    entry[3] = 0;
                    // Color planes (1 or 0)
                    entry[4] = 0;
                    entry[5] = 0;
                    // Bits per pixel (32)
                    entry[6] = 32;
                    entry[7] = 0;
                    // Image size
                    BitConverter.GetBytes(iconEntries[i].Length).CopyTo(entry, 8);
                    // Offset
                    BitConverter.GetBytes(iconOffsets[i]).CopyTo(entry, 12);

                    ms.Write(entry, 0, 16);
                }

                // Пишем bitmap данные
                foreach (var entry in iconEntries)
                {
                    ms.Write(entry, 0, entry.Length);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Конвертирует Bitmap в формат ICO BITMAPINFO
        /// </summary>
        private byte[] BitmapToIconBitmap(Bitmap bitmap, int size)
        {
            // ICO требует XOR mask (цвет) + AND mask (прозрачность)
            int width = size;
            int height = size * 2; // XOR + AND маски

            var result = new byte[40 + (width * height * 4) + (width * height / 8)];
            
            // BITMAPINFOHEADER (40 байт)
            BitConverter.GetBytes(40).CopyTo(result, 0);      // Header size
            BitConverter.GetBytes(width).CopyTo(result, 4);   // Width
            BitConverter.GetBytes(height).CopyTo(result, 8);  // Height (x2 для масок)
            BitConverter.GetBytes((short)1).CopyTo(result, 12); // Planes
            BitConverter.GetBytes((short)32).CopyTo(result, 14); // Bit count
            BitConverter.GetBytes(0).CopyTo(result, 16);      // Compression (none)
            BitConverter.GetBytes(0).CopyTo(result, 20);      // Image size
            BitConverter.GetBytes(0).CopyTo(result, 24);      // X pixels per meter
            BitConverter.GetBytes(0).CopyTo(result, 28);      // Y pixels per meter
            BitConverter.GetBytes(0).CopyTo(result, 32);      // Colors used
            BitConverter.GetBytes(0).CopyTo(result, 36);      // Important colors

            // Копируем пиксели (BGRA формат, снизу вверх)
            int pixelOffset = 40;
            for (int y = size - 1; y >= 0; y--)
            {
                for (int x = 0; x < size; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    result[pixelOffset++] = pixel.B;
                    result[pixelOffset++] = pixel.G;
                    result[pixelOffset++] = pixel.R;
                    result[pixelOffset++] = pixel.A;
                }
            }

            // AND mask (1 бит на пиксель, 0 = прозрачный)
            int andMaskOffset = 40 + (width * size * 4);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x += 8)
                {
                    byte andByte = 0;
                    for (int bit = 0; bit < 8 && x + bit < size; bit++)
                    {
                        var pixel = bitmap.GetPixel(x + bit, y);
                        // 0 = прозрачный, 1 = непрозрачный
                        if (pixel.A < 128)
                            andByte |= (byte)(1 << (7 - bit));
                    }
                    result[andMaskOffset++] = andByte;
                }
            }

            return result;
        }

        private string GenerateFilename(string gameName, string extension)
        {
            var safeName = Regex.Replace(gameName, @"[<>:""/\\|?*]", "_");
            safeName = Regex.Replace(safeName, @"\s+", "_");
            safeName = safeName.Trim('_');

            if (safeName.Length > 100)
                safeName = safeName.Substring(0, 100);

            if (string.IsNullOrEmpty(safeName))
                safeName = "unnamed";

            var baseName = safeName;
            var counter = 1;
            var filename = $"{baseName}{extension}";

            while (File.Exists(Path.Combine(_saveDirectory, filename)))
            {
                filename = $"{baseName}_{counter}{extension}";
                counter++;
            }

            return filename;
        }

        public string GetSaveDirectory()
        {
            return _saveDirectory;
        }
    }
}