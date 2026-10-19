
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace EZ2Play.App
{
    /// <summary>
    /// Checks GitHub Releases for newer stable EZ2Play versions.
    /// </summary>
    /// <remarks>
    /// Uses the official GitHub API without authentication.
    /// The request runs asynchronously and never blocks startup.
    /// Only versions newer than the installed application qualify.
    /// </remarks>
    internal static class UpdateChecker
    {
        private const string LatestReleaseUrl =
            "https://api.github.com/repos/free-gen/EZ2Play/releases/latest";

        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        static UpdateChecker()
        {
            // GitHub requires a User-Agent header.
            Client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "EZ2Play-UpdateChecker/1.0");

            Client.DefaultRequestHeaders.Accept.ParseAdd(
                "application/vnd.github+json");
        }

        /// <summary>
        /// Returns the latest version when an update is available.
        /// Returns null when the installed version is current
        /// or the release tag cannot be parsed.
        /// </summary>
        public static async Task<string> GetNewerVersionAsync()
        {
            using (var response = await Client.GetAsync(
                LatestReleaseUrl).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                string json = await response.Content
                    .ReadAsStringAsync().ConfigureAwait(false);

                string releaseTag =
                    (string)JObject.Parse(json)["tag_name"];

                if (!TryParseVersion(releaseTag, out Version latest))
                    return null;

                if (!TryParseVersion(AppInfo.Version, out Version installed))
                    return null;

                DebugLog.Write(
                    "Updates",
                    $"Installed={installed}, latest={latest}");

                return latest.CompareTo(installed) > 0
                    ? latest.ToString(3)
                    : null;
            }
        }

        /// <summary>
        /// Parses a numeric version with an optional v prefix.
        /// Missing build and revision components are normalized
        /// to zero before comparing versions.
        /// </summary>
        private static bool TryParseVersion(
            string text,
            out Version version)
        {
            version = null;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string value = text.Trim();

            if (value.StartsWith(
                "v",
                StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(1);
            }

            if (!Version.TryParse(value, out Version parsed))
                return false;

            version = new Version(
                parsed.Major,
                parsed.Minor,
                Math.Max(parsed.Build, 0),
                Math.Max(parsed.Revision, 0));

            return true;
        }
    }
}
