using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using SharpDX.XInput;

// Обработчик кнопки HOME (GUIDE) для закрытия активного процесса игры и возврата в лаунчер

namespace EZ2Play.App
{
    public class GuideExitHandler : IDisposable
    {
        // XInput
        [DllImport("xinput1_4.dll", EntryPoint = "#100")]
        private static extern int XInputGetState(int dwUserIndex, ref XInputStateEx pState);

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputStateEx
        {
            public int PacketNumber;
            public XInputGamepadEx Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputGamepadEx
        {
            public short wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        // WM_CLOSE
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;

        private DispatcherTimer _timer;
        private readonly Sound _audio;
        private long _lastPressMs = 0;
        private const int DEBOUNCE_MS = 500;

        public GuideExitHandler(Sound audio)
        {
            _audio = audio;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += Poll;
            _timer.Start();
        }

        // При нажатии GUIDE закрыть активный процесс игры
        private void Poll(object sender, EventArgs e)
        {
            try
            {
                var stateEx = new XInputStateEx();
                int result = XInputGetState(0, ref stateEx);

                if (result == 0 && (stateEx.Gamepad.wButtons & 0x0400) != 0)
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (now - _lastPressMs > DEBOUNCE_MS)
                    {
                        _lastPressMs = now;
                        _audio?.PlayBackSound(); // проигрывает звук при нажатии

                        // Закрываем, если лаунчер НЕ в фокусе
                        IntPtr hwnd = GetForegroundWindow();
                        if (Application.Current?.MainWindow != null)
                        {
                            var launcherHwnd = new WindowInteropHelper(Application.Current.MainWindow).Handle;
                            if (hwnd == launcherHwnd)
                                return;
                        }

                        if (hwnd != IntPtr.Zero)
                            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    }
                }
            }
            catch { }
        }

        public void Dispose()
        {
            _timer?.Stop();
            _timer = null;
        }
    }
}