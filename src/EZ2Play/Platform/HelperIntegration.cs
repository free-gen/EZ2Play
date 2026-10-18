using System;
using System.IO;
using System.Runtime.InteropServices;

namespace EZ2Play.App
{
    /// <summary>
    /// Manages the EZ2Play Helper process and its Windows Startup shortcut.
    /// </summary>
    /// <remarks>
    /// SystemProvider remains the public facade for these operations.
    /// The original PowerShell commands, COM cleanup, process options,
    /// logging, and error handling are preserved.
    /// </remarks>
    internal static class HelperIntegration
    {
        // Name of the shortcut stored in the Windows Startup folder.
        private const string AutorunShortcutName = "EZ2Play Helper.lnk";

        /// <summary>
        /// Checks whether the EZ2Play Helper shortcut exists in Startup.
        /// </summary>
        internal static bool IsAutorunEnabled()
        {
            try
            {
                string startupFolder = Environment.GetFolderPath(
                    Environment.SpecialFolder.Startup);

                string shortcutPath = Path.Combine(
                    startupFolder,
                    AutorunShortcutName);

                return File.Exists(shortcutPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Executes the PowerShell script used to maintain the Startup shortcut.
        /// </summary>
        private static bool RunAutorunPowerShell(
            string script,
            string operation)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",

                    Arguments =
                        "-NoProfile -ExecutionPolicy Bypass -Command " +
                        "\"$ErrorActionPreference='Stop'; " +
                        script +
                        "\"",

                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true
                };

                using (var process =
                    System.Diagnostics.Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        DebugLog.Write(
                            "Autorun",
                            $"{operation}: PowerShell could not be started.");

                        return false;
                    }

                    string error =
                        process.StandardError.ReadToEnd().Trim();

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        DebugLog.Write(
                            "Autorun",
                            $"{operation} failed. ExitCode={process.ExitCode}. Error={error}");

                        return false;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Autorun",
                    ex,
                    $"{operation} failed.");

                return false;
            }
        }

