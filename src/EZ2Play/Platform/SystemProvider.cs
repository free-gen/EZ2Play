
using System;
using System.Globalization;
using System.Windows.Media.Imaging;
namespace EZ2Play.App
{
    /// <summary>
    /// Provides access to Windows-specific functionality
    /// required by the application.
    /// </summary>
    /// <remarks>
    /// Centralizes access to system information, user account
    /// resources, input services, process management,
    /// autorun configuration, and display power management.
    ///
    /// The public methods are used by other application
    /// components and must retain their existing behavior.
    /// </remarks>
    public static class SystemProvider
    {
        /// <summary>
        /// Loads the current Windows user's account picture.
        /// </summary>
        /// <remarks>
        /// First attempts to locate the user's 192-pixel
        /// account picture through the Windows registry.
        ///
        /// If no account picture is available, the default
        /// Windows user image is used.
        ///
        /// The returned bitmap is fully loaded and frozen.
        /// </remarks>
        
        public static BitmapImage GetUserAvatar()
        {
            return WindowsEnvironment.GetUserAvatar();
        }

        /// <summary>
        /// Starts updating the application clock
        /// at one-second intervals.
        /// </summary>
        /// <param name="onTimeChanged">
        /// Callback receiving the current formatted time.
        /// </param>
        public static void StartClock(Action<string> onTimeChanged)
        {
            SystemClock.StartClock(onTimeChanged);
        }

        /// <summary>
        /// Stops the currently registered system clock timer.
        /// </summary>
        public static void StopClock()
        {
            SystemClock.StopClock();
        }

        /// <summary>
        /// Returns the current local time in 24-hour format.
        /// </summary>
        public static string GetCurrentTime()
        {
            return SystemClock.GetCurrentTime();
        }

        /// <summary>
        /// Indicates whether Xbox Game Bar is installed.
        /// </summary>
        /// <remarks>
        /// The installation check is performed lazily
        /// and its result is cached.
        /// </remarks>
        public static bool IsXboxGameBarInstalled()
        {
            return WindowsEnvironment.IsXboxGameBarInstalled();
        }

        /// <summary>
        /// Registers the native handle of the main window
        /// for foreground window detection.
        /// </summary>
        public static void SetMainWindowHandle(IntPtr handle)
        {
            WindowsEnvironment.SetMainWindowHandle(handle);
        }

        /// <summary>
        /// Checks whether the main application window
        /// is currently the foreground window.
        /// </summary>
        public static bool IsForeground()
        {
            return WindowsEnvironment.IsForeground();
        }

        /// <summary>
        /// Hides the mouse cursor through WPF.
        /// </summary>
        public static void HideCursor()
        {
            WindowsEnvironment.HideCursor();
        }

        /// <summary>
        /// Restores the default WPF mouse cursor behavior.
        /// </summary>
        public static void ShowCursor()
        {
            WindowsEnvironment.ShowCursor();
        }
        
        /// <summary>
        /// Checks whether the Helper startup shortcut exists.
        /// </summary>
        public static bool IsAutorunEnabled()
        {
            return HelperIntegration.IsAutorunEnabled();
        }

        /// <summary>
        /// Enables Helper autorun and starts the Helper process.
        /// </summary>
        public static bool EnableAutorun()
        {
            return HelperIntegration.EnableAutorun();
        }

        /// <summary>
        /// Removes Helper autorun and stops the Helper process.
        /// </summary>
        public static bool DisableAutorun()
        {
            return HelperIntegration.DisableAutorun();
        }

        /// <summary>
        /// Updates the command-line arguments stored in the
        /// Helper startup shortcut.
        /// </summary>
        public static bool SetAutorunArguments(string args)
        {
            return HelperIntegration.SetAutorunArguments(args);
        }

        /// <summary>
        /// Reads the arguments from the Helper startup shortcut.
        /// </summary>
        public static string GetAutorunArguments()
        {
            return HelperIntegration.GetAutorunArguments();
        }

