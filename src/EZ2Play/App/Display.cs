using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Management;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.IO;

namespace EZ2Play.App
{
    // --------------- Класс управления настройками дисплея ---------------

    public class Display
    {
        // --------------- Поля класса ---------------

        private readonly FrameworkElement _window;
        private readonly Sound _audioManager;
        private StackPanel _displayTogglePanel;
        private bool _isExternalDisplay = false;
        private bool _hasMultipleDisplays = false;
        private bool _wasLaunchedWithHotswap = false;
        private bool _isXboxGameBarInstalled = false;

        // Для предотвращения множественных перерасчетов
        private DateTime _lastLayoutRefresh = DateTime.MinValue;
        private const int LayoutRefreshDelayMs = 500;

        // --------------- Публичные свойства ---------------

        // Наличие нескольких дисплеев (только для чтения)
        public bool HasMultipleDisplays => _hasMultipleDisplays;

        // Текущий режим внешнего дисплея (только для чтения)
        public bool IsExternalDisplay => _isExternalDisplay;

        // --------------- Native структуры ---------------

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // --------------- Native импорты ---------------

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        // --------------- Конструктор ---------------

        // Инициализирует Display с ссылкой на окно и аудиоменеджер
        public Display(FrameworkElement window, bool wasLaunchedWithHotswap = false, Sound audioManager = null)
        {
            _window = window;
            _wasLaunchedWithHotswap = wasLaunchedWithHotswap;
            _audioManager = audioManager;

            // Проверяем наличие Xbox Game Bar при запуске
            _isXboxGameBarInstalled = IsXboxGameBarInstalled();

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
            catch
            {
            }
        }

        // --------------- Управление дисплеями ---------------
        
        // Проверяет наличие XboxGameBar в системе
        public static bool IsXboxGameBarInstalled()
        {
            try
            {
                using (var process = new Process())
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

        // Проверяет наличие нескольких дисплеев в системе (Если XboxGameBar отсутствует)
        public void CheckMultipleDisplays()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "root\\WMI",
                    "SELECT * FROM WmiMonitorBasicDisplayParams"))
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

        // Устанавливает панель переключения дисплея для управления видимостью
        public void SetDisplayTogglePanel(StackPanel panel)
        {
            _displayTogglePanel = panel;
            UpdateDisplayToggleVisibility();
        }

        // Обновляет видимость панели переключения дисплея
        public void UpdateDisplayToggleVisibility()
        {
            if (_displayTogglePanel != null)
            {
                bool shouldShow = _hasMultipleDisplays 
                            && !_wasLaunchedWithHotswap 
                            && !_isXboxGameBarInstalled;

                _displayTogglePanel.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // Переключает режим дисплея (внешний/внутренний)
        public void ToggleDisplay()
        {
            if (!_hasMultipleDisplays
                || _wasLaunchedWithHotswap
                || _isXboxGameBarInstalled)
                return;

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
                MessageBox.Show(
                    $"Failed to switch display: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // --------------- Управление layout окна ---------------

        // Гарантирует максимизацию окна и обновляет layout
        // Защищено от слишком частых вызовов
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

                    window.WindowState = WindowState.Normal;
                    window.Left = 0;
                    window.Top = 0;
                    window.Width = SystemParameters.PrimaryScreenWidth;
                    window.Height = SystemParameters.PrimaryScreenHeight;

                    // Используем InvalidateVisual для более легкого перерасчета
                    window.InvalidateVisual();

                    window.WindowState = WindowState.Maximized;

                    // Вызываем UpdateLayout только один раз
                    window.UpdateLayout();
                }
            }
            catch (Exception) { }
        }

        // --------------- Обработчики событий ---------------

        // Обработчик сообщений окна (DPI, изменение дисплея)
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

        // Обработчик изменения настроек дисплея
        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (_window is Window window)
            {
                // Используем Background priority и задержку для группировки событий
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    System.Threading.Thread.Sleep(100);
                    EnsureMaximizedAndRefreshLayout();
                }), DispatcherPriority.Background);
            }
        }

        // --------------- Hotswap логика ---------------

        // Восстанавливает настройки дисплея при выходе (если был hotswap)
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

        // --------------- Очистка ресурсов ---------------

        // Освобождает ресурсы (отписка от событий)
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