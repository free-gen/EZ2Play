using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Management;
using Microsoft.Win32;
using System.Runtime.InteropServices;   // ← добавлено для DPI

namespace EZ2Play.App
{
    public class Display
    {
        private readonly bool _isHorizontalMode;
        private readonly FrameworkElement _window;
        private readonly EZ2Play.App.Sound _audioManager;
        private bool _isExternalDisplay = false;
        private bool _hasMultipleDisplays = false;
        private bool _wasLaunchedWithHotswap = false;

        public bool HasMultipleDisplays => _hasMultipleDisplays;
        public bool IsExternalDisplay => _isExternalDisplay;

        // ====================== DPI FIX ======================
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        // ====================================================

        public Display(FrameworkElement window, bool isHorizontalMode, bool wasLaunchedWithHotswap = false, EZ2Play.App.Sound audioManager = null)
        {
            _window = window;
            _isHorizontalMode = isHorizontalMode;
            _wasLaunchedWithHotswap = wasLaunchedWithHotswap;
            _audioManager = audioManager;
            
            CheckMultipleDisplays();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        public void CheckMultipleDisplays()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBasicDisplayParams"))
                {
                    var monitors = searcher.Get();
                    int monitorCount = 0;
                    
                    foreach (ManagementObject monitor in monitors)
                    {
                        monitorCount++;
                    }
                    
                    _hasMultipleDisplays = monitorCount > 1;
                    Log($"Detected {monitorCount} monitor(s). Multiple displays: {_hasMultipleDisplays}");
                    
                    UpdateDisplayToggleVisibility();
                }
            }
            catch (Exception ex)
            {
                Log($"Error checking multiple displays: {ex.Message}");
                _hasMultipleDisplays = false;
                UpdateDisplayToggleVisibility();
            }
        }

        public void UpdateDisplayToggleVisibility()
        {
            try
            {
                var bottomPanelName = _isHorizontalMode ? "HorizontalBottomPanel" : "VerticalBottomPanel";
                var bottomPanel = _window.FindName(bottomPanelName) as Border;
                if (bottomPanel?.Child is StackPanel mainStackPanel)
                {
                    if (mainStackPanel.Children.Count >= 3)
                    {
                        var displayTogglePanel = mainStackPanel.Children[2] as StackPanel;
                        if (displayTogglePanel != null)
                        {
                            displayTogglePanel.Visibility = _hasMultipleDisplays ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating display toggle visibility: {ex.Message}");
            }
        }

        public void ToggleDisplay()
        {
            if (!_hasMultipleDisplays)
            {
                Log("Display toggle requested but only one monitor detected");
                return;
            }

            try
            {
                _audioManager?.PlayBackSound();
                
                _isExternalDisplay = !_isExternalDisplay;
                var argument = _isExternalDisplay ? "/external" : "/internal";
                
                Log($"Switching display to: {(_isExternalDisplay ? "external" : "internal")}");
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = "DisplaySwitch.exe",
                    Arguments = argument,
                    UseShellExecute = true,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                Log($"Display switch error: {ex.Message}");
                MessageBox.Show($"Failed to switch display: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void EnsureMaximizedAndRefreshLayout()
        {
            try
            {
                if (_window is Window window)
                {
                    window.WindowState = WindowState.Normal;
                    window.Left = 0;
                    window.Top = 0;
                    window.Width = SystemParameters.PrimaryScreenWidth;
                    window.Height = SystemParameters.PrimaryScreenHeight;
                    window.UpdateLayout();
                    window.WindowState = WindowState.Maximized;
                    window.UpdateLayout();
                }
            }
            catch (Exception ex)
            {
                Log($"Error refreshing layout after display change: {ex.Message}");
            }
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (_window is Window window)
            {
                window.Dispatcher.BeginInvoke(new Action(EnsureMaximizedAndRefreshLayout), DispatcherPriority.Background);
            }
        }

        public IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_DISPLAYCHANGE = 0x007E;
            const int WM_DPICHANGED = 0x02E0;
            
            if (msg == WM_DISPLAYCHANGE || msg == WM_DPICHANGED)
            {
                if (msg == WM_DPICHANGED)   // ← DPI FIX
                {
                    var rect = (RECT)Marshal.PtrToStructure(lParam, typeof(RECT));
                    SetWindowPos(hwnd, IntPtr.Zero,
                        rect.Left, rect.Top,
                        rect.Right - rect.Left,
                        rect.Bottom - rect.Top,
                        SWP_NOZORDER | SWP_NOACTIVATE);
                    handled = true;
                }

                if (_window is Window window)
                {
                    window.Dispatcher.BeginInvoke(new Action(EnsureMaximizedAndRefreshLayout), DispatcherPriority.Background);
                }
            }
            return IntPtr.Zero;
        }

        public void HandleHotswapOnExit()
        {
            if (_wasLaunchedWithHotswap && _hasMultipleDisplays)
            {
                try
                {
                    Log("Application was launched with hotswap, switching display back on exit");
                    
                    _audioManager?.PlayBackSound();
                    
                    var argument = _isExternalDisplay ? "/internal" : "/external";
                    
                    Log($"Switching display back to: {(_isExternalDisplay ? "internal" : "external")}");
                    
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "DisplaySwitch.exe",
                        Arguments = argument,
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                }
                catch (Exception ex)
                {
                    Log($"Error switching display back on exit: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            try
            {
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            }
            catch { /* Ignore */ }
        }

        private static void Log(string message)
        {
            if (EZ2Play.Main.App.EnableLogging)
            {
                try { System.IO.File.AppendAllText("EZ2Play.log", $"[{DateTime.Now}] Display: {message}\n"); }
                catch { /* Ignore */ }
            }
        }
    }
}