using System;
using System.IO;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Globalization;
using System.Windows.Input;
using Windows.UI.ViewManagement.Core;

namespace EZ2Play.App
{
    public static class SystemProvider
    {
        private static DispatcherTimer _clockTimer;
        private static IntPtr _mainWindowHandle;
        private const string AutorunShortcutName = "EZ2Play Helper.lnk";

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

        public static bool IsXboxGameBarInstalled()
        {
            try
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.FileName = "powershell.exe";
                    process.StartInfo.Arguments = "-NoProfile -Command \"if(Get-AppxPackage Microsoft.XboxGamingOverlay){exit 0}else{exit 1}\"";
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

        public static bool IsAutorunEnabled()
        {
            try
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupFolder, AutorunShortcutName);

                return File.Exists(shortcutPath);
            }

            catch
            {
                return false;
            }
        }

        private static bool RunAutorunPowerShell(string script, string operation)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments =
                        "-NoProfile -ExecutionPolicy Bypass -Command " +
                        "\"$ErrorActionPreference='Stop'; " +
                        script +
                        "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        DebugLog.Write("Autorun", $"{operation}: PowerShell could not be started.");
                        return false;
                    }

                    string error = process.StandardError.ReadToEnd().Trim();

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        DebugLog.Write("Autorun", $"{operation} failed. ExitCode={process.ExitCode}. Error={error}");
                        return false;
                    }

                    return true;
                }
            }

            catch (Exception ex)
            {
                DebugLog.Error("Autorun", ex, $"{operation} failed.");
                return false;
            }
        }

        public static bool EnableAutorun()
        {
            try
            {
                string helperPath = GetHelperExecutablePath();

                if (!File.Exists(helperPath))
                {
                    DebugLog.Write("Autorun", "Helper executable not found.");
                    return false;
                }

                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupFolder, AutorunShortcutName);
                string workDir = Path.GetDirectoryName(helperPath);

                string ps =
                    "$WshShell = New-Object -ComObject WScript.Shell; " +
                    "$Shortcut = $WshShell.CreateShortcut('" +
                    shortcutPath.Replace("'", "''") +
                    "'); " +
                    "$Shortcut.TargetPath = '" +
                    helperPath.Replace("'", "''") +
                    "'; " +
                    "$Shortcut.WorkingDirectory = '" +
                    workDir.Replace("'", "''") +
                    "'; " +
                    "$Shortcut.Arguments = ''; " +
                    "$Shortcut.Save();";

                if (!RunAutorunPowerShell(ps, "Enable autorun"))
                    return false;

                if (!File.Exists(shortcutPath))
                {
                    DebugLog.Write("Autorun", "Startup shortcut was not created.");
                    return false;
                }

                if (!StartHelperProcess(""))
                {
                    DebugLog.Write("Autorun", "Helper process could not be started.");
                    return false;
                }

                DebugLog.Write("Autorun", "Autorun enabled.");

                return true;
            }

            catch (Exception ex)
            {
                DebugLog.Error("Autorun", ex, "Failed to enable autorun.");
                return false;
            }
        }

        public static bool DisableAutorun()
        {
            try
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupFolder, AutorunShortcutName);

                if (File.Exists(shortcutPath))
                    File.Delete(shortcutPath);

                if (File.Exists(shortcutPath))
                {
                    DebugLog.Write("Autorun", "Startup shortcut still exists after deletion.");
                    return false;
                }

                StopHelperProcess();

                DebugLog.Write("Autorun", "Autorun disabled.");

                return true;
            }

            catch (Exception ex)
            {
                DebugLog.Error("Autorun", ex, "Failed to disable autorun.");
                return false;
            }
        }

        public static bool SetAutorunArguments(string args)
        {
            try
            {
                string helperPath = GetHelperExecutablePath();

                if (!File.Exists(helperPath))
                {
                    DebugLog.Write("Autorun", "Cannot update arguments: Helper executable not found.");
                    return false;
                }

                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupFolder, AutorunShortcutName);

                if (!File.Exists(shortcutPath))
                {
                    DebugLog.Write("Autorun", "Cannot update arguments: startup shortcut does not exist.");
                    return false;
                }

                string workDir = Path.GetDirectoryName(helperPath);

                args = (args ?? string.Empty).Trim();

                string ps =
                    "$WshShell = New-Object -ComObject WScript.Shell; " +
                    "$Shortcut = $WshShell.CreateShortcut('" +
                    shortcutPath.Replace("'", "''") +
                    "'); " +
                    "$Shortcut.TargetPath = '" +
                    helperPath.Replace("'", "''") +
                    "'; " +
                    "$Shortcut.WorkingDirectory = '" +
                    workDir.Replace("'", "''") +
                    "'; " +
                    "$Shortcut.Arguments = '" +
                    args.Replace("'", "''") +
                    "'; " +
                    "$Shortcut.Save();";

                if (!RunAutorunPowerShell(ps, "Update autorun arguments"))
                    return false;

                if (!File.Exists(shortcutPath))
                {
                    DebugLog.Write("Autorun", "Startup shortcut disappeared after arguments update.");
                    return false;
                }

                StopHelperProcess();
                StartHelperProcess(args);

                DebugLog.Write("Autorun", $"Arguments updated: [{args}]");

                return true;
            }

            catch (Exception ex)
            {
                DebugLog.Error("Autorun", ex, "Failed to update autorun arguments.");
                return false;
            }
        }

        public static string GetAutorunArguments()
        {
            object shell = null;
            object shortcut = null;

            try
            {
                string shortcutPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    AutorunShortcutName);

                if (!File.Exists(shortcutPath))
                    return string.Empty;

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");

                if (shellType == null)
                    return string.Empty;

                shell = Activator.CreateInstance(shellType);
                dynamic dynamicShell = shell;

                shortcut = dynamicShell.CreateShortcut(shortcutPath);
                dynamic dynamicShortcut = shortcut;

                string arguments = dynamicShortcut.Arguments;

                return arguments?.Trim() ?? string.Empty;
            }

            catch
            {
                return string.Empty;
            }

            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut))
                    Marshal.FinalReleaseComObject(shortcut);

                if (shell != null && Marshal.IsComObject(shell))
                    Marshal.FinalReleaseComObject(shell);
            }
        }

        private static string GetHelperExecutablePath()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string helperPath = Path.Combine(appDir, "EZ2Play Helper.exe");

            return helperPath;
        }

        public static bool StartHelperProcess(string arguments = "")
        {
            try
            {
                string helperPath = GetHelperExecutablePath();

                if (string.IsNullOrEmpty(helperPath) || !File.Exists(helperPath))
                    return false;

                // Do not start another helper instance if one is already running.
                if (IsHelperProcessRunning())
                    return true;

                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.FileName = helperPath;
                    process.StartInfo.WorkingDirectory = Path.GetDirectoryName(helperPath);
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    if (!process.Start())
                        return false;

                    return !process.HasExited;
                }
            }

            catch (Exception ex)
            {
                DebugLog.Error("Helper", ex, "Failed to start Helper.");
                return false;
            }
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

                    catch
                    {
                    }
                }
            }

            catch
            {
            }
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

        private const string FpsMonitorPath = @"C:\Program Files (x86)\FPS Monitor\FPSMonitor.exe";

        public static bool IsFpsMonitorInstalled()
        {
            return File.Exists(FpsMonitorPath);
        }

        public static bool IsFpsMonitorRunning()
        {
            try
            {
                return System.Diagnostics.Process.GetProcessesByName("FPSMonitor").Length > 0;
            }

            catch
            {
                return false;
            }
        }

        public static bool StartFpsMonitor()
        {
            try
            {
                if (IsFpsMonitorRunning())
                {
                    DebugLog.Write("FPS Monitor", "FPS Monitor is already running.");
                    return true;
                }

                if (!File.Exists(FpsMonitorPath))
                {
                    DebugLog.Write("FPS Monitor", "FPS Monitor executable not found.");
                    return false;
                }

                var process = System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = FpsMonitorPath,
                        UseShellExecute = true
                    });

                if (process == null)
                {
                    DebugLog.Write("FPS Monitor", "FPS Monitor process could not be started.");
                    return false;
                }

                DebugLog.Write("FPS Monitor", "FPS Monitor started.");

                return true;
            }

            catch (Exception ex)
            {
                DebugLog.Error("FPS Monitor", ex, "Failed to start FPS Monitor.");
                return false;
            }
        }

        public static bool StopFpsMonitor()
        {
            try
            {
                if (!IsFpsMonitorRunning())
                    return true;

                using (var process = System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "taskkill.exe",
                        Arguments = "/F /IM FPSMonitor.exe",
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    }))
                {
                    if (process == null)
                    {
                        DebugLog.Write("FPS Monitor", "taskkill process could not be started.");
                        return false;
                    }

                    process.WaitForExit();

                    // taskkill may exit before FPSMonitor disappears from the process list.
                    const int maxAttempts = 20;
                    const int delayMs = 100;

                    for (int attempt = 0; attempt < maxAttempts; attempt++)
                    {
                        if (!IsFpsMonitorRunning())
                        {
                            DebugLog.Write("FPS Monitor", $"FPS Monitor stopped after {attempt * delayMs} ms.");
                            return true;
                        }

                        System.Threading.Thread.Sleep(delayMs);
                    }

                    DebugLog.Write("FPS Monitor", $"FPS Monitor is still running after taskkill. ExitCode={process.ExitCode}");

                    return false;
                }
            }

            catch (Exception ex)
            {
                DebugLog.Error("FPS Monitor", ex, "Failed to stop FPS Monitor.");
                return false;
            }
        }

        public static CultureInfo ForceEnglishInputLanguage()
        {
            try
            {
                var manager = InputLanguageManager.Current;
                var previousLanguage = manager.CurrentInputLanguage;

                if (previousLanguage != null &&
                    string.Equals(previousLanguage.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase))
                {
                    return previousLanguage;
                }

                foreach (CultureInfo language in manager.AvailableInputLanguages)
                {
                    if (string.Equals(language.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase))
                    {
                        manager.CurrentInputLanguage = language;
                        return previousLanguage;
                    }
                }

                return previousLanguage;
            }

            catch
            {
                return null;
            }
        }

        public static void RestoreInputLanguage(CultureInfo language)
        {
            if (language == null) return;

            try
            {
                InputLanguageManager.Current.CurrentInputLanguage = language;
            }

            catch
            {
            }
        }

        public static void WarmUpSystemKeyboard()
        {
            try
            {
                CoreInputView.GetForCurrentView()?.TryHide();
            }

            catch
            {
            }
        }

        public static void ShowGamepadKeyboard()
        {
            try
            {
                var inputView = CoreInputView.GetForCurrentView();

                if (inputView == null) return;

                var gamepadKeyboard = CoreInputViewKind.Gamepad;

                if (inputView.IsKindSupported(gamepadKeyboard))
                    inputView.TryShow(gamepadKeyboard);
            }

            catch
            {
            }
        }

        public static void HideSystemKeyboard()
        {
            try
            {
                CoreInputView.GetForCurrentView()?.TryHide();
            }

            catch
            {
            }
        }

        // Prevent Windows from turning off the display while EZ2Play is running.
        public static void SetDisplaySleepBlocked(bool blocked)
        {
            SetThreadExecutionState(blocked ? ES_CONTINUOUS | ES_DISPLAY_REQUIRED : ES_CONTINUOUS);
        }

        [DllImport("kernel32.dll")]
        private static extern uint SetThreadExecutionState(uint esFlags);

        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}