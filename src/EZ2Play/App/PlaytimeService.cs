using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace EZ2Play.App
{
    public class PlaytimeService
    {
        private Dictionary<string, PlaytimeEntry> _data;
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

        public DateTime GetLastPlayed(string gameId)
        {
            gameId = NormalizeGameId(gameId);

            if (_data.ContainsKey(gameId))
                return _data[gameId].LastPlayed;

            return DateTime.MinValue;
        }

        // ========================= СЕССИЯ =========================

        public void Start(string gameId)
        {
            Console.WriteLine("START: " + gameId);
            
            _currentGameId = NormalizeGameId(gameId);
            _startTime = DateTime.Now;
            _isRunning = true;
        }

        public void Stop()
        {
            if (!_isRunning || _currentGameId == null)
                return;

            var session = DateTime.Now - _startTime;

            // Убедимся что запись существует
            if (!_data.ContainsKey(_currentGameId))
            {
                _data[_currentGameId] = new PlaytimeEntry();
            }

            // ВСЕГДА обновляем дату последнего запуска
            _data[_currentGameId].LastPlayed = DateTime.Now;

            // Добавляем время только если >= 10 сек
            if (session.TotalSeconds >= 10)
            {
                _data[_currentGameId].Playtime += (int)session.TotalSeconds;
            }

            Save();

            _isRunning = false;
            _currentGameId = null;
        }

        // ========================= ДАННЫЕ =========================

        public int GetSeconds(string gameId)
        {
            gameId = NormalizeGameId(gameId);

            if (_data.ContainsKey(gameId))
                return _data[gameId].Playtime;

            return 0;
        }

        private void AddPlaytime(string gameId, int seconds)
        {
            if (!_data.ContainsKey(gameId))
            {
                _data[gameId] = new PlaytimeEntry();
            }

            _data[gameId].Playtime += seconds;
            _data[gameId].LastPlayed = DateTime.Now;

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

        private string NormalizeGameId(string path)
        {
            return Path.GetFileName(path);
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);

                    // Пытаемся загрузить новый формат
                    try
                    {
                        _data = JsonConvert.DeserializeObject<Dictionary<string, PlaytimeEntry>>(json);
                    }
                    catch
                    {
                        // Если старый формат (int)
                        var oldData = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);

                        _data = new Dictionary<string, PlaytimeEntry>();

                        foreach (var kv in oldData)
                        {
                            _data[kv.Key] = new PlaytimeEntry
                            {
                                Playtime = kv.Value,
                                LastPlayed = DateTime.MinValue
                            };
                        }
                    }
                }
                else
                {
                    _data = new Dictionary<string, PlaytimeEntry>();
                }
            }
            catch
            {
                _data = new Dictionary<string, PlaytimeEntry>();
            }
        }

        private void Save()
        {
            try
            {
                string folder = Path.GetDirectoryName(_filePath);

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var sorted = new SortedDictionary<string, PlaytimeEntry>(_data, StringComparer.OrdinalIgnoreCase);
                var settings = new JsonSerializerSettings
                {
                    DateFormatString = "yyyy-MM-dd HH:mm"
                };

                string json = JsonConvert.SerializeObject(sorted, Formatting.Indented, settings);
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }
    }

    public class PlaytimeEntry
    {
        public int Playtime { get; set; }
        public DateTime LastPlayed { get; set; }
    }
}