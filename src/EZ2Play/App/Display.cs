using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using System.Management;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace EZ2Play.App
{
    public class Display
    {
        private readonly FrameworkElement _window;
        private readonly Sound _sound;
        private bool _wasLaunchedWithHotswap = false;
        private bool _hasMultipleDisplays = false;
        private bool _isExternalDisplay = false;
        private bool _isXboxGameBarInstalled = false;

        private int _currentDisplayIndex = 0;
        private List<string> _displayNames = new List<string>();

        private DateTime _lastLayoutRefresh = DateTime.MinValue;
        private const int LayoutRefreshDelayMs = 500;

        public event Action<string> OnDisplayChanged;

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

        public Display(FrameworkElement window, bool wasLaunchedWithHotswap = false, Sound audioManager = null)
        {
            _window = window;
            _wasLaunchedWithHotswap = wasLaunchedWithHotswap;
            _sound = audioManager;

            _isXboxGameBarInstalled = SystemProvider.IsXboxGameBarInstalled();

            if (_wasLaunchedWithHotswap)
            {
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    TryInitialHotSwap();
                }), DispatcherPriority.ApplicationIdle);
            }

            CheckMultipleDisplays();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        private void TryInitialHotSwap()
        {
            if (!_hasMultipleDisplays)
                return;

            try
            {
                _sound?.PlayBackSound();
                _isExternalDisplay = true; // При hotswap всегда переключаем на external
                _currentDisplayIndex = 1; // Синхронизируем индекс
                RunDisplaySwitch("/external");
                OnDisplayChanged?.Invoke(GetCurrentDisplayName());
            }
            catch { }
        }

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

        // Восстанавливает настройки дисплея при выходе (если был hotswap)
        public void HandleHotswapOnExit()
        {
            if (_wasLaunchedWithHotswap && _hasMultipleDisplays && _isExternalDisplay)
            {
                try
                {
                    _sound?.PlayBackSound();
                    RunDisplaySwitch("/internal");
                }
                catch { }
            }
        }

        public void RefreshDisplayList()
        {
            _displayNames.Clear();
            _currentDisplayIndex = 0;
            _displayNames.Add("Default");
            _displayNames.Add("External");
        }

        public string GetCurrentDisplayName()
        {
            if (_displayNames.Count == 0) RefreshDisplayList();
            if (_currentDisplayIndex >= _displayNames.Count) _currentDisplayIndex = 0;
            return _displayNames[_currentDisplayIndex];
        }

        public void SwitchDisplay(int direction)
        {
            if (_displayNames.Count <= 1) return;
            
            _currentDisplayIndex += direction;
            if (_currentDisplayIndex < 0) _currentDisplayIndex = _displayNames.Count - 1;
            if (_currentDisplayIndex >= _displayNames.Count) _currentDisplayIndex = 0;
            
            bool isExternal = _currentDisplayIndex > 0;
            
            // Синхронизируем _isExternalDisplay
            _isExternalDisplay = isExternal;
            
            RunDisplaySwitch(isExternal ? "/external" : "/internal");
            
            OnDisplayChanged?.Invoke(GetCurrentDisplayName());
        }

        public void EnsureMaximizedAndRefreshLayout()
        {
            if ((DateTime.Now - _lastLayoutRefresh).TotalMilliseconds < LayoutRefreshDelayMs)
                return;

            try
            {
                if (_window is Window window)
                {
                    _lastLayoutRefresh = DateTime.Now;

                    window.WindowState = WindowState.Normal;
                    window.Left = 0;
                    window.Top = 0;
                    window.Width = SystemParameters.PrimaryScreenWidth;
                    window.Height = SystemParameters.PrimaryScreenHeight;

                    window.InvalidateVisual();
                    window.WindowState = WindowState.Maximized;
                    window.UpdateLayout();
                }
            }
            catch { }
        }

        public IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_DISPLAYCHANGE = 0x007E;
            const int WM_DPICHANGED = 0x02E0;

            if (msg == WM_DISPLAYCHANGE || msg == WM_DPICHANGED)
            {
                if (msg == WM_DPICHANGED)
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
                    window.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        System.Threading.Thread.Sleep(100);
                        EnsureMaximizedAndRefreshLayout();
                    }), DispatcherPriority.Background);
                }
            }
            return IntPtr.Zero;
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (_window is Window window)
            {
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    System.Threading.Thread.Sleep(100);
                    EnsureMaximizedAndRefreshLayout();
                }), DispatcherPriority.Background);
            }
        }

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
            catch { }
        }

        public void Dispose()
        {
            try
            {
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            }
            catch { }
        }
    }
}