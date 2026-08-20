using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Media;

class Program
{
    private static Mutex _singleInstanceMutex;

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputStateEx
    {
        public uint dwPacketNumber;
        public ushort wButtons;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "#100")]
    private static extern int XInputGetStateEx(int dwUserIndex, ref XInputStateEx pState);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const int ERROR_SUCCESS = 0;
    private const ushort XINPUT_GAMEPAD_GUIDE = 0x0400;
    private const string ProcessName = "EZ2Play";
    private const byte VK_MENU = 0x12;
    private const uint KEYEVENTF_KEYUP = 0x0002;

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
        
        // Args Filter
        foreach (var arg in args)
        {
            // For Daemon (Debug)
            if (arg == "-d" || arg == "--debug")
                debugMode = true;
                
            // For EZ2Play (Any Args)
            else
                launcherArgs += arg + " ";
        }
        launcherArgs = launcherArgs.Trim();
        
        string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EZ2Play.exe");
        
        if (debugMode)
        {
            AllocConsole();
            Console.Title = "EZ2Play Daemon [DEBUG]";
            Console.WriteLine("DEBUG MODE ACTIVE");
            Console.WriteLine($"Raw args: {string.Join(", ", args)}");
            Console.WriteLine($"Launcher args: [{launcherArgs}]");
        }

        bool pressed = false;
        DateTime startTime = DateTime.Now;
        bool longPressHandled = false;
        bool wasRunning = false;
        bool waitingForClose = false;

        while (true)
        {
            bool isRunning = Process.GetProcessesByName(ProcessName).Length > 0;
            
            if (isRunning && !wasRunning)
            {
                if (debugMode) Console.WriteLine($" [APP] Application started");
                waitingForClose = true;
                new Thread(() => FocusWindowWithRetry(debugMode)).Start();
            }
            else if (!isRunning && wasRunning)
            {
                if (debugMode) Console.WriteLine($" [APP] Application closed");
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
            else if (guide && pressed && !longPressHandled && (DateTime.Now - startTime).TotalMilliseconds >= 500)
            {
                longPressHandled = true;
                if (debugMode) Console.WriteLine(" [BUTTON] Long Press Detected");

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

    // EZ2Play Focus
    static bool IsGuidePressed()
    {
        for (int userIndex = 0; userIndex < 4; userIndex++)
        {
            var state = new XInputStateEx();

            int result =
                XInputGetStateEx(userIndex, ref state);

            if (result == ERROR_SUCCESS &&
                (state.wButtons & XINPUT_GAMEPAD_GUIDE) != 0)
            {
                return true;
            }
        }

        return false;
    }

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
                    // Отсоединяем в обратном порядке.
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
                    Console.WriteLine(" [FOCUS] Focus set");

                return;
            }
            
            Thread.Sleep(500);
        }
        
        if (debugMode) Console.WriteLine($" [FOCUS] Failed to find window");
    }

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
                    if (Process.GetProcessById((int)pid).ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                    {
                        foundWindow = hwnd;
                        return false;
                    }
                }
                catch { }
            }
            return true;
        }, IntPtr.Zero);
        
        return foundWindow;
    }
}