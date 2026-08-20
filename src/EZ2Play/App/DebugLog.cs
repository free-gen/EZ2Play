using System;
using System.Diagnostics;
using System.IO;

namespace EZ2Play.App
{
    internal static class DebugLog
    {
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
                // Диагностика никогда не должна ломать приложение.
            }
        }
    }
}