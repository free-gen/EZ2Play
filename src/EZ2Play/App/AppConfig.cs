using System;
using System.IO;
using Newtonsoft.Json;

namespace EZ2Play.App
{
    public class AppConfig
    {
        private readonly string _filePath;
        
        // Настройки уведомлений
        public bool GamebarNotificationShown { get; set; }
        public bool LastGamebarState { get; set; }
        public bool HotSwapNotificationShown { get; set; }
        public bool LastHotSwapState { get; set; }
        
        // Настройка автозапуска
        public bool AutorunEnabled { get; set; }

        public AppConfig()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, AppInfo.Name);
            _filePath = Path.Combine(folder, "config.json");
            
            Load();
        }

        // Логика уведомления GameBar
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

        // Логика уведомления HotSwap
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

        private void Load()
        {
            if (!File.Exists(_filePath))
            {
                GamebarNotificationShown = false;
                LastGamebarState = false;
                HotSwapNotificationShown = false;
                LastHotSwapState = false;
                AutorunEnabled = false;
                return;
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                var data = JsonConvert.DeserializeObject<AppConfigData>(json);
                
                if (data?.Notifications != null)
                {
                    GamebarNotificationShown = data.Notifications.GamebarShown;
                    LastGamebarState = data.Notifications.LastGamebarState;
                    HotSwapNotificationShown = data.Notifications.HotSwapShown;
                    LastHotSwapState = data.Notifications.LastHotSwapState;
                }
                
                AutorunEnabled = data?.AutorunEnabled ?? false;
            }
            catch
            {
                GamebarNotificationShown = false;
                LastGamebarState = false;
                AutorunEnabled = false;
            }
        }

        public void Save()
        {
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
                    AutorunEnabled = AutorunEnabled
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }

        private class AppConfigData
        {
            public NotificationSettings Notifications { get; set; }
            public bool AutorunEnabled { get; set; }
        }

        private class NotificationSettings
        {
            public bool GamebarShown { get; set; }
            public bool LastGamebarState { get; set; }
            public bool HotSwapShown { get; set; }
            public bool LastHotSwapState { get; set; }
        }
    }
}