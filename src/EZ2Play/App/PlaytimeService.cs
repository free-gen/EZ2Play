using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace EZ2Play.App
{
    public class PlaytimeService
    {
        private Dictionary<string, int> _data;
        private readonly string _filePath;

        private string _currentGameId;
        private DateTime _startTime;
        private bool _isRunning;

        public PlaytimeService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, AppInfo.Name);

            _filePath = Path.Combine(folder, "playtime.json");

            Load();
        }

        // ========================= СЕССИЯ =========================

        public void Start(string gameId)
        {
            Console.WriteLine("START: " + gameId);
            
            _currentGameId = gameId.ToLower();
            _startTime = DateTime.Now;
            _isRunning = true;
        }

        public void Stop()
        {
            Console.WriteLine("STOP CALLED");
            Console.WriteLine($"Running: {_isRunning}, GameId: {_currentGameId}");

            if (!_isRunning || _currentGameId == null)
                return;

            var session = DateTime.Now - _startTime;

            if (session.TotalSeconds >= 10)
            {
                AddPlaytime(_currentGameId, (int)session.TotalSeconds);
            }

            _isRunning = false;
            _currentGameId = null;
        }

        // ========================= ДАННЫЕ =========================

        public int GetSeconds(string gameId)
        {
            gameId = gameId.ToLower();

            if (_data.ContainsKey(gameId))
                return _data[gameId];

            return 0;
        }

        private void AddPlaytime(string gameId, int seconds)
        {
            if (!_data.ContainsKey(gameId))
                _data[gameId] = 0;

            _data[gameId] += seconds;

            Save();
        }

        // ========================= ФОРМАТ =========================

        public (int value, bool isHours) GetFormattedValue(string gameId)
        {
            int seconds = GetSeconds(gameId);
            
            if (seconds == 0)
                return (0, false);
            
            var ts = TimeSpan.FromSeconds(seconds);
            
            // Если есть часы
            if (ts.TotalHours >= 1)
            {
                int hours = (int)ts.TotalHours;
                int minutes = ts.Minutes;
                
                if (minutes >= 50)
                {
                    hours++;
                }
                
                return (hours, true); // true = это часы
            }
            
            // Для минут - любые секунды округляем до 1 минуты
            int totalMinutes = (int)Math.Ceiling(ts.TotalMinutes);
            return (totalMinutes, false); // false = это минуты
        }

        // ========================= JSON =========================

        private void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    _data = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
                }
                else
                {
                    _data = new Dictionary<string, int>();
                }
            }
            catch
            {
                _data = new Dictionary<string, int>();
            }
        }

        private void Save()
        {
            try
            {
                string folder = Path.GetDirectoryName(_filePath);

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string json = JsonConvert.SerializeObject(_data, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }
    }
}