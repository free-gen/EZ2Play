using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using EZParser.Models;

namespace EZParser.Services
{
    public class SteamGridDBApi
    {
        private const string BaseUrl = "https://www.steamgriddb.com/api/v2";
        private readonly HttpClient _httpClient;

        public SteamGridDBApi(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key is required", nameof(apiKey));

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SteamGridCoverDownloader/2.0");
        }

        public async Task<List<string>> SearchGamesAsync(string query)
        {
            var encoded = Uri.EscapeDataString(query.Trim());
            var endpoints = new[]
            {
                $"/search/games/{encoded}",
                $"/search/autocomplete/{encoded}"
            };

            foreach (var endpoint in endpoints)
            {
                try
                {
                    var url = BaseUrl + endpoint;
                    var response = await _httpClient.GetAsync(url);
                    
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        continue;

                    var content = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<JObject>(content);

                    var games = new List<string>();

                    if (data["success"]?.ToObject<bool>() == true && data["data"] != null)
                    {
                        var items = data["data"];
                        
                        if (items is JArray array)
                        {
                            foreach (var item in array)
                            {
                                if (item["id"] != null && item["name"] != null)
                                {
                                    games.Add($"{item["name"]} (ID: {item["id"]})");
                                }
                            }
                        }
                        else if (items["id"] != null && items["name"] != null)
                        {
                            games.Add($"{items["name"]} (ID: {items["id"]})");
                        }
                    }

                    if (games.Count > 0)
                        return games.GetRange(0, Math.Min(30, games.Count));
                }
                catch
                {
                    continue;
                }
            }

            return new List<string>();
        }

        public async Task<List<GridResult>> GetSquareGridsAsync(int gameId, string gameName, int limit = 60)
        {
            try
            {
                var url = $"{BaseUrl}/grids/game/{gameId}";
                var queryParams = $"?dimensions=512x512,1024x1024&nsfw=false&types=static&limit={limit}";
                
                var response = await _httpClient.GetAsync(url + queryParams);
                
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    return new List<GridResult>();

                var content = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<JObject>(content);

                var grids = new List<GridResult>();

                if (data["success"]?.ToObject<bool>() == true && data["data"] != null)
                {
                    foreach (var item in data["data"])
                    {
                        grids.Add(new GridResult
                        {
                            Id = item["id"].ToObject<int>(),
                            Url = item["url"]?.ToString(),
                            Thumb = item["thumb"]?.ToString() ?? item["url"]?.ToString(),
                            Style = item["style"]?.ToString() ?? "grid",
                            Author = item["author"]?["name"]?.ToString() ?? "Unknown",
                            Name = gameName // Используем переданное имя игры
                        });
                    }
                }

                return grids;
            }
            catch
            {
                return new List<GridResult>();
            }
        }
    }
}