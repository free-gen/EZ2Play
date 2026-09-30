using System;
using System.Diagnostics;
using System.IO;

namespace EZ2Play.App
{
    internal static class DebugLog
    {
        [Conditional("DEBUG")]
        public static void StartSession()
        {
            AppendSessionMarker("START");
        }

        [Conditional("DEBUG")]
        public static void EndSession()
        {
            AppendSessionMarker("CLOSE");
        }

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

        private static readonly object Sync = new object();

        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EZ2Play",
            "debug.log");

        [Conditional("DEBUG")]
        public static void Write(string source, string message)
        {
            Append("INFO", source, message);
        }

        [Conditional("DEBUG")]
        public static void Error(string source, Exception exception, string message = null)
        {
            string text = string.IsNullOrWhiteSpace(message)
                ? exception.ToString()
                : message + Environment.NewLine + exception;

            Append("ERROR", source, text);
        }

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