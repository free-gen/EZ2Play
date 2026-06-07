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

        public AppConfig()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, AppInfo.Name);
            _filePath = Path.Combine(folder, "config.json");
            
            Load();
        }

        public bool ShouldShowGamebarNotification(bool currentState)
        {
            // Не показывали никогда → показать
            if (!GamebarNotificationShown)
                return true;
            
            // Показывали, но состояние изменилось → показать снова
            return LastGamebarState != currentState;
        }

        public void MarkGamebarNotificationShown(bool currentState)
        {
            GamebarNotificationShown = true;
            LastGamebarState = currentState;
            Save();
        }

        private void Load()
        {
            if (!File.Exists(_filePath))
            {
                // Значения по умолчанию
                GamebarNotificationShown = false;
                LastGamebarState = false;
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
                }
            }
            catch
            {
                // При ошибке — безопасные значения по умолчанию
                GamebarNotificationShown = false;
                LastGamebarState = false;
            }
        }

        private void Save()
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
                        LastGamebarState = LastGamebarState
                    }
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }

        // Внутренние классы для сериализации
        private class AppConfigData
        {
            public NotificationSettings Notifications { get; set; }
        }

        private class NotificationSettings
        {
            public bool GamebarShown { get; set; }
            public bool LastGamebarState { get; set; }
        }
    }
}