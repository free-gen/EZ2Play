using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using SharpDX.XInput;

namespace EZ2Play.App
{
    // --------------- Обработчик кнопки HOME (GUIDE) для закрытия игры ---------------

    public class GuideExitHandler : IDisposable
    {
        // --------------- Native структуры ---------------

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

        // --------------- Native импорты ---------------

        [DllImport("xinput1_4.dll", EntryPoint = "#100")]
        private static extern int XInputGetState(int dwUserIndex, ref XInputStateEx pState);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        // --------------- Константы ---------------

        private const uint WM_CLOSE = 0x0010;
        private const int DEBOUNCE_MS = 500;
        private const short GUIDE_BUTTON_MASK = 0x0400;

        // --------------- Поля класса ---------------

        private DispatcherTimer _timer;
        private readonly Sound _audio;
        private long _lastPressMs = 0;
        private readonly bool _isXboxGameBarInstalled;

        // --------------- Конструктор ---------------

        public GuideExitHandler(Sound audio)
        {
            _audio = audio;
            _isXboxGameBarInstalled = SystemProvider.IsXboxGameBarInstalled();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += Poll;
            _timer.Start();
        }

        // --------------- Опрос контроллера ---------------

        // Проверка нажатия кнопки GUIDE (XInput)
        private bool IsGuidePressed()
        {
            for (int userIndex = 0; userIndex < 4; userIndex++)
            {
                var stateEx = new XInputStateEx();

                int result =
                    XInputGetState(userIndex, ref stateEx);

                if (result == 0 &&
                    (stateEx.Gamepad.wButtons & GUIDE_BUTTON_MASK) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void Poll(object sender, EventArgs e)
        {
            try
            {
                if (!IsGuidePressed())
                    return;

                // Если установлен Xbox Game Bar,
                // полностью игнорируем нажатие Guide.
                if (_isXboxGameBarInstalled)
                    return;

                // Если лаунчер в фокусе — Guide не закрывает его.
                if (SystemProvider.IsForeground())
                    return;

                long now =
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Защита от повторных нажатий.
                if (now - _lastPressMs <= DEBOUNCE_MS)
                    return;

                _lastPressMs = now;

                _audio?.PlayBackSound();

                // Существующую семантику до 1.0 не меняем:
                // закрываем текущее foreground window.
                IntPtr hwnd = GetForegroundWindow();

                if (hwnd != IntPtr.Zero)
                {
                    PostMessage(
                        hwnd,
                        WM_CLOSE,
                        IntPtr.Zero,
                        IntPtr.Zero);
                }
            }
            catch
            {
            }
        }

        // --------------- Очистка ресурсов ---------------

        public void Dispose()
        {
            _timer?.Stop();
            _timer = null;
        }
    }
}