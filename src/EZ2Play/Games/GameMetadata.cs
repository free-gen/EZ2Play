
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace EZ2Play.App
{
    /// <summary>
    /// Manages game playtime statistics and launch history.
    /// Stores metadata in the application's roaming AppData directory.
    /// </summary>
    /// <remarks>
    /// Supports the current metadata format, legacy playtime records,
    /// and recovery from a backup file.
    /// </remarks>
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
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, AppInfo.Name);

            _filePath = Path.Combine(folder, "metadata.json");
            _backupPath = _filePath + ".bak";

            Load();
        }

        /// <summary>
        /// Returns the last recorded launch time of a game.
        /// </summary>
        public DateTime GetLastPlayed(string gameId)
        {
            gameId = NormalizeGameId(gameId);

            if (_data.ContainsKey(gameId))
                return _data[gameId].LastPlayed;

            return DateTime.MinValue;
        }

        /// <summary>
        /// Starts tracking a game session and records its launch time.
        /// </summary>
        public void Start(string gameId)
        {
            _currentGameId = NormalizeGameId(gameId);
            _startTime = DateTime.Now;
            _isRunning = true;

            if (!_data.ContainsKey(_currentGameId))
            {
                _data[_currentGameId] = new PlaytimeEntry();
            }

            // LastPlayed records a successful launch.
            // Start is called only after Process.Start() succeeds.
            _data[_currentGameId].LastPlayed = _startTime;

            Save();
        }

        /// <summary>
        /// Stops the current game session and updates its playtime.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning || _currentGameId == null) return;

            var session = DateTime.Now - _startTime;

            // Count playtime only for sessions lasting at least 10 seconds.
            if (session.TotalSeconds >= 10)
            {
                if (!_data.ContainsKey(_currentGameId))
                {
                    _data[_currentGameId] = new PlaytimeEntry();
                }

                _data[_currentGameId].Playtime += (int)session.TotalSeconds;

                Save();
            }

            _isRunning = false;
            _currentGameId = null;
        }

        /// <summary>
        /// Returns the accumulated playtime of a game in seconds.
        /// </summary>
        public int GetSeconds(string gameId)
        {
            gameId = NormalizeGameId(gameId);

            if (_data.ContainsKey(gameId))
                return _data[gameId].Playtime;

            return 0;
        }

        /// <summary>
        /// Converts accumulated playtime into a value and unit
        /// suitable for display in the user interface.
        /// </summary>
        /// <remarks>
        /// Values below one hour are rounded up to whole minutes.
        /// Hour values are rounded up when remaining minutes
        /// reach 50, matching the original display behavior.
        /// </remarks>
        public (int value, bool isHours) GetFormattedValue(string gameId)
        {
            int seconds = GetSeconds(gameId);

            if (seconds == 0)
                return (0, false);

            var ts = TimeSpan.FromSeconds(seconds);

            if (ts.TotalHours >= 1)
            {
                int hours = (int)ts.TotalHours;
                int minutes = ts.Minutes;

                if (minutes >= 50)
                    hours++;

                return (hours, true);
            }

            int totalMinutes = (int)Math.Ceiling(ts.TotalMinutes);

            return (totalMinutes, false);
        }

        /// <summary>
        /// Uses the shortcut file name as the persistent game identifier.
        /// </summary>
        private string NormalizeGameId(string path)
        {
            return Path.GetFileName(path);
        }

        /// <summary>
        /// Loads game metadata from the primary file or its backup.
        /// </summary>
        private void Load()
        {
            Dictionary<string, PlaytimeEntry> loaded;

            if (TryLoadFile(_filePath, out loaded))
            {
                _data = loaded;
                return;
            }

            if (TryLoadFile(_backupPath, out loaded))
            {
                _data = loaded;

                DebugLog.Write("Metadata", "metadata.json could not be loaded. Backup recovered successfully.");

                RestorePrimaryFromBackup();

                return;
            }

            _data = new Dictionary<string, PlaytimeEntry>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(_filePath) || File.Exists(_backupPath))
            {
                DebugLog.Write("Metadata", "metadata.json and backup could not be loaded. Empty metadata was used.");
            }
        }

        /// <summary>
        /// Attempts to deserialize metadata using the current format,
        /// falling back to the legacy format when necessary.
        /// </summary>
        private bool TryLoadFile(string path, out Dictionary<string, PlaytimeEntry> result)
        {
            result = null;

            if (!File.Exists(path)) return false;

            try
            {
                string json = File.ReadAllText(path);

                // Try the current metadata format first.
                try
                {
                    var current = JsonConvert.DeserializeObject<Dictionary<string, PlaytimeEntry>>(json);

                    if (current != null && ValidateEntries(current))
                    {
                        result = new Dictionary<string, PlaytimeEntry>(current, StringComparer.OrdinalIgnoreCase);
                        return true;
                    }
                }
                catch
                {
                    // Fall back to the legacy format below.
                }

                // Legacy format: { "game.lnk": 1234 }
                var legacy = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);

                if (legacy == null) return false;

                foreach (var entry in legacy)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value < 0)
                        return false;
                }

                result = new Dictionary<string, PlaytimeEntry>(StringComparer.OrdinalIgnoreCase);

                foreach (var kv in legacy)
                {
                    result[kv.Key] = new PlaytimeEntry
                    {
                        Playtime = kv.Value,
                        LastPlayed = DateTime.MinValue
                    };
                }

                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Error("Metadata", ex, $"Failed to load {Path.GetFileName(path)}.");

                return false;
            }
        }

        /// <summary>
        /// Validates game identifiers and playtime values
        /// in deserialized metadata records.
        /// </summary>
        private bool ValidateEntries(Dictionary<string, PlaytimeEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                    return false;

                if (entry.Value.Playtime < 0)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Restores the primary metadata file from its backup.
        /// </summary>
        /// <remarks>
        /// A recovery temporary file is used so that the original
        /// backup is not modified during restoration.
        /// </remarks>
        private void RestorePrimaryFromBackup()
        {
            string recoveryTemp = _filePath + ".recovery.tmp";

            try
            {
                File.Copy(_backupPath, recoveryTemp, true);

                if (File.Exists(_filePath))
                {
                    File.Replace(recoveryTemp, _filePath, null);
                }
                else
                {
                    File.Move(recoveryTemp, _filePath);
                }

                DebugLog.Write("Metadata", "Primary metadata.json restored from backup.");
            }
            catch (Exception ex)
            {
                DebugLog.Error("Metadata", ex, "Failed to restore primary metadata.json from backup.");
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

        /// <summary>
        /// Saves game statistics using a temporary file and
        /// preserves the previous metadata as a backup.
        /// </summary>
        private void Save()
        {
            string tempPath = _filePath + ".tmp";

            try
            {
                string folder = Path.GetDirectoryName(_filePath);

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var sorted = new SortedDictionary<string, PlaytimeEntry>(
                    _data,
                    StringComparer.OrdinalIgnoreCase);

                var settings = new JsonSerializerSettings
                {
                    DateFormatString = "yyyy-MM-dd HH:mm:ss"
                };

                string json = JsonConvert.SerializeObject(
                    sorted,
                    Formatting.Indented,
                    settings);

                File.WriteAllText(tempPath, json);

                if (File.Exists(_filePath))
                {
                    // Replace metadata.json and preserve the previous file as metadata.json.bak.
                    File.Replace(tempPath, _filePath, _backupPath);
                }
                else
                {
                    File.Move(tempPath, _filePath);
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error("Metadata", ex, "Failed to save metadata.json.");
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

    /// <summary>
    /// Represents the persistent playtime and last launch time
    /// of a single game.
    /// </summary>
    public class PlaytimeEntry
    {
        public int Playtime { get; set; }
        public DateTime LastPlayed { get; set; }
    }
}
