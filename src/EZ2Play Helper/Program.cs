using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// Monitors the controller Guide button and starts EZ2Play
/// when a long press is detected.
/// </summary>
/// <remarks>
/// The Helper runs as a single instance and monitors the EZ2Play
/// process. When EZ2Play starts, a separate thread attempts to
/// bring its window to the foreground.
///
/// The existing polling intervals, Guide-button threshold,
/// Windows API calls, and error handling are preserved.
/// </remarks>
class Program
{
    // Prevent multiple Helper instances from running simultaneously.
    private static Mutex _singleInstanceMutex;

    /// <summary>
    /// Extended XInput controller state returned by export #100.
    /// </summary>
    /// <remarks>
    /// The sequential layout matches the unmanaged XInput structure.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    private struct XInputStateEx
    {
        public uint dwPacketNumber;
        public XInputGamepadEx Gamepad;
    }

    /// <summary>
    /// Controller buttons, triggers, and thumbstick values used by XInput.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepadEx
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    // Extended XInput export includes the Guide button state.
    [DllImport("xinput1_4.dll", EntryPoint = "#100")]
    private static extern int XInputGetStateEx(
        int dwUserIndex,
        ref XInputStateEx pState);

    // Native window foreground and thread-input operations.
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(
        uint idAttach,
        uint idAttachTo,
        bool fAttach);

    // Enumerate visible windows to locate the EZ2Play process window.
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(
        EnumWindowsProc lpEnumFunc,
        IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    // Simulate Alt key down/up while requesting foreground focus.
    [DllImport("user32.dll")]
    private static extern void keybd_event(
        byte bVk,
        byte bScan,
        uint dwFlags,
        UIntPtr dwExtraInfo);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    // Console functions used for optional diagnostic output.
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // XInput and Windows input constants.
    private const int ERROR_SUCCESS = 0;
    private const ushort XINPUT_GAMEPAD_GUIDE = 0x0400;
    private const string ProcessName = "EZ2Play";
    private const byte VK_MENU = 0x12;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    /// <summary>
    /// Starts the single-instance Helper and monitors the launcher
    /// process and controller Guide button.
    /// </summary>
    /// <param name="args">
    /// Helper arguments, including optional -d or --debug and
    /// arguments passed through to the EZ2Play executable.
    /// </param>
    /// <remarks>
    /// The loop checks the process and Guide button every 100 ms.
    /// A Guide press held for at least 500 ms starts EZ2Play.
    ///
    /// Guide input is not processed while waiting for the
    /// running EZ2Play process to close.
    /// </remarks>
    static void Main(string[] args)
    {
        bool createdNew;

        _singleInstanceMutex = new Mutex(
            true,
            @"Local\EZ2Play.Helper.SingleInstance",
            out createdNew);

        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            return;
        }

        string launcherArgs = "";
        bool debugMode = false;

        // Keep Helper debug arguments separate from launcher arguments.
        foreach (var arg in args)
        {
            if (arg == "-d" || arg == "--debug")
            {
                debugMode = true;
            }
            else
            {
                launcherArgs += arg + " ";
            }
        }

        launcherArgs = launcherArgs.Trim();

        string exePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "EZ2Play.exe");

        if (debugMode)
        {
            AllocConsole();
            Console.Title = "EZ2Play Daemon [DEBUG]";
            Console.WriteLine("DEBUG MODE ACTIVE");
            Console.WriteLine($"Raw args: {string.Join(", ", args)}");
            Console.WriteLine($"Launcher args: [{launcherArgs}]");
        }

        // State of the current Guide-button press and launcher process.
        bool pressed = false;
        DateTime startTime = DateTime.Now;
        bool longPressHandled = false;
        bool wasRunning = false;
        bool waitingForClose = false;

