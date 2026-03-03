using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Management;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace EZ2Play.App
{
    public class Display
    {
        private readonly FrameworkElement _window;
        private readonly Sound _audioManager;
        private bool _isExternalDisplay = false;
        private bool _hasMultipleDisplays = false;
        private bool _wasLaunchedWithHotswap = false;
        
        // Для предотвращения множественных перерасчетов
        private DateTime _lastLayoutRefresh = DateTime.MinValue;
        private const int LayoutRefreshDelayMs = 500; // Задержка между перерасчетами

        public bool HasMultipleDisplays => _hasMultipleDisplays;
        public bool IsExternalDisplay => _isExternalDisplay;

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
                    
                    UpdateDisplayToggleVisibility();
                }
            }
            catch (Exception)
            {
                _hasMultipleDisplays = false;
                UpdateDisplayToggleVisibility();
            }
        }

        public void UpdateDisplayToggleVisibility()
        {
            try
            {
                var bottomPanel = _window.FindName("BottomPanel") as Border;
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
            catch (Exception) { }
        }

        public void ToggleDisplay()
        {
            if (!_hasMultipleDisplays)
            {
                return;
            }

            try
            {
                _audioManager?.PlayBackSound();
                
                _isExternalDisplay = !_isExternalDisplay;
                var argument = _isExternalDisplay ? "/external" : "/internal";
                
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
                MessageBox.Show($"Failed to switch display: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void EnsureMaximizedAndRefreshLayout()
        {
            // Защита от слишком частых вызовов
            if ((DateTime.Now - _lastLayoutRefresh).TotalMilliseconds < LayoutRefreshDelayMs)
                return;
            
            try
            {
                if (_window is Window window)
                {
                    _lastLayoutRefresh = DateTime.Now;
                    
                    // Сохраняем текущее состояние
                    var wasVisible = window.IsVisible;
                    
                    window.WindowState = WindowState.Normal;
                    window.Left = 0;
                    window.Top = 0;
                    window.Width = SystemParameters.PrimaryScreenWidth;
                    window.Height = SystemParameters.PrimaryScreenHeight;
                    
                    // Используем InvalidateVisual вместо UpdateLayout для более легкого перерасчета
                    window.InvalidateVisual();
                    
                    window.WindowState = WindowState.Maximized;
                    
                    // Вызываем UpdateLayout только один раз
                    window.UpdateLayout();
                }
            }
            catch (Exception) { }
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (_window is Window window)
            {
                // Используем Background priority и задержку для группировки событий
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    System.Threading.Thread.Sleep(100); // Небольшая задержка для стабилизации
                    EnsureMaximizedAndRefreshLayout();
                }), DispatcherPriority.Background);
            }
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
                    // Используем задержку для группировки событий
                    window.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        System.Threading.Thread.Sleep(100);
                        EnsureMaximizedAndRefreshLayout();
                    }), DispatcherPriority.Background);
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
                    _audioManager?.PlayBackSound();
                    
                    var argument = _isExternalDisplay ? "/internal" : "/external";

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "DisplaySwitch.exe",
                        Arguments = argument,
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                }
                catch (Exception)
                {
                }
            }
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