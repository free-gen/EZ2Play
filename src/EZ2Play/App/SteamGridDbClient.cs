using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace EZ2Play.App
{
    internal sealed class SteamGridDbClient : IDisposable
    {
        private const string BaseUrl = "https://www.steamgriddb.com/api/v2";

        private readonly HttpClient _apiClient;
        private readonly HttpClient _imageClient;

        public SteamGridDbClient()
        {
            _apiClient = new HttpClient();
            _apiClient.DefaultRequestHeaders.UserAgent.ParseAdd("EZ2Play/1.0");

            _imageClient = new HttpClient();
            _imageClient.DefaultRequestHeaders.UserAgent.ParseAdd("EZ2Play/1.0");
        }

        public bool ConfigureAuthorization(string primaryApiKey, string fallbackApiKey)
        {
            string apiKey = !string.IsNullOrWhiteSpace(primaryApiKey)
                ? primaryApiKey.Trim()
                : fallbackApiKey?.Trim();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _apiClient.DefaultRequestHeaders.Authorization = null;
                return false;
            }

            _apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            return true;
        }

        public async Task<List<ParserGameResult>> SearchGamesAsync(string query, int maxResults, CancellationToken cancellationToken)
        {
            string encoded = Uri.EscapeDataString(query.Trim());
            var data = await GetDataAsync($"{BaseUrl}/search/autocomplete/{encoded}", cancellationToken);

            if (data == null)
                return new List<ParserGameResult>();

            return data
                .Where(item => item["id"] != null && item["name"] != null)
                .Take(maxResults)
                .Select(item => new ParserGameResult
                {
                    Id = item["id"].ToObject<int>(),
                    Name = item["name"].ToString()
                })
                .ToList();
        }

        public async Task<List<ParserGridResult>> GetSquareGridsAsync(int gameId, int maxResults, CancellationToken cancellationToken)
        {
            var data = await GetDataAsync($"{BaseUrl}/grids/game/{gameId}?dimensions=512x512,1024x1024&mimes=image/png,image/jpeg&nsfw=false&types=static", cancellationToken);

            if (data == null)
                return new List<ParserGridResult>();

            return data
                .Where(item => item["url"] != null)
                .Take(maxResults)
                .Select(item => new ParserGridResult
                {
                    Id = item["id"]?.ToObject<int>() ?? 0,
                    Url = item["url"].ToString(),
                    Thumb = item["thumb"]?.ToString() ?? item["url"].ToString()
                })
                .ToList();
        }

        public async Task<List<ParserGridResult>> GetHeroesAsync(int gameId, int maxResults, int minimumWidth, CancellationToken cancellationToken)
        {
            var data = await GetDataAsync($"{BaseUrl}/heroes/game/{gameId}?dimensions=3840x1240&mimes=image/png,image/jpeg&nsfw=false&types=static", cancellationToken);

            if (data == null)
                return new List<ParserGridResult>();

            return data
                .Where(item => item["url"] != null && (item["width"]?.ToObject<int>() ?? 0) >= minimumWidth)
                .Take(maxResults)
                .Select(item => new ParserGridResult
                {
                    Id = item["id"]?.ToObject<int>() ?? 0,
                    Url = item["url"].ToString(),
                    Thumb = item["thumb"]?.ToString() ?? item["url"].ToString(),
                    Width = item["width"]?.ToObject<int>() ?? 0,
                    Height = item["height"]?.ToObject<int>() ?? 0
                })
                .ToList();
        }

        public async Task<byte[]> DownloadImageAsync(string url, CancellationToken cancellationToken)
        {
            using (var response = await _imageClient.GetAsync(url, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                cancellationToken.ThrowIfCancellationRequested();

                return bytes;
            }
        }

        private async Task<JArray> GetDataAsync(string url, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (var response = await _apiClient.GetAsync(url, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    throw new SteamGridDbAuthException($"HTTP {(int)response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"SteamGridDB returned HTTP {(int)response.StatusCode}.");

                string content = await response.Content.ReadAsStringAsync();
                cancellationToken.ThrowIfCancellationRequested();

                var json = JObject.Parse(content);

                if (json["success"]?.ToObject<bool>() != true)
                    throw new InvalidOperationException("SteamGridDB returned success=false.");

                return json["data"] as JArray;
            }
        }

        public void Dispose()
        {
            _apiClient.Dispose();
            _imageClient.Dispose();
        }
    }

    internal sealed class SteamGridDbAuthException : Exception
    {
        public SteamGridDbAuthException(string message) : base(message)
        {
        }
    }
}