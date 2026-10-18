
using System;
using System.IO;
using Newtonsoft.Json;

namespace EZ2Play.App
{
    /// <summary>
    /// Manages persistent application settings and notification states.
    /// Configuration is stored in config.json under the application's
    /// roaming AppData directory.
    /// </summary>
    public class AppConfig
    {
        private readonly string _filePath;

        // Persisted HotSwap notification state.
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

        /// <summary>
        /// Determines whether the HotSwap notification should appear.
        /// The notification is shown initially and whenever the state changes.
        /// </summary>
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

        /// <summary>
        /// Restores the original default configuration values.
        /// </summary>
        private void ResetToDefaults()
        {
            HotSwapNotificationShown = false;
            LastHotSwapState = false;
            AutorunEnabled = false;
            SteamGridDbApiKey = string.Empty;
        }

        /// <summary>
        /// Loads the configuration from disk.
        /// Missing or invalid configuration data is handled using
        /// the original default-value and error-handling behavior.
        /// </summary>
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
                    HotSwapNotificationShown = data.Notifications.HotSwapShown;
                    LastHotSwapState = data.Notifications.LastHotSwapState;
                }
                else
                {
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

        /// <summary>
        /// Saves the current configuration using a temporary file
        /// before replacing or creating config.json.
        /// </summary>
        public void Save()
        {
            string tempPath = _filePath + ".tmp";

            try
            {
                string folder = Path.GetDirectoryName(_filePath);

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var data = new AppConfigData
                {
                    Notifications = new NotificationSettings
                    {
                        HotSwapShown = HotSwapNotificationShown,
                        LastHotSwapState = LastHotSwapState
                    },

                    AutorunEnabled = AutorunEnabled,
                    SteamGridDbApiKey = SteamGridDbApiKey ?? string.Empty
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);

                // Write the complete temporary file before modifying the destination.
                File.WriteAllText(tempPath, json);

                // Preserve the original file replacement behavior.
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
                // Remove any temporary file left after saving or an error.
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Represents the structure serialized to config.json.
        /// Property names must remain compatible with existing files.
        /// </summary>
        private class AppConfigData
        {
            public NotificationSettings Notifications { get; set; }
            public bool AutorunEnabled { get; set; }
            public string SteamGridDbApiKey { get; set; }
        }

        /// <summary>
        /// Stores the persisted notification history.
        /// </summary>
        private class NotificationSettings
        {
            public bool HotSwapShown { get; set; }
            public bool LastHotSwapState { get; set; }
        }
    }
}