        /// <summary>
        /// Starts the EZ2Play Helper process
        /// with optional command-line arguments.
        /// </summary>
        /// <remarks>
        /// If any Helper process is already running,
        /// another instance is not started.
        /// </remarks> 
        public static bool StartHelperProcess(string arguments = "")
        {
            return HelperIntegration.StartHelperProcess(arguments);
        }

        /// <summary>
        /// Terminates running EZ2Play Helper processes.
        /// </summary>
        /// <remarks>
        /// Each process is given up to three seconds
        /// to exit after receiving the termination request.
        /// </remarks>      
        public static void StopHelperProcess()
        {
            HelperIntegration.StopHelperProcess();
        }

        /// <summary>
        /// Checks whether an EZ2Play Helper process
        /// is currently running.
        /// </summary>  
        public static bool IsHelperProcessRunning()
        {
            return HelperIntegration.IsHelperProcessRunning();
        }

        /// <summary>
        /// Checks whether FPS Monitor is installed.
        /// </summary>
        public static bool IsFpsMonitorInstalled()
        {
            return FpsMonitorIntegration.IsFpsMonitorInstalled();
        }

        /// <summary>
        /// Checks whether FPS Monitor is running.
        /// </summary>
        public static bool IsFpsMonitorRunning()
        {
            return FpsMonitorIntegration.IsFpsMonitorRunning();
        }

        /// <summary>
        /// Starts FPS Monitor unless it is already running.
        /// </summary>
        public static bool StartFpsMonitor()
        {
            return FpsMonitorIntegration.StartFpsMonitor();
        }

        /// <summary>
        /// Stops FPS Monitor using the original taskkill procedure.
        /// </summary>
        public static bool StopFpsMonitor()
        {
            return FpsMonitorIntegration.StopFpsMonitor();
        }

        /// <summary>
        /// Attempts to switch the active Windows input language
        /// to an available English keyboard layout.
        /// </summary>
        /// <returns>
        /// The previously active input language, or null
        /// if an error occurs.
        /// </returns>
        /// <remarks>
        /// The previous language is returned even when
        /// no English input language is available.
        /// </remarks> 
        public static CultureInfo ForceEnglishInputLanguage()
        {
            return SystemKeyboard.ForceEnglishInputLanguage();
        }

        /// <summary>
        /// Restores the previously active Windows input language.
        /// </summary>
        public static void RestoreInputLanguage(CultureInfo language)
        {
            SystemKeyboard.RestoreInputLanguage(language);
        }

        /// <summary>
        /// Performs the original system keyboard warm-up
        /// operation through CoreInputView.
        /// </summary>
        public static void WarmUpSystemKeyboard()
        {
            SystemKeyboard.WarmUpSystemKeyboard();
        }

        /// <summary>
        /// Requests the Windows gamepad keyboard.
        /// </summary>
        /// <remarks>
        /// Attempts to display CoreInputViewKind.Gamepad first.
        ///
        /// If that request fails or does not succeed,
        /// the standard Windows keyboard is requested.
        ///
        /// Failures are written to DebugLog.
        /// </remarks>
        public static void ShowGamepadKeyboard()
        {
            SystemKeyboard.ShowGamepadKeyboard();
        }

        /// <summary>
        /// Requests the Windows system keyboard to close.
        /// </summary>
        public static void HideSystemKeyboard()
        {
            SystemKeyboard.HideSystemKeyboard();
        }

        /// <summary>
        /// Prevents Windows from turning off the display
        /// while EZ2Play is running.
        /// </summary>
        /// <param name="blocked">
        /// True to keep the display active;
        /// false to restore the continuous execution state.
        /// </param>
        public static void SetDisplaySleepBlocked(bool blocked)
        {
            WindowsEnvironment.SetDisplaySleepBlocked(blocked);
        }
    }
}
