using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Media;

class Program
{
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

        var state = new XInputStateEx();
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

            int result = XInputGetStateEx(0, ref state);
            bool connected = result == ERROR_SUCCESS;
            bool guide = connected && ((state.wButtons & XINPUT_GAMEPAD_GUIDE) != 0);

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
    static void FocusWindowWithRetry(bool debugMode)
    {
        Thread.Sleep(1500);
        
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            IntPtr hWnd = FindWindowByProcessName(ProcessName);
            
            if (hWnd != IntPtr.Zero)
            {
                keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
                
                uint foreThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
                uint appThread = GetWindowThreadProcessId(hWnd, out _);
                uint curThread = GetCurrentThreadId();

                if (foreThread != curThread)
                    AttachThreadInput(foreThread, curThread, true);
                if (appThread != curThread && appThread != foreThread)
                    AttachThreadInput(appThread, curThread, true);

                SetForegroundWindow(hWnd);

                if (foreThread != curThread)
                    AttachThreadInput(foreThread, curThread, false);
                if (appThread != curThread && appThread != foreThread)
                    AttachThreadInput(appThread, curThread, false);
                
                keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                if (debugMode) Console.WriteLine($" [FOCUS] Focus set");
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