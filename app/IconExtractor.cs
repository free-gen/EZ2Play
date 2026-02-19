using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

namespace EZ2Play.App
{
    public static class IconExtractor
    {
        private const string LogFile = "EZ2Play.log";
        private const string ShortcutsDirectory = "shortcuts";
        
        private static void Log(string message)
        {
            if (EZ2Play.Main.App.EnableLogging)
            {
                try { File.AppendAllText(LogFile, $"[{DateTime.Now}] IconExtractor: {message}\n"); }
                catch { /* Ignore */ }
            }
        }
        private const int IconSize = 64; // Размер иконки в пикселях (вертикальный режим)
        private const int HorizontalIconSize = 256; // Размер иконки в пикселях (горизонтальный режим)

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, 
            out SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHDefExtractIconW(string pszIconFile, int iIndex, uint uFlags, 
            out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIconSize);

        [DllImport("shell32.dll")]
        private static extern bool ExtractIconEx(string lpszFile, int nIconIndex, 
            out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        private const uint SHGFI_ICONLOCATION = 0x1000;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_ICON = 0x100;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        // Получаем путь к иконке из ярлыка
        private static string GetIconPathFromShortcut(string lnkPath)
        {
            try
            {
                Type shellLinkType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellLinkType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellLinkType);
                    dynamic shortcut = shell.CreateShortcut(lnkPath);
                    
                    string iconLocation = shortcut.IconLocation;
                    string targetPath = shortcut.TargetPath;
                    shortcut = null;
                    shell = null;
                    
                    if (!string.IsNullOrWhiteSpace(iconLocation))
                        return iconLocation;
                    else if (!string.IsNullOrWhiteSpace(targetPath) && targetPath.Length > 0)
                        return targetPath + ",0";
                }
                else
                {
                    Log($"WScript.Shell type not found for shortcut '{lnkPath}'");
                }
            }
            catch (Exception ex) 
            { 
                Log($"Error getting icon path from shortcut '{lnkPath}': {ex.Message}");
            }

            return null;
        }
        // Извлекаем иконку из файла
        private static IntPtr ExtractHdIconFromFile(string iconLocation, int defaultIconIndex, int size)
        {
            try
            {
                // Проверяем корректность входного параметра
                if (string.IsNullOrWhiteSpace(iconLocation) || iconLocation.StartsWith(","))
                {
                    // Логируем только если это не стандартная проблема с ",0"
                    if (iconLocation != ",0")
                    {
                        Log($"Invalid icon location: '{iconLocation}'");
                    }
                    return IntPtr.Zero;
                }
                
                // Парсим путь к иконке и индекс
                string iconPath = iconLocation;
                int iconIndex = defaultIconIndex;

                int commaIndex = iconLocation.LastIndexOf(',');
                if (commaIndex > 0)
                {
                    iconPath = iconLocation.Substring(0, commaIndex);
                    if (int.TryParse(iconLocation.Substring(commaIndex + 1), out int parsedIndex))
                        iconIndex = parsedIndex;
                }

                if (!File.Exists(iconPath))
                {
                    Log($"Icon file does not exist: {iconPath}");
                    return IntPtr.Zero;
                }

                // Пробуем SHDefExtractIconW (основной метод)
                IntPtr hIconLarge, hIconSmall;
                IntPtr result = SHDefExtractIconW(iconPath, iconIndex, 0, out hIconLarge, out hIconSmall, (uint)size);
                
                if (result == IntPtr.Zero)
                {
                    IntPtr hIcon = (size >= 32) ? hIconLarge : hIconSmall;
                    if (hIcon != IntPtr.Zero)
                    {
                        if (hIcon == hIconLarge && hIconSmall != IntPtr.Zero)
                            DestroyIcon(hIconSmall);
                        else if (hIcon == hIconSmall && hIconLarge != IntPtr.Zero)
                            DestroyIcon(hIconLarge);
                        return hIcon;
                    }
                }

                // Fallback: ExtractIconEx
                bool extractResult = ExtractIconEx(iconPath, iconIndex, out hIconLarge, out hIconSmall, 1);
                
                if (extractResult)
                {
                    IntPtr hIcon = (size >= 32) ? hIconLarge : hIconSmall;
                    if (hIcon != IntPtr.Zero)
                    {
                        if (hIcon == hIconLarge && hIconSmall != IntPtr.Zero)
                            DestroyIcon(hIconSmall);
                        else if (hIcon == hIconSmall && hIconLarge != IntPtr.Zero)
                            DestroyIcon(hIconLarge);
                        return hIcon;
                    }
                }
                
                Log($"Failed to extract icon from '{iconPath}' with index {iconIndex}");
            }
            catch (Exception ex) 
            { 
                Log($"Error extracting icon from '{iconLocation}': {ex.Message}");
            }
            return IntPtr.Zero;
        }

        // Конвертируем иконку в BitmapSource
        private static ImageSource ConvertIconToBitmapSource(IntPtr hIcon, int size)
        {
            try
            {
                var tempBitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                if (tempBitmapSource.PixelWidth == size && tempBitmapSource.PixelHeight == size)
                {
                    return tempBitmapSource;
                }

                return new TransformedBitmap(
                    tempBitmapSource,
                    new ScaleTransform((double)size / tempBitmapSource.PixelWidth, 
                                     (double)size / tempBitmapSource.PixelHeight));
            }
            catch (Exception ex)
            {
                Log($"Error converting icon to BitmapSource: {ex.Message}");
                return null;
            }
        }

