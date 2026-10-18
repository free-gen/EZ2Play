
using System;
using System.IO;

namespace EZ2Play.App
{
    /// <summary>
    /// Manages detection, startup, and shutdown of FPS Monitor.
    /// </summary>
    /// <remarks>
    /// SystemProvider remains the public facade for these operations.
    /// The original process names, startup options, timeouts,
    /// status checks, and error handling are preserved.
    /// </remarks>
    internal static class FpsMonitorIntegration
    {
        // Default installation path used by the original implementation.
        private const string FpsMonitorPath =
            @"C:\Program Files (x86)\FPS Monitor\FPSMonitor.exe";

        /// <summary>
        /// Checks whether FPS Monitor is installed at its expected path.
        /// </summary>
        internal static bool IsFpsMonitorInstalled()
        {
            return File.Exists(FpsMonitorPath);
        }

        /// <summary>
        /// Checks whether a process named FPSMonitor is running.
        /// </summary>
        internal static bool IsFpsMonitorRunning()
        {
            try
            {
                return System.Diagnostics.Process.GetProcessesByName(
                    "FPSMonitor").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Starts FPS Monitor if it is installed and not already running.
        /// </summary>
        /// <remarks>
        /// Preserves UseShellExecute=true and the existing log messages.
        /// </remarks>
        internal static bool StartFpsMonitor()
        {
            try
            {
                if (IsFpsMonitorRunning())
                {
                    DebugLog.Write(
                        "FPS Monitor",
                        "FPS Monitor is already running.");

                    return true;
                }

                if (!File.Exists(FpsMonitorPath))
                {
                    DebugLog.Write(
                        "FPS Monitor",
                        "FPS Monitor executable not found.");

                    return false;
                }

                var process = System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = FpsMonitorPath,
                        UseShellExecute = true
                    });

                if (process == null)
                {
                    DebugLog.Write(
                        "FPS Monitor",
                        "FPS Monitor process could not be started.");

                    return false;
                }

                DebugLog.Write("FPS Monitor", "FPS Monitor started.");

                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "FPS Monitor",
                    ex,
                    "Failed to start FPS Monitor.");

                return false;
            }
        }

        /// <summary>
        /// Stops FPS Monitor by launching taskkill with elevation.
        /// </summary>
        /// <remarks>
        /// Waits up to 15 seconds for taskkill to exit. Afterwards,
        /// checks the FPSMonitor process list up to 20 times with
        /// a 100 ms delay between checks. All original error paths
        /// and log messages are preserved.
        /// </remarks>
        internal static bool StopFpsMonitor()
        {
            try
            {
                if (!IsFpsMonitorRunning())
                    return true;

                using (var process = System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "taskkill.exe",
                        Arguments = "/F /IM FPSMonitor.exe",
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle =
                            System.Diagnostics.ProcessWindowStyle.Hidden
                    }))
                {
                    if (process == null)
                    {
                        DebugLog.Write(
                            "FPS Monitor",
                            "taskkill process could not be started.");

                        return false;
                    }

                    if (!process.WaitForExit(15000))
                    {
                        DebugLog.Write(
                            "FPS Monitor",
                            "taskkill timed out after 15 seconds.");

                        try
                        {
                            if (!process.HasExited)
                                process.Kill();
                        }
                        catch (Exception ex)
                        {
                            DebugLog.Error(
                                "FPS Monitor",
                                ex,
                                "Failed to terminate timed-out taskkill.");
                        }

                        return false;
                    }

                    // taskkill may exit before FPSMonitor disappears
                    // from the process list.
                    const int maxAttempts = 20;
                    const int delayMs = 100;

                    for (int attempt = 0;
                         attempt < maxAttempts;
                         attempt++)
                    {
                        if (!IsFpsMonitorRunning())
                        {
                            DebugLog.Write(
                                "FPS Monitor",
                                $"FPS Monitor stopped after {attempt * delayMs} ms.");

                            return true;
                        }

                        System.Threading.Thread.Sleep(delayMs);
                    }

                    DebugLog.Write(
                        "FPS Monitor",
                        $"FPS Monitor is still running after taskkill. ExitCode={process.ExitCode}");

                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "FPS Monitor",
                    ex,
                    "Failed to stop FPS Monitor.");

                return false;
            }
        }
    }
}
