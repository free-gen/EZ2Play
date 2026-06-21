using System;
using System.IO;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace EZ2Play.App
{
    public static class SystemProvider
    {
        // --------------- Поля ---------------

        private static DispatcherTimer _clockTimer;
        private static IntPtr _mainWindowHandle;
        private const string AutorunShortcutName = "EZ2Play Helper.lnk";

        // --------------- Аватар пользователя ---------------

        public static BitmapImage GetUserAvatar()
        {
            try
            {
                string path = null;
                var sid = WindowsIdentity.GetCurrent()?.User?.Value;

                if (!string.IsNullOrEmpty(sid))
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(
                        $@"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\{sid}"))
                    {
                        path = key?.GetValue("Image192") as string;
                    }
                }

                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    path = Path.Combine(@"C:\ProgramData\Microsoft\User Account Pictures", "user-192.png");

                if (!File.Exists(path))
                    return null;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        // --------------- Системное время ---------------

        public static void StartClock(Action<string> onTimeChanged)
        {            
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => onTimeChanged?.Invoke(GetCurrentTime());
            _clockTimer.Start();
        }
        
        public static void StopClock()
        {
            _clockTimer?.Stop();
            _clockTimer = null;
        }
        
        public static string GetCurrentTime() => DateTime.Now.ToString("HH:mm");

        // --------------- Xbox Game Bar ---------------

        public static bool IsXboxGameBarInstalled()
        {
            try
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.FileName = "powershell.exe";
                    process.StartInfo.Arguments =
                        "-NoProfile -Command \"if(Get-AppxPackage Microsoft.XboxGamingOverlay){exit 0}else{exit 1}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        // --------------- Окно и курсор ---------------

        public static void SetMainWindowHandle(IntPtr handle)
        {
            _mainWindowHandle = handle;
        }

        public static bool IsForeground()
        {
            return GetForegroundWindow() == _mainWindowHandle;
        }

        public static void HideCursor()
        {
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.None;
        }

        public static void ShowCursor()
        {
            System.Windows.Input.Mouse.OverrideCursor = null;
        }

        // --------------- Автозапуск ---------------

        public static bool IsAutorunEnabled()
        {
            try
            {
                string startupFolder =
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Startup);

                string shortcutPath =
                    Path.Combine(
                        startupFolder,
                        AutorunShortcutName);

                return File.Exists(shortcutPath);
            }
            catch
            {
                return false;
            }
        }

        public static void EnableAutorun()
        {
            try
            {
                string helperPath = GetHelperExecutablePath();
                if (!File.Exists(helperPath))
                    return;

                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupFolder, AutorunShortcutName);
                string workDir = Path.GetDirectoryName(helperPath);

                string ps =
                    "$WshShell = New-Object -ComObject WScript.Shell; " +
                    "$Shortcut = $WshShell.CreateShortcut('" + shortcutPath.Replace("'", "''") + "'); " +
                    "$Shortcut.TargetPath = '" + helperPath.Replace("'", "''") + "'; " +
                    "$Shortcut.WorkingDirectory = '" + workDir.Replace("'", "''") + "'; " +
                    "$Shortcut.Arguments = ''; " +
                    "$Shortcut.Save();";

                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + ps + "\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });

                StartHelperProcess("");
            }
            catch { }
        }

        public static void DisableAutorun()
        {
            try
            {
                string startupFolder =
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Startup);

                string shortcutPath =
                    Path.Combine(
                        startupFolder,
                        AutorunShortcutName);

                if (File.Exists(shortcutPath))
                    File.Delete(shortcutPath);

                StopHelperProcess();
            }
            catch { }
        }

        // --------------- Аргументы автозапуска ---------------                

        public static void SetAutorunArguments(string args)
        {
            try
            {
                string helperPath = GetHelperExecutablePath();
                if (!File.Exists(helperPath))
                    return;

                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupFolder, AutorunShortcutName);
                string workDir = Path.GetDirectoryName(helperPath);

                string ps =
                    "$WshShell = New-Object -ComObject WScript.Shell; " +
                    "$Shortcut = $WshShell.CreateShortcut('" + shortcutPath.Replace("'", "''") + "'); " +
                    "$Shortcut.TargetPath = '" + helperPath.Replace("'", "''") + "'; " +
                    "$Shortcut.WorkingDirectory = '" + workDir.Replace("'", "''") + "'; " +
                    "$Shortcut.Arguments = '" + args.Replace("'", "''") + "'; " +
                    "$Shortcut.Save();";

                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + ps + "\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });

                // Перезапускаем хелпер
                StopHelperProcess();
                StartHelperProcess(args);
            }
            catch { }
        }

        public static string GetAutorunArguments()
        {
            try
            {
                string shortcutPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    AutorunShortcutName);

                if (!File.Exists(shortcutPath))
                    return "";

                string ps = 
                    "$WshShell = New-Object -ComObject WScript.Shell; " +
                    "$Shortcut = $WshShell.CreateShortcut('" + shortcutPath.Replace("'", "''") + "'); " +
                    "$Shortcut.Arguments;";

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + ps + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    return output;
                }
            }
            catch
            {
                return "";
            }
        }

        private static string GetHelperExecutablePath()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string helperPath = Path.Combine(appDir, "EZ2Play Helper.exe");
            return helperPath;
        }

        public static void StartHelperProcess(string arguments = "")
        {
            try
            {
                string helperPath = GetHelperExecutablePath();
                if (string.IsNullOrEmpty(helperPath) || !File.Exists(helperPath))
                    return;

                // Проверяем, не запущен ли уже
                if (IsHelperProcessRunning())
                    return;

                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = helperPath;
                process.StartInfo.WorkingDirectory = Path.GetDirectoryName(helperPath);
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
            }
            catch { }
        }

        public static void StopHelperProcess()
        {
            try
            {
                foreach (var process in System.Diagnostics.Process.GetProcessesByName("EZ2Play Helper"))
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static bool IsHelperProcessRunning()
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("EZ2Play Helper");
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        // --------------- Native imports ---------------

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}