        public static ImageSource GetIconForShortcut(string shortcutPath)
        {
            return GetIconForShortcut(shortcutPath, IconSize);
        }

        public static ImageSource GetIconForShortcut(string shortcutPath, int iconSize)
        {
            try
            {
                // Получаем путь к иконке из ярлыка
                string iconPath = GetIconPathFromShortcut(shortcutPath);
                // Если это .url — обрабатываем отдельно
                if (shortcutPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                {
                    var urlIcon = GetIconPathFromUrlFile(shortcutPath);
                    if (!string.IsNullOrEmpty(urlIcon))
                    {
                        var hIcon = ExtractHdIconFromFile(urlIcon, 0, iconSize);
                        if (hIcon != IntPtr.Zero)
                        {
                            var bitmap = ConvertIconToBitmapSource(hIcon, iconSize);
                            DestroyIcon(hIcon);
                            return bitmap;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(iconPath))
                {
                    int defaultIndex = 0;
                    bool hasComma = iconPath.LastIndexOf(',') > 0;
                    
                    if (!hasComma)
                    {
                        // Получаем индекс иконки только если его нет в строке
                        SHFILEINFO shinfo = new SHFILEINFO();
                        IntPtr res = SHGetFileInfo(shortcutPath, 0, out shinfo,
                            (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_ICONLOCATION | SHGFI_LARGEICON);
                        if (shinfo.hIcon != IntPtr.Zero) DestroyIcon(shinfo.hIcon);
                        defaultIndex = shinfo.iIcon;
                    }

                    var hdIcon = ExtractHdIconFromFile(iconPath, defaultIndex, iconSize);
                    if (hdIcon != IntPtr.Zero)
                    {
                        var bitmapSource = ConvertIconToBitmapSource(hdIcon, iconSize);
                        DestroyIcon(hdIcon);
                        return bitmapSource;
                    }
                }

                // Агрессивный fallback: получаем целевой файл
                try
                {
                    Type shellLinkType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellLinkType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellLinkType);
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);
                        string targetPath = shortcut.TargetPath;
                        shortcut = null;
                        shell = null;
                        
                        if (!string.IsNullOrWhiteSpace(targetPath) && targetPath.Length > 0 && File.Exists(targetPath))
                        {
                            var hdIcon = ExtractHdIconFromFile(targetPath + ",0", 0, iconSize);
                            if (hdIcon != IntPtr.Zero)
                            {
                                var bitmapSource = ConvertIconToBitmapSource(hdIcon, iconSize);
                                DestroyIcon(hdIcon);
                                return bitmapSource;
                            }
                        }
                        else
                        {
                            Log($"Fallback failed: target path '{targetPath}' is empty or does not exist for shortcut '{shortcutPath}'");
                        }
                    }
                    else
                    {
                        Log($"Fallback failed: WScript.Shell type not found for shortcut '{shortcutPath}'");
                    }
                }
                catch (Exception ex) 
                { 
                    Log($"Fallback icon extraction failed for '{shortcutPath}': {ex.Message}");
                }
                
                Log($"All icon extraction methods failed for shortcut '{shortcutPath}'");
                return null;
            }
            catch (Exception ex)
            {
                Log($"Critical error in GetIconForShortcut for '{shortcutPath}': {ex.Message}");
                return null;
            }
        }

        /// Загружает все ярлыки из папки shortcuts
        public static ShortcutInfo[] LoadShortcuts(bool isHorizontalMode)
        {
            try
            {
                var shortcutsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ShortcutsDirectory);

                if (!Directory.Exists(shortcutsDir))
                {
                    Directory.CreateDirectory(shortcutsDir);
                    return Array.Empty<ShortcutInfo>();
                }

                var iconSize = isHorizontalMode ? HorizontalIconSize : IconSize;

                return Directory.GetFiles(shortcutsDir)
                    .Where(f => f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                    .Select(path => new ShortcutInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(path),
                        Icon = GetIconForShortcut(path, iconSize),
                        FullPath = path
                    })
                    .ToArray();
            }
            catch (Exception ex)
            {
                Log($"Error loading shortcuts: {ex.Message}");
                return Array.Empty<ShortcutInfo>();
            }
        }

        private static string GetIconPathFromUrlFile(string urlPath)
        {
            try
            {
                var lines = File.ReadAllLines(urlPath);

                string iconFile = null;
                int iconIndex = 0;

                foreach (var line in lines)
                {
                    if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                        iconFile = line.Substring("IconFile=".Length);

                    if (line.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(line.Substring("IconIndex=".Length), out iconIndex);
                }

                if (!string.IsNullOrWhiteSpace(iconFile))
                    return $"{iconFile},{iconIndex}";
            }
            catch (Exception ex)
            {
                Log($"Error reading .url icon: {ex.Message}");
            }

            return null;
        }

        /// Проверяет наличие ярлыков
        public static bool HasShortcuts()
        {
            try
            {
                var shortcutsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ShortcutsDirectory);
                return Directory.Exists(shortcutsDir) && Directory.GetFiles(shortcutsDir)
                                                            .Any(f => f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                                                                    f.EndsWith(".url", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }
    }

    public class ShortcutInfo
    {
        public string Name { get; set; }
        public ImageSource Icon { get; set; }
        public string FullPath { get; set; }
    }
} 