
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace EZ2Play.App
{
    /// <summary>
    /// Provides Windows-specific environment operations
    /// used by the application.
    /// </summary>
    /// <remarks>
    /// Handles user account information, Xbox Game Bar
    /// detection, foreground window checks, cursor
    /// visibility, and display sleep prevention.
    ///
    /// SystemProvider remains the public facade for
    /// these operations.
    ///
    /// All Windows API calls, exception handling,
    /// process timeouts, and original return values
    /// are preserved.
    /// </remarks>
    internal static class WindowsEnvironment
    {
        // Handle of the application's main window.
        private static IntPtr _mainWindowHandle;

        // Cache the Xbox Game Bar detection result.
        // The detection is performed on first access.
        private static readonly Lazy<bool> XboxGameBarInstalled =
            new Lazy<bool>(DetectXboxGameBarInstalled);

        // Windows execution state flags.
        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;

        /// <summary>
        /// Retrieves the current Windows user's avatar.
        /// </summary>
        /// <returns>
        /// A frozen BitmapImage containing the user avatar,
        /// or null when no supported image is available.
        /// </returns>
        /// <remarks>
        /// Reads the Image192 path from the Windows account
        /// picture registry entry associated with the
        /// current user's security identifier.
        ///
        /// Falls back to the standard Windows user picture
        /// when the registry image is unavailable.
        ///
        /// The bitmap is fully loaded and frozen before
        /// being returned.
        /// </remarks>
        internal static BitmapImage GetUserAvatar()
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

                // Use the standard Windows account picture
                // when the user's image is unavailable.
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    path = Path.Combine(
                        @"C:\ProgramData\Microsoft\User Account Pictures",
                        "user-192.png");
                }

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

        /// <summary>
        /// Determines whether Xbox Game Bar is installed.
        /// </summary>
        /// <returns>
        /// True when Xbox Game Bar was detected successfully;
        /// otherwise, false.
        /// </returns>
        /// <remarks>
        /// The detection result is cached using Lazy.
        /// Subsequent calls return the original result
        /// without starting another PowerShell process.
        /// </remarks>
        internal static bool IsXboxGameBarInstalled()
        {
            return XboxGameBarInstalled.Value;
        }

        /// <summary>
        /// Detects the Microsoft Xbox Gaming Overlay package
        /// using Windows PowerShell.
        /// </summary>
        /// <returns>
        /// True when the package is installed;
        /// otherwise, false.
        /// </returns>
        /// <remarks>
        /// Preserves the original PowerShell command,
        /// eight-second process timeout, exit code check,
        /// and process termination behavior.
        /// </remarks>
        private static bool DetectXboxGameBarInstalled()
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

                    if (!process.Start())
                        return false;

                    // Preserve the original eight-second timeout.
                    if (!process.WaitForExit(8000))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }

                        return false;
                    }

                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Stores the native handle of the application's
        /// main window for foreground checks.
        /// </summary>
        /// <param name="handle">
        /// Native handle of the main application window.
        /// </param>
        internal static void SetMainWindowHandle(IntPtr handle)
        {
            _mainWindowHandle = handle;
        }

        /// <summary>
        /// Checks whether the application's main window
        /// is the current Windows foreground window.
        /// </summary>
        /// <returns>
        /// True when the native foreground window handle
        /// matches the stored main window handle.
        /// </returns>
        internal static bool IsForeground()
        {
            return GetForegroundWindow() == _mainWindowHandle;
        }

        /// <summary>
        /// Hides the mouse cursor through the WPF
        /// application-wide cursor override.
        /// </summary>
        /// <remarks>
        /// Uses Mouse.OverrideCursor rather than
        /// the native Windows ShowCursor API.
        /// </remarks>
        internal static void HideCursor()
        {
            System.Windows.Input.Mouse.OverrideCursor =
                System.Windows.Input.Cursors.None;
        }

        /// <summary>
        /// Restores normal WPF cursor behavior by
        /// clearing the cursor override.
        /// </summary>
        internal static void ShowCursor()
        {
            System.Windows.Input.Mouse.OverrideCursor = null;
        }

        /// <summary>
        /// Controls whether Windows is allowed
        /// to turn off the display.
        /// </summary>
        /// <param name="blocked">
        /// True to require the display to remain active;
        /// false to clear that display requirement.
        /// </param>
        /// <remarks>
        /// Uses the original SetThreadExecutionState call
        /// and the same Windows execution state flags.
        ///
        /// The call is performed synchronously on the
        /// calling thread. No background thread or timer
        /// is introduced.
        /// </remarks>
        internal static void SetDisplaySleepBlocked(bool blocked)
        {
            SetThreadExecutionState(
                blocked
                    ? ES_CONTINUOUS | ES_DISPLAY_REQUIRED
                    : ES_CONTINUOUS);
        }

        /// <summary>
        /// Sets the execution state of the calling thread.
        /// </summary>
        [DllImport("kernel32.dll")]
        private static extern uint SetThreadExecutionState(uint esFlags);

        /// <summary>
        /// Retrieves the native handle of the current
        /// foreground window.
        /// </summary>
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}
