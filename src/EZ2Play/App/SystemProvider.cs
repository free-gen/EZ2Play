using System;
using System.IO;
using System.Security.Principal;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace EZ2Play.App
{
    public static class SystemProvider
    {
        private static DispatcherTimer _clockTimer;
        
        // ----- Аватар -----
        public static BitmapImage GetUserAvatar()
        {
            try
            {
                string path = null;
                var sid = WindowsIdentity.GetCurrent()?.User?.Value;

                if (!string.IsNullOrEmpty(sid))
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(
                        $@"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\{sid}"))
                    {
                        path = key?.GetValue("Image192") as string;
                    }
                }

                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    path = Path.Combine(@"C:\ProgramData\Microsoft\User Account Pictures", "user-192.png");

                if (!File.Exists(path))
                    return null;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                
                return bmp;
            }
            catch
            {
                return null;
            }
        }
        
        // ----- Часы -----
        public static void StartClock(Action<string> onTimeChanged)
        {            
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => onTimeChanged?.Invoke(GetCurrentTime());
            _clockTimer.Start();
        }
        
        public static void StopClock()
        {
            _clockTimer?.Stop();
            _clockTimer = null;
        }
        
        public static string GetCurrentTime() => DateTime.Now.ToString("HH:mm");
        
        // ----- Xbox Game Bar -----
        public static bool IsXboxGameBarInstalled()
        {
            try
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.FileName = "powershell.exe";
                    process.StartInfo.Arguments =
                        "-NoProfile -Command \"if(Get-AppxPackage Microsoft.XboxGamingOverlay){exit 0}else{exit 1}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}