        while (true)
        {
            bool isRunning =
                Process.GetProcessesByName(ProcessName).Length > 0;

            if (isRunning && !wasRunning)
            {
                if (debugMode)
                {
                    Console.WriteLine($" [APP] Application started");
                }

                waitingForClose = true;

                // Focus the newly detected window without blocking polling.
                new Thread(() => FocusWindowWithRetry(debugMode)).Start();
            }
            else if (!isRunning && wasRunning)
            {
                if (debugMode)
                {
                    Console.WriteLine($" [APP] Application closed");
                }

                waitingForClose = false;
            }

            wasRunning = isRunning;

            if (waitingForClose)
            {
                Thread.Sleep(100);
                continue;
            }

            bool guide = IsGuidePressed();

            if (guide && !pressed)
            {
                pressed = true;
                startTime = DateTime.Now;
                longPressHandled = false;
            }
            else if (guide && pressed && !longPressHandled &&
                     (DateTime.Now - startTime).TotalMilliseconds >= 500)
            {
                longPressHandled = true;

                if (debugMode)
                {
                    Console.WriteLine(" [BUTTON] Long Press Detected");
                }

                SystemSounds.Beep.Play();

                if (File.Exists(exePath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = launcherArgs,
                        UseShellExecute = true
                    };

                    Process.Start(startInfo);
                }
            }
            else if (!guide && pressed)
            {
                pressed = false;
            }

            Thread.Sleep(100);
        }
    }

    /// <summary>
    /// Checks whether the Guide button is pressed on any of
    /// the four supported XInput controller slots.
    /// </summary>
    /// <returns>
    /// True when at least one connected controller reports Guide.
    /// </returns>
    static bool IsGuidePressed()
    {
        for (int userIndex = 0; userIndex < 4; userIndex++)
        {
            var state = new XInputStateEx();

            int result =
                XInputGetStateEx(userIndex, ref state);

            if (result == ERROR_SUCCESS &&
                (state.Gamepad.wButtons & XINPUT_GAMEPAD_GUIDE) != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Attempts to bring the EZ2Play window to the foreground
    /// after the launcher process is detected.
    /// </summary>
    /// <param name="debugMode">
    /// Whether to print focus attempts to the diagnostic console.
    /// </param>
    /// <remarks>
    /// Waits 1500 ms before searching for the window, then makes
    /// up to five attempts separated by 500 ms.
    ///
    /// The foreground and application input threads are attached
    /// only when needed. Attachments and the simulated Alt key
    /// are released in the original finally block and order.
    /// </remarks>
    static void FocusWindowWithRetry(bool debugMode)
    {
        Thread.Sleep(1500);

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            IntPtr hWnd = FindWindowByProcessName(ProcessName);

            if (hWnd != IntPtr.Zero)
            {
                uint foreThread = 0;
                uint appThread = 0;
                uint curThread = GetCurrentThreadId();

                bool foreAttached = false;
                bool appAttached = false;
                bool altPressed = false;

                try
                {
                    keybd_event(
                        VK_MENU,
                        0,
                        0,
                        UIntPtr.Zero);

                    altPressed = true;

                    foreThread =
                        GetWindowThreadProcessId(
                            GetForegroundWindow(),
                            out _);

                    appThread =
                        GetWindowThreadProcessId(
                            hWnd,
                            out _);

                    if (foreThread != 0 &&
                        foreThread != curThread)
                    {
                        foreAttached =
                            AttachThreadInput(
                                foreThread,
                                curThread,
                                true);
                    }

                    if (appThread != 0 &&
                        appThread != curThread &&
                        appThread != foreThread)
                    {
                        appAttached =
                            AttachThreadInput(
                                appThread,
                                curThread,
                                true);
                    }

                    SetForegroundWindow(hWnd);
                }
                finally
                {
                    // Detach input threads in reverse attachment order.
                    if (appAttached)
                    {
                        AttachThreadInput(
                            appThread,
                            curThread,
                            false);
                    }

                    if (foreAttached)
                    {
                        AttachThreadInput(
                            foreThread,
                            curThread,
                            false);
                    }

                    // Release Alt even when focus operations fail.
                    if (altPressed)
                    {
                        keybd_event(
                            VK_MENU,
                            0,
                            KEYEVENTF_KEYUP,
                            UIntPtr.Zero);
                    }
                }

                if (debugMode)
                {
                    Console.WriteLine(" [FOCUS] Focus set");
                }

                return;
            }

            Thread.Sleep(500);
        }

        if (debugMode)
        {
            Console.WriteLine($" [FOCUS] Failed to find window");
        }
    }

    /// <summary>
    /// Finds the first visible top-level window owned by
    /// a process with the specified name.
    /// </summary>
    /// <param name="processName">Name of the target process.</param>
    /// <returns>
    /// Handle of the matching window, or IntPtr.Zero when not found.
    /// </returns>
    /// <remarks>
    /// Window enumeration stops after the first match.
    /// Errors looking up individual process IDs are ignored.
    /// </remarks>
    static IntPtr FindWindowByProcessName(string processName)
    {
        IntPtr foundWindow = IntPtr.Zero;

        EnumWindows((hwnd, lParam) =>
        {
            if (IsWindowVisible(hwnd))
            {
                GetWindowThreadProcessId(hwnd, out uint pid);

                try
                {
                    if (Process.GetProcessById((int)pid).ProcessName.Equals(
                        processName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        foundWindow = hwnd;
                        return false;
                    }
                }
                catch
                {
                }
            }

            return true;
        }, IntPtr.Zero);

        return foundWindow;
    }
}