        /// <summary>
        /// Creates the Helper shortcut in Startup and starts the Helper process.
        /// </summary>
        internal static bool EnableAutorun()
        {
            try
            {
                string helperPath = GetHelperExecutablePath();

                if (!File.Exists(helperPath))
                {
                    DebugLog.Write(
                        "Autorun",
                        "Helper executable not found.");

                    return false;
                }

                string startupFolder = Environment.GetFolderPath(
                    Environment.SpecialFolder.Startup);

                string shortcutPath = Path.Combine(
                    startupFolder,
                    AutorunShortcutName);

                string workDir = Path.GetDirectoryName(helperPath);

                // Escape single quotes for PowerShell string literals.
                string ps =
                    "$WshShell = New-Object -ComObject WScript.Shell; " +
                    "$Shortcut = $WshShell.CreateShortcut('" +
                    shortcutPath.Replace("'", "''") +
                    "'); " +
                    "$Shortcut.TargetPath = '" +
                    helperPath.Replace("'", "''") +
                    "'; " +
                    "$Shortcut.WorkingDirectory = '" +
                    workDir.Replace("'", "''") +
                    "'; " +
                    "$Shortcut.Arguments = ''; " +
                    "$Shortcut.Save();";

                if (!RunAutorunPowerShell(ps, "Enable autorun"))
                    return false;

                if (!File.Exists(shortcutPath))
                {
                    DebugLog.Write(
                        "Autorun",
                        "Startup shortcut was not created.");

                    return false;
                }

                if (!StartHelperProcess(""))
                {
                    DebugLog.Write(
                        "Autorun",
                        "Helper process could not be started.");

                    return false;
                }

                DebugLog.Write("Autorun", "Autorun enabled.");

                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Autorun",
                    ex,
                    "Failed to enable autorun.");

                return false;
            }
        }

        /// <summary>
        /// Removes the Startup shortcut and stops running Helper processes.
        /// </summary>
        internal static bool DisableAutorun()
        {
            try
            {
                string startupFolder = Environment.GetFolderPath(
                    Environment.SpecialFolder.Startup);

                string shortcutPath = Path.Combine(
                    startupFolder,
                    AutorunShortcutName);

                if (File.Exists(shortcutPath))
                    File.Delete(shortcutPath);

                if (File.Exists(shortcutPath))
                {
                    DebugLog.Write(
                        "Autorun",
                        "Startup shortcut still exists after deletion.");

                    return false;
                }

                StopHelperProcess();

                DebugLog.Write("Autorun", "Autorun disabled.");

                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Autorun",
                    ex,
                    "Failed to disable autorun.");

                return false;
            }
        }

        /// <summary>
        /// Updates Startup shortcut arguments and restarts the Helper.
        /// </summary>
        internal static bool SetAutorunArguments(string args)
        {
            try
            {
                string helperPath = GetHelperExecutablePath();

                if (!File.Exists(helperPath))
                {
                    DebugLog.Write(
                        "Autorun",
                        "Cannot update arguments: Helper executable not found.");

                    return false;
                }

                string startupFolder = Environment.GetFolderPath(
                    Environment.SpecialFolder.Startup);

                string shortcutPath = Path.Combine(
                    startupFolder,
                    AutorunShortcutName);

                if (!File.Exists(shortcutPath))
                {
                    DebugLog.Write(
                        "Autorun",
                        "Cannot update arguments: startup shortcut does not exist.");

                    return false;
                }

                string workDir = Path.GetDirectoryName(helperPath);

                args = (args ?? string.Empty).Trim();

                string ps =
                    "$WshShell = New-Object -ComObject WScript.Shell; " +
                    "$Shortcut = $WshShell.CreateShortcut('" +
                    shortcutPath.Replace("'", "''") +
                    "'); " +
                    "$Shortcut.TargetPath = '" +
                    helperPath.Replace("'", "''") +
                    "'; " +
                    "$Shortcut.WorkingDirectory = '" +
                    workDir.Replace("'", "''") +
                    "'; " +
                    "$Shortcut.Arguments = '" +
                    args.Replace("'", "''") +
                    "'; " +
                    "$Shortcut.Save();";

                if (!RunAutorunPowerShell(
                    ps,
                    "Update autorun arguments"))
                {
                    return false;
                }

                if (!File.Exists(shortcutPath))
                {
                    DebugLog.Write(
                        "Autorun",
                        "Startup shortcut disappeared after arguments update.");

                    return false;
                }

                StopHelperProcess();
                StartHelperProcess(args);

                DebugLog.Write(
                    "Autorun",
                    $"Arguments updated: [{args}]");

                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Autorun",
                    ex,
                    "Failed to update autorun arguments.");

                return false;
            }
        }

        /// <summary>
        /// Reads the arguments stored in the existing Startup shortcut.
        /// </summary>
        /// <remarks>
        /// Releases the original Windows Script Host COM objects in finally.
        /// </remarks>
        internal static string GetAutorunArguments()
        {
            object shell = null;
            object shortcut = null;

            try
            {
                string shortcutPath = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Startup),
                    AutorunShortcutName);

                if (!File.Exists(shortcutPath))
                    return string.Empty;

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");

                if (shellType == null)
                    return string.Empty;

                shell = Activator.CreateInstance(shellType);

                dynamic dynamicShell = shell;

                shortcut = dynamicShell.CreateShortcut(shortcutPath);

                dynamic dynamicShortcut = shortcut;

                string arguments = dynamicShortcut.Arguments;

                return arguments?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut))
                    Marshal.FinalReleaseComObject(shortcut);

                if (shell != null && Marshal.IsComObject(shell))
                    Marshal.FinalReleaseComObject(shell);
            }
        }

        /// <summary>
        /// Returns the executable path under the application base directory.
        /// </summary>
        internal static string GetHelperExecutablePath()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;

            string helperPath = Path.Combine(
                appDir,
                "EZ2Play Helper.exe");

            return helperPath;
        }

        /// <summary>
        /// Starts Helper unless a matching process is already running.
        /// </summary>
        internal static bool StartHelperProcess(string arguments = "")
        {
            try
            {
                string helperPath = GetHelperExecutablePath();

                if (string.IsNullOrEmpty(helperPath) ||
                    !File.Exists(helperPath))
                {
                    return false;
                }

                // Do not start another helper instance if one is already running.
                if (IsHelperProcessRunning())
                    return true;

                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.FileName = helperPath;

                    process.StartInfo.WorkingDirectory =
                        Path.GetDirectoryName(helperPath);

                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    if (!process.Start())
                        return false;

                    return !process.HasExited;
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Helper",
                    ex,
                    "Failed to start Helper.");

                return false;
            }
        }

        /// <summary>
        /// Stops every running process named EZ2Play Helper.
        /// </summary>
        internal static void StopHelperProcess()
        {
            try
            {
                foreach (var process in
                    System.Diagnostics.Process.GetProcessesByName(
                        "EZ2Play Helper"))
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Returns whether an EZ2Play Helper process is present.
        /// </summary>
        internal static bool IsHelperProcessRunning()
        {
            try
            {
                var processes =
                    System.Diagnostics.Process.GetProcessesByName(
                        "EZ2Play Helper");

                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
