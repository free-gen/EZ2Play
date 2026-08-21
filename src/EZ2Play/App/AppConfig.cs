using System;
using System.IO;
using Newtonsoft.Json;

namespace EZ2Play.App
{
    public class AppConfig
    {
        private readonly string _filePath;

        // Notification settings
        public bool GamebarNotificationShown { get; set; }
        public bool LastGamebarState { get; set; }
        public bool HotSwapNotificationShown { get; set; }
        public bool LastHotSwapState { get; set; }

        // Autorun setting
        public bool AutorunEnabled { get; set; }

        // SteamGridDB API key
        public string SteamGridDbApiKey { get; set; }

        public AppConfig()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, AppInfo.Name);
            _filePath = Path.Combine(folder, "config.json");

            Load();
        }

        // Game Bar notification logic
        public bool ShouldShowGamebarNotification(bool currentState)
        {
            if (!GamebarNotificationShown)
                return true;

            return LastGamebarState != currentState;
        }

        public void MarkGamebarNotificationShown(bool currentState)
        {
            GamebarNotificationShown = true;
            LastGamebarState = currentState;
            Save();
        }

        // HotSwap notification logic
        public bool ShouldShowHotSwapNotification(bool currentState)
        {
            if (!HotSwapNotificationShown)
                return true;

            return LastHotSwapState != currentState;
        }

        public void MarkHotSwapNotificationShown(bool currentState)
        {
            HotSwapNotificationShown = true;
            LastHotSwapState = currentState;
            Save();
        }

        // Restore default configuration values
        private void ResetToDefaults()
        {
            GamebarNotificationShown = false;
            LastGamebarState = false;
            HotSwapNotificationShown = false;
            LastHotSwapState = false;
            AutorunEnabled = false;
            SteamGridDbApiKey = string.Empty;
        }

        // Load configuration from disk
        private void Load()
        {
            if (!File.Exists(_filePath))
            {
                ResetToDefaults();
                return;
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                var data = JsonConvert.DeserializeObject<AppConfigData>(json);

                if (data == null)
                {
                    ResetToDefaults();
                    DebugLog.Write("Config", "config.json contained no usable data.");
                    return;
                }

                if (data.Notifications != null)
                {
                    GamebarNotificationShown = data.Notifications.GamebarShown;
                    LastGamebarState = data.Notifications.LastGamebarState;
                    HotSwapNotificationShown = data.Notifications.HotSwapShown;
                    LastHotSwapState = data.Notifications.LastHotSwapState;
                }

                else
                {
                    GamebarNotificationShown = false;
                    LastGamebarState = false;
                    HotSwapNotificationShown = false;
                    LastHotSwapState = false;
                }

                AutorunEnabled = data.AutorunEnabled;
                SteamGridDbApiKey = data.SteamGridDbApiKey ?? string.Empty;
            }
            catch (Exception ex)
            {
                ResetToDefaults();
                DebugLog.Error("Config", ex, "Failed to load config.json. Defaults were used.");
            }
        }

        // Save configuration atomically
        public void Save()
        {
            string tempPath =
                _filePath + ".tmp";

            try
            {
                string folder = Path.GetDirectoryName(_filePath);

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var data = new AppConfigData
                    {
                        Notifications = new NotificationSettings
                            {
                                GamebarShown = GamebarNotificationShown,
                                LastGamebarState = LastGamebarState,
                                HotSwapShown = HotSwapNotificationShown,
                                LastHotSwapState = LastHotSwapState
                            },

                        AutorunEnabled = AutorunEnabled,
                        SteamGridDbApiKey = SteamGridDbApiKey ?? string.Empty
                    };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);

                // Write the complete temporary file first.
                File.WriteAllText(tempPath, json);

                // Then replace the main file atomically.
                if (File.Exists(_filePath))
                {
                    File.Replace(tempPath, _filePath, null);
                }

                else
                {
                    File.Move(tempPath, _filePath);
                }
            }

            catch (Exception ex)
            {
                DebugLog.Error("Config", ex, "Failed to save config.json.");
            }

            finally
            {
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }

                catch
                {
                }
            }
        }

        // Serialized configuration model
        private class AppConfigData
        {
            public NotificationSettings Notifications { get; set; }
            public bool AutorunEnabled { get; set; }
            public string SteamGridDbApiKey { get; set; }
        }

        // Serialized notification settings
        private class NotificationSettings
        {
            public bool GamebarShown { get; set; }
            public bool LastGamebarState { get; set; }
            public bool HotSwapShown { get; set; }
            public bool LastHotSwapState { get; set; }
        }
    }
}