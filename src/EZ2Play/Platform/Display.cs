
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace EZ2Play.App
{
    /// <summary>
    /// Manages Windows display detection, switching,
    /// and application window layout updates.
    /// </summary>
    /// <remarks>
    /// Supports switching between internal and external
    /// displays through the Windows DisplaySwitch utility.
    ///
    /// Also handles display configuration and DPI changes,
    /// including refreshing the main window layout.
    /// </remarks>
    public class Display
    {
        // Application window and optional sound manager.
        private readonly FrameworkElement _window;
        private readonly Sound _sound;

        // Display configuration and HotSwap state.
        private bool _wasLaunchedWithHotswap;
        private bool _hasMultipleDisplays;
        private bool _isExternalDisplay;

        private int _currentDisplayIndex;

        private List<string> _displayNames = new List<string>();

        // Prevent excessively frequent window layout refreshes.
        private DateTime _lastLayoutRefresh = DateTime.MinValue;
        private const int LayoutRefreshDelayMs = 500;

        /// <summary>
        /// Raised after switching the active display.
        /// Provides the name of the newly selected display.
        /// </summary>
        public event Action<string> OnDisplayChanged;

        public bool IsExternalDisplay => _isExternalDisplay;
        public bool HasMultipleDisplays => _hasMultipleDisplays;

        /// <summary>
        /// Represents a native Windows rectangle.
        /// Used to read the suggested window bounds
        /// supplied with WM_DPICHANGED.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// Updates the native window position and dimensions.
        /// </summary>
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        // Preserve the current Z-order and activation state.
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        public Display(
            FrameworkElement window,
            bool wasLaunchedWithHotswap = false,
            Sound audioManager = null)
        {
            _window = window;
            _wasLaunchedWithHotswap = wasLaunchedWithHotswap;
            _sound = audioManager;

            // Delay the initial HotSwap operation until
            // the WPF Loaded dispatcher priority.
            if (_wasLaunchedWithHotswap)
            {
                window.Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        TryInitialHotSwap();
                    }),
                    DispatcherPriority.Loaded);
            }

            CheckMultipleDisplays();
            SynchronizeDisplayMode();

            SystemEvents.DisplaySettingsChanged +=
                OnDisplaySettingsChanged;
        }

        /// <summary>
        /// Switches to the external display when EZ2Play
        /// was launched in HotSwap mode.
        /// </summary>
        /// <remarks>
        /// Only attempts the switch when multiple monitors
        /// have been detected.
        /// </remarks>
        private void TryInitialHotSwap()
        {
            if (!_hasMultipleDisplays)
                return;

            try
            {
                _sound?.PlayBackSound();

                _isExternalDisplay = true;
                _currentDisplayIndex = 1;

                RunDisplaySwitch("/external");

                OnDisplayChanged?.Invoke(
                    GetCurrentDisplayName());
            }
            catch
            {
            }
        }

        /// <summary>
        /// Detects whether more than one monitor is connected.
        /// </summary>
        /// <remarks>
        /// Uses WMI to enumerate monitor display parameters.
        ///
        /// A failed WMI query is treated as a single-display
        /// configuration.
        /// </remarks>
        public void CheckMultipleDisplays()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "root\\WMI",
                    "SELECT * FROM WmiMonitorBasicDisplayParams"))
                {
                    int monitorCount = 0;

                    foreach (ManagementObject monitor in searcher.Get())
                    {
                        monitorCount++;
                    }

                    _hasMultipleDisplays = monitorCount > 1;
                }
            }
            catch
            {
                _hasMultipleDisplays = false;
            }
        }

        /// <summary>
        /// Restores the internal display when exiting
        /// an application session started in HotSwap mode.
        /// </summary>
        /// <remarks>
        /// The restore operation is performed only when
        /// multiple displays are available and the application
        /// is currently configured for the external display.
        /// </remarks>
        public void HandleHotswapOnExit()
        {
            if (_wasLaunchedWithHotswap &&
                _hasMultipleDisplays &&
                _isExternalDisplay)
            {
                try
                {
                    _sound?.PlayBackSound();

                    RunDisplaySwitch("/internal");
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Synchronizes the selected display mode with
        /// the current Windows projection topology.
        /// </summary>
        private bool SynchronizeDisplayMode()
        {
            if (!DisplayTopology.TryGetIsExternal(out bool isExternal))
                return false;

            _isExternalDisplay = isExternal;
            _currentDisplayIndex = isExternal ? 1 : 0;

            return true;
        }

        /// <summary>
        /// Rebuilds the list of available display modes.
        /// </summary>
        /// <remarks>
        /// The original implementation exposes two logical
        /// display choices: Default and External.
        /// </remarks>
        public void RefreshDisplayList()
        {
            _displayNames.Clear();

            _displayNames.Add("Default");
            _displayNames.Add("External");
        }

        /// <summary>
        /// Returns the name of the currently selected
        /// logical display mode.
        /// </summary>
        public string GetCurrentDisplayName()
        {
            if (_displayNames.Count == 0)
                RefreshDisplayList();

            if (_currentDisplayIndex >= _displayNames.Count)
                _currentDisplayIndex = 0;

            return _displayNames[_currentDisplayIndex];
        }

        /// <summary>
        /// Moves the selected display mode in the
        /// specified direction and applies the change.
        /// </summary>
        /// <remarks>
        /// Selection wraps around between the available
        /// logical display modes.
        ///
        /// The display change event is raised after
        /// requesting the Windows display switch.
        /// </remarks>
        public void SwitchDisplay(int direction)
        {
            if (!_hasMultipleDisplays || _displayNames.Count <= 1)
                return;

            // Read the actual Windows projection mode before
            // selecting the next display.
            if (!SynchronizeDisplayMode())
                return;

            _currentDisplayIndex += direction;

            if (_currentDisplayIndex < 0)
                _currentDisplayIndex = _displayNames.Count - 1;

            if (_currentDisplayIndex >= _displayNames.Count)
                _currentDisplayIndex = 0;

            bool isExternal = _currentDisplayIndex > 0;

            _isExternalDisplay = isExternal;

            RunDisplaySwitch(
                isExternal ? "/external" : "/internal");

            OnDisplayChanged?.Invoke(
                GetCurrentDisplayName());
        }

        /// <summary>
        /// Restores the main window to a maximized state
        /// and forces its layout to update.
        /// </summary>
        /// <remarks>
        /// Refreshes are limited to one operation per
        /// LayoutRefreshDelayMs interval.
        ///
        /// The window is temporarily restored to Normal,
        /// resized to the primary screen, and maximized again.
        /// </remarks>
        public void EnsureMaximizedAndRefreshLayout()
        {
            double elapsed =
                (DateTime.Now - _lastLayoutRefresh).TotalMilliseconds;

            if (elapsed < LayoutRefreshDelayMs)
            {
                DebugLog.Write(
                    "Display",
                    $"Layout refresh skipped: {elapsed:F0} ms.");

                return;
            }

            try
            {
                if (_window is Window window)
                {
                    DebugLog.Write(
                        "Display",
                        $"Refresh START: state={window.WindowState}, " +
                        $"size={window.ActualWidth:F0}x{window.ActualHeight:F0}, " +
                        $"primary={SystemParameters.PrimaryScreenWidth:F0}x{SystemParameters.PrimaryScreenHeight:F0}");

                    _lastLayoutRefresh = DateTime.Now;

                    window.WindowState = WindowState.Normal;

                    window.Left = 0;
                    window.Top = 0;

                    window.Width =
                        SystemParameters.PrimaryScreenWidth;

                    window.Height =
                        SystemParameters.PrimaryScreenHeight;

                    window.InvalidateVisual();

                    window.WindowState = WindowState.Maximized;

                    window.UpdateLayout();

                    DebugLog.Write(
                        "Display",
                        $"Refresh END: state={window.WindowState}, " +
                        $"size={window.ActualWidth:F0}x{window.ActualHeight:F0}");
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Display",
                    ex,
                    "Failed to refresh window layout.");
            }
        }

        /// <summary>
        /// Schedules a delayed main window layout refresh.
        /// </summary>
        /// <remarks>
        /// The callback runs at DispatcherPriority.Background
        /// and waits 100 ms before refreshing the window.
        ///
        /// This allows Windows display changes to begin
        /// propagating before the layout is recalculated.
        /// </remarks>
        private void ScheduleLayoutRefresh()
        {
            if (!(_window is Window window))
                return;

            window.Dispatcher.BeginInvoke(
                new Action(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(100);

                    EnsureMaximizedAndRefreshLayout();
                }),
                DispatcherPriority.Background);
        }

        /// <summary>
        /// Handles Windows display configuration changes.
        /// </summary>
        /// <remarks>
        /// WPF handles WM_DPICHANGED internally through its
        /// per-monitor DPI support. This hook must not mark
        /// that message as handled.
        ///
        /// WM_DISPLAYCHANGE requests the existing window
        /// layout refresh without suppressing Windows processing.
        /// </remarks>
        public IntPtr WndProc(
            IntPtr hwnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam,
            ref bool handled)
        {
            const int WM_DISPLAYCHANGE = 0x007E;

            if (msg == WM_DISPLAYCHANGE)
            {
                DebugLog.Write(
                    "Display",
                    "WM_DISPLAYCHANGE received.");

                ScheduleLayoutRefresh();
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Handles Windows display settings changes.
        /// Refreshes monitor detection and schedules
        /// a main window layout update.
        /// </summary>
        private void OnDisplaySettingsChanged(
            object sender,
            EventArgs e)
        {
            DebugLog.Write("Display", "DisplaySettingsChanged received.");

            CheckMultipleDisplays();
            SynchronizeDisplayMode();

            ScheduleLayoutRefresh();
        }

        /// <summary>
        /// Runs the Windows DisplaySwitch utility
        /// with the specified display mode argument.
        /// </summary>
        /// <remarks>
        /// The current implementation uses /internal
        /// and /external to change the active display.
        ///
        /// Process launch errors are ignored.
        /// </remarks>
        public void RunDisplaySwitch(string argument)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "DisplaySwitch.exe",
                    Arguments = argument,
                    UseShellExecute = true,
                    CreateNoWindow = true
                });
            }
            catch
            {
            }
        }

        /// <summary>
        /// Releases the Windows display settings
        /// event subscription.
        /// </summary>
        public void Dispose()
        {
            try
            {
                SystemEvents.DisplaySettingsChanged -=
                    OnDisplaySettingsChanged;
            }
            catch
            {
            }
        }
    }
}
