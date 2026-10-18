
using System;
using System.Diagnostics;
using System.IO;

namespace EZ2Play.App
{
    /// <summary>
    /// Provides diagnostic logging for debug builds.
    /// Writes session markers, informational messages, and errors
    /// to the application's debug.log file.
    /// </summary>
    /// <remarks>
    /// Public logging methods use Conditional("DEBUG") so their
    /// calls are omitted from non-DEBUG builds.
    /// Logging failures must never interrupt application execution.
    /// </remarks>
    internal static class DebugLog
    {
        private static readonly object Sync = new object();

        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EZ2Play",
            "debug.log");

        /// <summary>
        /// Writes a marker indicating the beginning of a debug session.
        /// </summary>
        [Conditional("DEBUG")]
        public static void StartSession()
        {
            AppendSessionMarker("START");
        }

        /// <summary>
        /// Writes a marker indicating the end of a debug session.
        /// </summary>
        [Conditional("DEBUG")]
        public static void EndSession()
        {
            AppendSessionMarker("CLOSE");
        }

        /// <summary>
        /// Appends a session marker with the current timestamp.
        /// </summary>
        private static void AppendSessionMarker(string phase)
        {
            try
            {
                string directory = Path.GetDirectoryName(LogPath);

                lock (Sync)
                {
                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    bool hasContent = File.Exists(LogPath) && new FileInfo(LogPath).Length > 0;

                    // Separate consecutive debug sessions with an empty line.
                    string prefix =
                        phase == "START" && hasContent
                            ? Environment.NewLine
                            : string.Empty;

                    string line =
                        $"{prefix}DEBUG SESSION {phase}: " +
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}{Environment.NewLine}";

                    File.AppendAllText(LogPath, line);
                }
            }
            catch
            {
                // Diagnostics must never break the application.
            }
        }

        /// <summary>
        /// Writes an informational diagnostic message.
        /// </summary>
        [Conditional("DEBUG")]
        public static void Write(string source, string message)
        {
            Append("INFO", source, message);
        }

        /// <summary>
        /// Writes an exception and an optional explanatory message.
        /// </summary>
        [Conditional("DEBUG")]
        public static void Error(string source, Exception exception, string message = null)
        {
            string text = string.IsNullOrWhiteSpace(message)
                ? exception.ToString()
                : message + Environment.NewLine + exception;

            Append("ERROR", source, text);
        }

        /// <summary>
        /// Appends a timestamped diagnostic entry to the log file.
        /// </summary>
        private static void Append(string level, string source, string message)
        {
            try
            {
                string directory = Path.GetDirectoryName(LogPath);

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string line =
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} " +
                    $"[{level}] [{source}] {message}{Environment.NewLine}";

                lock (Sync)
                {
                    File.AppendAllText(LogPath, line);
                }
            }
            catch
            {
                // Diagnostics must never break the application.
            }
        }
    }
}
