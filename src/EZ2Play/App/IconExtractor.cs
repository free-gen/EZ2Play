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
        private const string ShortcutsDirectory = "shortcuts";
        private const string CoversDirectory = "covers";
        private const string BackgroundsDirectory = "backgrounds";
        private const int IconSize = 256;

        private const int SHIL_JUMBO = 0x4;

        private const uint SHGFI_SYSICONINDEX = 0x4000;
        private const uint SHGFI_ICONLOCATION = 0x1000;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_ICON = 0x100;

        [DllImport("shell32.dll")]
        private static extern int SHGetImageList(int iImageList, ref Guid riid, out IntPtr ppv);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("comctl32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, uint flags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHDefExtractIconW(string pszIconFile, int iIndex, uint uFlags, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIconSize);

        [DllImport("shell32.dll")]
        private static extern bool ExtractIconEx(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

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

        // Get the largest available icon from the system image list.
        private static ImageSource GetJumboIcon(string path)
        {
            SHFILEINFO shinfo = new SHFILEINFO();

            if (SHGetFileInfo(path, 0, out shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_SYSICONINDEX) == IntPtr.Zero)
                return null;

            Guid iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
            IntPtr imageListPtr = IntPtr.Zero;
            IntPtr hIcon = IntPtr.Zero;

            try
            {
                int hr = SHGetImageList(SHIL_JUMBO, ref iidImageList, out imageListPtr);

                if (hr != 0 || imageListPtr == IntPtr.Zero)
                    return null;

                hIcon = ImageList_GetIcon(imageListPtr, shinfo.iIcon, 0);

                return hIcon != IntPtr.Zero
                    ? ConvertIconToBitmapSource(hIcon)
                    : null;
            }

            catch
            {
                return null;
            }

            finally
            {
                if (hIcon != IntPtr.Zero)
                    DestroyIcon(hIcon);

                if (imageListPtr != IntPtr.Zero)
                {
                    try
                    {
                        Marshal.Release(imageListPtr);
                    }

                    catch
                    {
                    }
                }
            }
        }

        // Read the icon location from a .lnk shortcut.
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

                    if (!string.IsNullOrWhiteSpace(iconLocation))
                        return iconLocation;
                    else if (!string.IsNullOrWhiteSpace(targetPath))
                        return targetPath + ",0";
                }
            }

            catch
            {
            }

            return null;
        }

        // Extract an icon from a file using its path and icon index.
        private static IntPtr ExtractIconFromFile(string iconLocation, int defaultIconIndex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(iconLocation) || iconLocation.StartsWith(","))
                    return IntPtr.Zero;

                string iconPath = iconLocation;
                int iconIndex = defaultIconIndex;

                int commaIndex = iconLocation.LastIndexOf(',');

                if (commaIndex > 0)
                {
                    iconPath = iconLocation.Substring(0, commaIndex);

                    if (int.TryParse(iconLocation.Substring(commaIndex + 1), out int parsedIndex))
                        iconIndex = parsedIndex;
                }

                iconPath = iconPath.Trim().Trim('"');

                if (!File.Exists(iconPath))
                    return IntPtr.Zero;

                IntPtr hIconLarge, hIconSmall;
                IntPtr result = SHDefExtractIconW(iconPath, iconIndex, 0, out hIconLarge, out hIconSmall, IconSize);

                if (result == IntPtr.Zero && hIconLarge != IntPtr.Zero)
                {
                    if (hIconSmall != IntPtr.Zero) DestroyIcon(hIconSmall);

                    return hIconLarge;
                }

                // Fall back to ExtractIconEx if SHDefExtractIconW fails.
                if (ExtractIconEx(iconPath, iconIndex, out hIconLarge, out hIconSmall, 1) && hIconLarge != IntPtr.Zero)
                {
                    if (hIconSmall != IntPtr.Zero) DestroyIcon(hIconSmall);

                    return hIconLarge;
                }
            }

            catch
            {
            }

            return IntPtr.Zero;
        }

        private static ImageSource ConvertIconToBitmapSource(IntPtr hIcon)
        {
            try
            {
                return Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }

            catch
            {
                return null;
            }
        }

        // Load a custom cover matching the shortcut name.
        public static ImageSource GetCustomCover(string shortcutPath)
        {
            try
            {
                string shortcutsDir = Path.GetDirectoryName(shortcutPath);
                string coversDir = Path.Combine(shortcutsDir, CoversDirectory);

                if (!Directory.Exists(coversDir))
                    return null;

                string name = Path.GetFileNameWithoutExtension(shortcutPath);
                string[] extensions = { ".png", ".jpg", ".jpeg" };

                foreach (string extension in extensions)
                {
                    string coverPath = Path.Combine(coversDir, name + extension);

                    if (!File.Exists(coverPath))
                        continue;

                    var bitmap = new BitmapImage();

                    using (var stream = new FileStream(coverPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 320;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                    }

                    bitmap.Freeze();

                    return bitmap;
                }
            }

            catch
            {
            }

            return null;
        }

        // Find a custom background matching the shortcut name.
        public static string GetCustomBackgroundPath(string shortcutPath)
        {
            try
            {
                string shortcutsDir = Path.GetDirectoryName(shortcutPath);
                string backgroundsDir = Path.Combine(shortcutsDir, BackgroundsDirectory);

                if (!Directory.Exists(backgroundsDir))
                    return null;

                string name = Path.GetFileNameWithoutExtension(shortcutPath);
                string[] extensions = { ".png", ".jpg", ".jpeg" };

                foreach (string extension in extensions)
                {
                    string backgroundPath = Path.Combine(backgroundsDir, name + extension);

                    if (File.Exists(backgroundPath))
                        return backgroundPath;
                }
            }

            catch
            {
            }

            return null;
        }

        // Resolve the best available icon for a shortcut.
        public static ImageSource GetIconForShortcut(string shortcutPath)
        {
            try
            {
                if (shortcutPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                {
                    var urlIcon = GetIconPathFromUrlFile(shortcutPath);

                    if (!string.IsNullOrEmpty(urlIcon))
                    {
                        var hIcon = ExtractIconFromFile(urlIcon, 0);

                        if (hIcon != IntPtr.Zero)
                        {
                            var bitmap = ConvertIconToBitmapSource(hIcon);
                            DestroyIcon(hIcon);

                            return bitmap;
                        }
                    }
                }

                string iconPath = GetIconPathFromShortcut(shortcutPath);

                if (!string.IsNullOrEmpty(iconPath))
                {
                    int defaultIndex = 0;

                    if (iconPath.LastIndexOf(',') <= 0)
                    {
                        SHFILEINFO shinfo = new SHFILEINFO();

                        SHGetFileInfo(
                            shortcutPath,
                            0,
                            out shinfo,
                            (uint)Marshal.SizeOf(shinfo),
                            SHGFI_ICON | SHGFI_ICONLOCATION | SHGFI_LARGEICON);

                        if (shinfo.hIcon != IntPtr.Zero)
                            DestroyIcon(shinfo.hIcon);

                        defaultIndex = shinfo.iIcon;
                    }

                    var hIcon = ExtractIconFromFile(iconPath, defaultIndex);

                    if (hIcon != IntPtr.Zero)
                    {
                        var bitmapSource = ConvertIconToBitmapSource(hIcon);
                        DestroyIcon(hIcon);

                        return bitmapSource;
                    }
                }

                // Fall back to the shortcut target file.
                try
                {
                    Type shellLinkType = Type.GetTypeFromProgID("WScript.Shell");

                    if (shellLinkType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellLinkType);
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);
                        string targetPath = shortcut.TargetPath;

                        if (!string.IsNullOrWhiteSpace(targetPath) && File.Exists(targetPath))
                        {
                            var hIcon = ExtractIconFromFile(targetPath + ",0", 0);

                            if (hIcon != IntPtr.Zero)
                            {
                                var bitmapSource = ConvertIconToBitmapSource(hIcon);
                                DestroyIcon(hIcon);

                                return bitmapSource;
                            }
                        }
                    }
                }

                catch
                {
                }

                // Final fallback to the system jumbo icon.
                var jumboIcon = GetJumboIcon(shortcutPath);

                if (jumboIcon != null)
                    return jumboIcon;

                return null;
            }

            catch
            {
                return null;
            }
        }

        // Detect the launcher or source associated with a shortcut.
        private static string GetSourceTypeFromShortcut(string shortcutPath)
        {
            try
            {
                // Prefer an explicit source stored in the .lnk description.
                if (shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    string comment = GetCommentFromShortcut(shortcutPath);

                    if (!string.IsNullOrWhiteSpace(comment))
                        return comment;
                }

                if (shortcutPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                {
                    var lines = File.ReadAllLines(shortcutPath);

                    foreach (var line in lines)
                    {
                        if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                        {
                            string url = line.Substring(4).ToLowerInvariant();

                            if (url.Contains("steam://") || url.Contains("steampowered.com")) return "Steam";
                            if (url.Contains("epicgames.com") || url.Contains("com.epicgames")) return "Epic Games";
                            if (url.Contains("vkplay://") || url.Contains("vkplay.ru")) return "VK Play";
                            if (url.Contains("rockstargames.com") || url.Contains("rockstar")) return "Rockstar";
                            if (url.Contains("gog.com")) return "GOG";
                            if (url.Contains("xbox.com") || url.Contains("microsoft.com/xbox")) return "Xbox";
                        }
                    }
                }

                try
                {
                    Type shellLinkType = Type.GetTypeFromProgID("WScript.Shell");

                    if (shellLinkType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellLinkType);
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);

                        string target = (shortcut.TargetPath ?? " ").Trim();

                        // An empty target path indicates a Microsoft Store shortcut.
                        if (string.IsNullOrWhiteSpace(target))
                            return "Microsoft Store";

                        string t = target.ToLowerInvariant();

                        if (t.Contains("rockstar games\\launcher")) return "Rockstar";
                        if (t.Contains("vkplay") || t.Contains("vk play")) return "VK Play";
                        if (t.Contains("ubisoft") || t.Contains("uplay")) return "Ubisoft";
                        if (t.Contains("gog galaxy")) return "GOG";
                        if (t.Contains("xbox") || t.Contains("microsoft.xbox")) return "Xbox";
                    }
                }

                catch
                {
                }

                return "Portable";
            }

            catch
            {
                return "Portable";
            }
        }

        private static string GetCommentFromShortcut(string shortcutPath)
        {
            try
            {
                Type shellLinkType = Type.GetTypeFromProgID("WScript.Shell");

                if (shellLinkType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellLinkType);
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);

                    string comment = shortcut.Description;

                    if (!string.IsNullOrWhiteSpace(comment))
                        return comment.Trim();
                }
            }

            catch
            {
            }

            return null;
        }

        // Load all supported shortcuts from the shortcuts directory.
        public static ShortcutInfo[] LoadShortcuts()
        {
            try
            {
                var shortcutsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ShortcutsDirectory);

                if (!Directory.Exists(shortcutsDir))
                {
                    Directory.CreateDirectory(shortcutsDir);
                    return Array.Empty<ShortcutInfo>();
                }

                return Directory.GetFiles(shortcutsDir)
                    .Where(f => f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                    .Select(path => new ShortcutInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(path),
                        Icon = GetCustomCover(path) ?? GetIconForShortcut(path),
                        FullPath = path,
                        SourceType = GetSourceTypeFromShortcut(path)
                    })
                    .ToArray();
            }

            catch
            {
                return Array.Empty<ShortcutInfo>();
            }
        }

        // Read the icon path from a .url file while preserving local encoding.
        private static string GetIconPathFromUrlFile(string urlPath)
        {
            try
            {
                var lines = File.ReadAllLines(urlPath, System.Text.Encoding.Default);

                string iconFile = null;
                int iconIndex = 0;
                bool insideMainSection = false;

                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();

                    if (line.StartsWith("[InternetShortcut]", StringComparison.OrdinalIgnoreCase))
                    {
                        insideMainSection = true;
                        continue;
                    }

                    if (line.StartsWith("[") && !line.StartsWith("[InternetShortcut]", StringComparison.OrdinalIgnoreCase))
                    {
                        insideMainSection = false;
                    }

                    if (!insideMainSection)
                        continue;

                    if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                        iconFile = line.Substring("IconFile=".Length).Trim().Trim('"');

                    if (line.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(line.Substring("IconIndex=".Length).Trim(), out iconIndex);
                }

                if (!string.IsNullOrWhiteSpace(iconFile) && File.Exists(iconFile))
                    return $"{iconFile},{iconIndex}";
            }

            catch
            {
            }

            return null;
        }

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
        public string SourceType { get; set; }

        public string DisplayName => Name?.Replace(';', ':');
    }
}