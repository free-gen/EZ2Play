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
        private const string WorkerBaseUrl = "https://ez2play.helios-kms.workers.dev/api/v2";
        private const string DirectBaseUrl = "https://www.steamgriddb.com/api/v2";

        private readonly HttpClient _workerClient;
        private readonly HttpClient _directApiClient;
        private readonly HttpClient _imageClient;

        private string _fallbackApiKey;

        public SteamGridDbClient()
        {
            _workerClient = new HttpClient();
            _workerClient.DefaultRequestHeaders.UserAgent.ParseAdd("EZ2Play/1.1");

            _directApiClient = new HttpClient();
            _directApiClient.DefaultRequestHeaders.UserAgent.ParseAdd("EZ2Play/1.1");

            _imageClient = new HttpClient();
            _imageClient.DefaultRequestHeaders.UserAgent.ParseAdd("EZ2Play/1.1");
        }

        public void ConfigureFallbackAuthorization(string fallbackApiKey)
        {
            _fallbackApiKey = fallbackApiKey?.Trim();

            if (string.IsNullOrWhiteSpace(_fallbackApiKey))
            {
                _directApiClient.DefaultRequestHeaders.Authorization = null;
                return;
            }

            _directApiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _fallbackApiKey);
        }

        public async Task<List<ParserGameResult>> SearchGamesAsync(string query, int maxResults, CancellationToken cancellationToken)
        {
            string encoded = Uri.EscapeDataString(query.Trim());
            var data = await GetDataAsync($"/search/autocomplete/{encoded}", cancellationToken);

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
            var data = await GetDataAsync($"/grids/game/{gameId}?dimensions=512x512,1024x1024&mimes=image/png,image/jpeg&nsfw=false&types=static", cancellationToken);

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
            var data = await GetDataAsync($"/heroes/game/{gameId}?dimensions=3840x1240&mimes=image/png,image/jpeg&nsfw=false&types=static", cancellationToken);

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

        private async Task<JArray> GetDataAsync(string relativeUrl, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await GetDataFromWorkerAsync(relativeUrl, cancellationToken);
            }

            catch (OperationCanceledException)
            {
                throw;
            }

            catch (Exception ex)
            {
                DebugLog.Error("SteamGridDB", ex, "Cloudflare Worker request failed. Trying local API key fallback.");

                if (string.IsNullOrWhiteSpace(_fallbackApiKey))
                    throw;
            }

            return await GetDataDirectAsync(relativeUrl, cancellationToken);
        }

        private async Task<JArray> GetDataFromWorkerAsync(string relativeUrl, CancellationToken cancellationToken)
        {
            using (var response = await _workerClient.GetAsync(WorkerBaseUrl + relativeUrl, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    throw new SteamGridDbAuthException($"Worker returned HTTP {(int)response.StatusCode}.");

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"EZ2Play SteamGridDB Worker returned HTTP {(int)response.StatusCode}.");

                return await ParseDataAsync(response, cancellationToken);
            }
        }

        private async Task<JArray> GetDataDirectAsync(string relativeUrl, CancellationToken cancellationToken)
        {
            using (var response = await _directApiClient.GetAsync(DirectBaseUrl + relativeUrl, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    throw new SteamGridDbAuthException($"HTTP {(int)response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"SteamGridDB returned HTTP {(int)response.StatusCode}.");

                return await ParseDataAsync(response, cancellationToken);
            }
        }

        private static async Task<JArray> ParseDataAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            string content = await response.Content.ReadAsStringAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var json = JObject.Parse(content);

            if (json["success"]?.ToObject<bool>() != true)
                throw new InvalidOperationException("SteamGridDB returned success=false.");

            return json["data"] as JArray;
        }

        public void Dispose()
        {
            _workerClient.Dispose();
            _directApiClient.Dispose();
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