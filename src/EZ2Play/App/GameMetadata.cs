using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace EZ2Play.App
{
    public class GameMetadata
    {
        private Dictionary<string, PlaytimeEntry> _data;

        private readonly string _filePath;
        private readonly string _backupPath;

        private string _currentGameId;
        private DateTime _startTime;
        private bool _isRunning;

        public GameMetadata()
        {
            string appData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData);

            string folder =
                Path.Combine(
                    appData,
                    AppInfo.Name);

            _filePath =
                Path.Combine(
                    folder,
                    "metadata.json");

            _backupPath =
                _filePath + ".bak";

            Load();
        }

        public DateTime GetLastPlayed(string gameId)
        {
            gameId =
                NormalizeGameId(gameId);

            if (_data.ContainsKey(gameId))
                return _data[gameId].LastPlayed;

            return DateTime.MinValue;
        }

        // ========================= СЕССИЯ =========================

        public void Start(string gameId)
        {
            _currentGameId =
                NormalizeGameId(gameId);

            _startTime =
                DateTime.Now;

            _isRunning = true;

            if (!_data.ContainsKey(_currentGameId))
            {
                _data[_currentGameId] =
                    new PlaytimeEntry();
            }

            // LastPlayed означает факт успешного запуска.
            // Launcher вызывает Start только после успешного Process.Start().
            _data[_currentGameId].LastPlayed =
                _startTime;

            Save();
        }

        public void Stop()
        {
            if (!_isRunning ||
                _currentGameId == null)
            {
                return;
            }

            var session =
                DateTime.Now - _startTime;

            // Playtime учитываем по старому правилу:
            // только сессии продолжительностью >= 10 секунд.
            if (session.TotalSeconds >= 10)
            {
                if (!_data.ContainsKey(_currentGameId))
                {
                    _data[_currentGameId] =
                        new PlaytimeEntry();
                }

                _data[_currentGameId].Playtime +=
                    (int)session.TotalSeconds;

                Save();
            }

            _isRunning = false;
            _currentGameId = null;
        }

        // ========================= ДАННЫЕ =========================

        public int GetSeconds(string gameId)
        {
            gameId =
                NormalizeGameId(gameId);

            if (_data.ContainsKey(gameId))
                return _data[gameId].Playtime;

            return 0;
        }

        // ========================= ФОРМАТ =========================

        public (int value, bool isHours)
            GetFormattedValue(string gameId)
        {
            int seconds =
                GetSeconds(gameId);

            if (seconds == 0)
                return (0, false);

            var ts =
                TimeSpan.FromSeconds(seconds);

            if (ts.TotalHours >= 1)
            {
                int hours =
                    (int)ts.TotalHours;

                int minutes =
                    ts.Minutes;

                if (minutes >= 50)
                    hours++;

                return (hours, true);
            }

            int totalMinutes =
                (int)Math.Ceiling(
                    ts.TotalMinutes);

            return (totalMinutes, false);
        }

        // ========================= JSON =========================

        private string NormalizeGameId(string path)
        {
            return Path.GetFileName(path);
        }

        private void Load()
        {
            Dictionary<string, PlaytimeEntry> loaded;

            if (TryLoadFile(
                    _filePath,
                    out loaded))
            {
                _data = loaded;
                return;
            }

            if (TryLoadFile(
                    _backupPath,
                    out loaded))
            {
                _data = loaded;

                DebugLog.Write(
                    "Metadata",
                    "metadata.json could not be loaded. Backup recovered successfully.");

                RestorePrimaryFromBackup();

                return;
            }

            _data =
                new Dictionary<string, PlaytimeEntry>(
                    StringComparer.OrdinalIgnoreCase);

            if (File.Exists(_filePath) ||
                File.Exists(_backupPath))
            {
                DebugLog.Write(
                    "Metadata",
                    "metadata.json and backup could not be loaded. Empty metadata was used.");
            }
        }

        private bool TryLoadFile(
            string path,
            out Dictionary<string, PlaytimeEntry> result)
        {
            result = null;

            if (!File.Exists(path))
                return false;

            try
            {
                string json =
                    File.ReadAllText(path);

                // Текущий формат.
                try
                {
                    var current =
                        JsonConvert.DeserializeObject<
                            Dictionary<string, PlaytimeEntry>>(
                            json);

                    if (current != null)
                    {
                        result =
                            new Dictionary<string, PlaytimeEntry>(
                                current,
                                StringComparer.OrdinalIgnoreCase);

                        return true;
                    }
                }
                catch
                {
                    // Ниже попробуем legacy-формат.
                }

                // Старый формат:
                // { "game.lnk": 1234 }
                var legacy =
                    JsonConvert.DeserializeObject<
                        Dictionary<string, int>>(
                        json);

                if (legacy == null)
                    return false;

                result =
                    new Dictionary<string, PlaytimeEntry>(
                        StringComparer.OrdinalIgnoreCase);

                foreach (var kv in legacy)
                {
                    result[kv.Key] =
                        new PlaytimeEntry
                        {
                            Playtime = kv.Value,
                            LastPlayed = DateTime.MinValue
                        };
                }

                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Metadata",
                    ex,
                    $"Failed to load {Path.GetFileName(path)}.");

                return false;
            }
        }

        private void RestorePrimaryFromBackup()
        {
            string recoveryTemp =
                _filePath + ".recovery.tmp";

            try
            {
                File.Copy(
                    _backupPath,
                    recoveryTemp,
                    true);

                if (File.Exists(_filePath))
                {
                    File.Replace(
                        recoveryTemp,
                        _filePath,
                        null);
                }
                else
                {
                    File.Move(
                        recoveryTemp,
                        _filePath);
                }

                DebugLog.Write(
                    "Metadata",
                    "Primary metadata.json restored from backup.");
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Metadata",
                    ex,
                    "Failed to restore primary metadata.json from backup.");
            }
            finally
            {
                try
                {
                    if (File.Exists(recoveryTemp))
                        File.Delete(recoveryTemp);
                }
                catch
                {
                }
            }
        }

        private void Save()
        {
            string tempPath =
                _filePath + ".tmp";

            try
            {
                string folder =
                    Path.GetDirectoryName(_filePath);

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var sorted =
                    new SortedDictionary<string, PlaytimeEntry>(
                        _data,
                        StringComparer.OrdinalIgnoreCase);

                var settings =
                    new JsonSerializerSettings
                    {
                        DateFormatString =
                            "yyyy-MM-dd HH:mm:ss"
                    };

                string json =
                    JsonConvert.SerializeObject(
                        sorted,
                        Formatting.Indented,
                        settings);

                File.WriteAllText(
                    tempPath,
                    json);

                if (File.Exists(_filePath))
                {
                    // metadata.json становится новым файлом,
                    // старый гарантированно уходит в metadata.json.bak.
                    File.Replace(
                        tempPath,
                        _filePath,
                        _backupPath);
                }
                else
                {
                    File.Move(
                        tempPath,
                        _filePath);
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Metadata",
                    ex,
                    "Failed to save metadata.json.");
            }
            finally
            {
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
    }

    public class PlaytimeEntry
    {
        public int Playtime { get; set; }
        public DateTime LastPlayed { get; set; }
    }
}