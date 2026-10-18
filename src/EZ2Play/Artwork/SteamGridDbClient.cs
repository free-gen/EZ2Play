
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace EZ2Play.App
{
    /// <summary>
    /// Provides access to the SteamGridDB API for searching
    /// games, retrieving artwork, and downloading images.
    /// </summary>
    /// <remarks>
    /// API requests are sent through the EZ2Play Cloudflare
    /// Worker by default.
    ///
    /// When the Worker request fails, the client can fall
    /// back to the direct SteamGridDB API using a configured
    /// authorization key.
    ///
    /// Image downloads use a separate HttpClient and enforce
    /// a maximum download size.
    /// </remarks>
    internal sealed class SteamGridDbClient : IDisposable
    {
        // API endpoints.
        private const string WorkerBaseUrl =
            "https://ez2play.free-gen.workers.dev/api/v2";

        private const string DirectBaseUrl =
            "https://www.steamgriddb.com/api/v2";

        // Maximum permitted image download size: 48 MiB.
        private const int MaxImageBytes = 48 * 1024 * 1024;

        // Separate HTTP clients for Worker requests,
        // direct API requests, and image downloads.
        private readonly HttpClient _workerClient;
        private readonly HttpClient _directApiClient;
        private readonly HttpClient _imageClient;

        // Explicit API key takes precedence over the Worker.
        private string _configuredApiKey;

        public SteamGridDbClient()
        {
            _workerClient = new HttpClient();

            _workerClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                AppInfo.Name + "/" + AppInfo.Version);

            _directApiClient = new HttpClient();

            _directApiClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                AppInfo.Name + "/" + AppInfo.Version);

            _imageClient = new HttpClient();

            _imageClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                AppInfo.Name + "/" + AppInfo.Version);
        }

        /// <summary>
        /// Configures the optional SteamGridDB API key.
        /// </summary>
        /// <remarks>
        /// A configured key enables direct SteamGridDB access
        /// and takes precedence over the Cloudflare Worker.
        ///
        /// An empty key removes the Authorization header
        /// and allows requests through the Worker.
        /// </remarks>
        public void ConfigureAuthorization(string apiKey)
        {
            _configuredApiKey = apiKey?.Trim();

            if (string.IsNullOrWhiteSpace(_configuredApiKey))
            {
                _directApiClient.DefaultRequestHeaders.Authorization = null;
                return;
            }

            _directApiClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _configuredApiKey);
        }

        /// <summary>
        /// Searches SteamGridDB for games matching
        /// the specified text query.
        /// </summary>
        /// <returns>
        /// A list of matching games, limited by maxResults.
        /// Returns an empty list when the API provides no data.
        /// </returns>
        public async Task<List<ParserGameResult>> SearchGamesAsync(
            string query,
            int maxResults,
            CancellationToken cancellationToken)
        {
            string encoded = Uri.EscapeDataString(query.Trim());

            var data = await GetDataAsync(
                $"/search/autocomplete/{encoded}",
                cancellationToken);

            if (data == null)
                return new List<ParserGameResult>();

            // Only results containing both an ID and name
            // can be used by the parser.
            return data
                .Where(item =>
                    item["id"] != null &&
                    item["name"] != null)
                .Take(maxResults)
                .Select(item => new ParserGameResult
                {
                    Id = item["id"].ToObject<int>(),
                    Name = item["name"].ToString()
                })
                .ToList();
        }

        /// <summary>
        /// Retrieves square cover artwork for the
        /// specified SteamGridDB game.
        /// </summary>
        /// <remarks>
        /// Requests static PNG and JPEG images in
        /// 512x512 or 1024x1024 dimensions.
        ///
        /// NSFW artwork is excluded by the API query.
        /// </remarks>
        public async Task<List<ParserGridResult>> GetSquareGridsAsync(
            int gameId,
            int maxResults,
            CancellationToken cancellationToken)
        {
            var data = await GetDataAsync(
                $"/grids/game/{gameId}?dimensions=512x512,1024x1024&mimes=image/png,image/jpeg&nsfw=false&types=static",
                cancellationToken);

            if (data == null)
                return new List<ParserGridResult>();

            return data
                .Where(item => item["url"] != null)
                .Take(maxResults)
                .Select(item => new ParserGridResult
                {
                    Id = item["id"]?.ToObject<int>() ?? 0,
                    Url = item["url"].ToString(),
                    Thumb = item["thumb"]?.ToString()
                        ?? item["url"].ToString()
                })
                .ToList();
        }

        /// <summary>
        /// Retrieves wide hero artwork for the
        /// specified SteamGridDB game.
        /// </summary>
        /// <remarks>
        /// Requests static PNG and JPEG hero images with
        /// dimensions of 3840x1240.
        ///
        /// Results are additionally filtered by minimumWidth.
        /// </remarks>
        public async Task<List<ParserGridResult>> GetHeroesAsync(
            int gameId,
            int maxResults,
            int minimumWidth,
            CancellationToken cancellationToken)
        {
            var data = await GetDataAsync(
                $"/heroes/game/{gameId}?dimensions=3840x1240&mimes=image/png,image/jpeg&nsfw=false&types=static",
                cancellationToken);

            if (data == null)
                return new List<ParserGridResult>();

            return data
                .Where(item =>
                    item["url"] != null &&
                    (item["width"]?.ToObject<int>() ?? 0) >= minimumWidth)
                .Take(maxResults)
                .Select(item => new ParserGridResult
                {
                    Id = item["id"]?.ToObject<int>() ?? 0,
                    Url = item["url"].ToString(),
                    Thumb = item["thumb"]?.ToString()
                        ?? item["url"].ToString(),
                    Width = item["width"]?.ToObject<int>() ?? 0,
                    Height = item["height"]?.ToObject<int>() ?? 0
                })
                .ToList();
        }

        /// <summary>
        /// Downloads an image into a byte array while
        /// enforcing the configured size limit.
        /// </summary>
        /// <remarks>
        /// ResponseHeadersRead allows the response body
        /// to be processed as a stream.
        ///
        /// The size limit is checked both against the
        /// Content-Length header and during streaming.
        ///
        /// Cancellation is respected during the HTTP request
        /// and while reading the response body.
        /// </remarks>
        public async Task<byte[]> DownloadImageAsync(
            string url,
            CancellationToken cancellationToken,
            int maxBytes = MaxImageBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (var response = await _imageClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                long? contentLength =
                    response.Content.Headers.ContentLength;

                // Reject downloads whose declared size
                // exceeds the configured limit.
                if (contentLength.HasValue &&
                    contentLength.Value > maxBytes)
                {
                    throw new InvalidDataException(
                        "Image exceeds the download limit.");
                }

                using (var input = await response.Content
                    .ReadAsStreamAsync().ConfigureAwait(false))
                using (var output = new MemoryStream(
                    contentLength.HasValue && contentLength.Value > 0
                        ? (int)contentLength.Value
                        : 0))
                {
                    byte[] buffer = new byte[64 * 1024];

                    int bytesRead;
                    int totalBytes = 0;

                    // Read the image incrementally, rejecting
                    // data that would exceed the download limit.
                    while ((bytesRead = await input.ReadAsync(
                        buffer,
                        0,
                        buffer.Length,
                        cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        if (bytesRead > maxBytes - totalBytes)
                        {
                            throw new InvalidDataException(
                                "Image exceeds the download limit.");
                        }

                        output.Write(buffer, 0, bytesRead);
                        totalBytes += bytesRead;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    // Preserve the original buffer reuse optimization.
                    return output.Length == output.Capacity
                        ? output.GetBuffer()
                        : output.ToArray();
                }
            }
        }

        /// <summary>
        /// Retrieves SteamGridDB data using the configured
        /// API key or the Cloudflare Worker.
        /// </summary>
        /// <remarks>
        /// An explicit API key always uses direct API access.
        /// Failed direct requests never fall back to the Worker.
        ///
        /// Without a configured key, requests use the Worker.
        /// Worker authorization failures are converted to
        /// SteamGridDbApiKeyMissingException.
        ///
        /// Cancellation exceptions propagate unchanged.
        /// </remarks>
        private async Task<JArray> GetDataAsync(
            string relativeUrl,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Prefer direct API access when a key is configured.
            if (!string.IsNullOrWhiteSpace(_configuredApiKey))
            {
                return await GetDataDirectAsync(
                    relativeUrl,
                    cancellationToken);
            }

            // Use the Worker only when no local key is configured.
            try
            {
                return await GetDataFromWorkerAsync(
                    relativeUrl,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "SteamGridDB",
                    ex,
                    "Cloudflare Worker request failed.");

                if (ex is SteamGridDbAuthException)
                {
                    throw new SteamGridDbApiKeyMissingException(
                        "Worker authorization failed and no API key is configured.",
                        ex);
                }

                throw;
            }
        }

        /// <summary>
        /// Requests data through the EZ2Play Cloudflare Worker.
        /// </summary>
        /// <remarks>
        /// HTTP 401 and 403 responses are represented by
        /// SteamGridDbAuthException.
        ///
        /// Other unsuccessful HTTP status codes produce
        /// HttpRequestException.
        /// </remarks>
        private async Task<JArray> GetDataFromWorkerAsync(
            string relativeUrl,
            CancellationToken cancellationToken)
        {
            using (var response = await _workerClient.GetAsync(
                WorkerBaseUrl + relativeUrl,
                cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new SteamGridDbAuthException(
                        $"Worker returned HTTP {(int)response.StatusCode}.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"EZ2Play SteamGridDB Worker returned HTTP {(int)response.StatusCode}.");
                }

                return await ParseDataAsync(
                    response,
                    cancellationToken);
            }
        }

        /// <summary>
        /// Requests data directly from SteamGridDB
        /// using the configured Authorization header.
        /// </summary>
        private async Task<JArray> GetDataDirectAsync(
            string relativeUrl,
            CancellationToken cancellationToken)
        {
            using (var response = await _directApiClient.GetAsync(
                DirectBaseUrl + relativeUrl,
                cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new SteamGridDbAuthException(
                        $"HTTP {(int)response.StatusCode}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"SteamGridDB returned HTTP {(int)response.StatusCode}.");
                }

                return await ParseDataAsync(
                    response,
                    cancellationToken);
            }
        }

        /// <summary>
        /// Parses the SteamGridDB API response and extracts
        /// its data array.
        /// </summary>
        /// <remarks>
        /// A response with success=false is treated as an error.
        /// Returns null if the data field is not a JSON array.
        /// </remarks>
        private static async Task<JArray> ParseDataAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            string content = await response.Content.ReadAsStringAsync();

            cancellationToken.ThrowIfCancellationRequested();

            var json = JObject.Parse(content);

            if (json["success"]?.ToObject<bool>() != true)
            {
                throw new InvalidOperationException(
                    "SteamGridDB returned success=false.");
            }

            return json["data"] as JArray;
        }

        /// <summary>
        /// Disposes all HTTP clients owned by this instance.
        /// </summary>
        public void Dispose()
        {
            _workerClient.Dispose();
            _directApiClient.Dispose();
            _imageClient.Dispose();
        }
    }

    /// <summary>
    /// Represents an authorization failure returned
    /// by the SteamGridDB Worker or direct API.
    /// </summary>
    internal sealed class SteamGridDbAuthException : Exception
    {
        public SteamGridDbAuthException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Indicates that the Worker authorization failed
    /// and no fallback API key is configured.
    /// </summary>
    internal sealed class SteamGridDbApiKeyMissingException : Exception
    {
        public SteamGridDbApiKeyMissingException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
