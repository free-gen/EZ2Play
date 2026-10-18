
using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace EZ2Play.App
{
    /// <summary>
    /// Handles the gamepad Guide button for closing
    /// the current foreground application.
    /// </summary>
    /// <remarks>
    /// Guide input is monitored regardless of whether
    /// Xbox Game Bar is installed.
    ///
    /// When EZ2Play is in the background, pressing Guide
    /// sends WM_CLOSE to the foreground window.
    ///
    /// Existing polling, debounce, and foreground checks
    /// remain unchanged.
    /// </remarks>
    public class GuideExitHandler : IDisposable
    {
        // Extended XInput structures used to read the Guide button.
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

        // The extended XInput function exposes the Guide button state.
        [DllImport("xinput1_4.dll", EntryPoint = "#100")]
        private static extern int XInputGetState(
            int dwUserIndex,
            ref XInputStateEx pState);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool PostMessage(
            IntPtr hWnd,
            uint Msg,
            IntPtr wParam,
            IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;
        private const int DEBOUNCE_MS = 500;
        private const short GUIDE_BUTTON_MASK = 0x0400;

        private DispatcherTimer _timer;
        private readonly Sound _audio;

        private long _lastPressMs = 0;

        public GuideExitHandler(Sound audio)
        {
            _audio = audio;

            // Always monitor Guide, regardless of Game Bar installation.
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };

            _timer.Tick += Poll;
            _timer.Start();
        }

        /// <summary>
        /// Checks the Guide button state across all four
        /// supported XInput controller slots.
        /// </summary>
        private bool IsGuidePressed()
        {
            for (int userIndex = 0; userIndex < 4; userIndex++)
            {
                var stateEx = new XInputStateEx();

                int result = XInputGetState(userIndex, ref stateEx);

                if (result == 0 && (stateEx.Gamepad.wButtons & GUIDE_BUTTON_MASK) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Polls the Guide button and closes the foreground
        /// window when the launcher is running in the background.
        /// </summary>
        private void Poll(object sender, EventArgs e)
        {
            try
            {
                if (!IsGuidePressed()) return;

                // Do not close the launcher while it is in the foreground.
                if (SystemProvider.IsForeground()) return;

                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Prevent repeated presses within the debounce interval.
                if (now - _lastPressMs <= DEBOUNCE_MS) return;

                _lastPressMs = now;

                _audio?.PlayBackSound();

                // Preserve the pre-1.0 behavior by closing the current foreground window.
                IntPtr hwnd = GetForegroundWindow();

                if (hwnd != IntPtr.Zero)
                {
                    PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Stops polling the Guide button and releases
        /// the timer reference.
        /// </summary>
        public void Dispose()
        {
            _timer?.Stop();
            _timer = null;
        }
    }
}
