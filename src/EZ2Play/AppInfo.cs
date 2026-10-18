using System.Linq;
using System.Reflection;

namespace EZ2Play.App
{
    /// <summary>
    /// Exposes application metadata stored in assembly attributes.
    /// </summary>
    /// <remarks>
    /// Reads metadata generated from EZ2Play.csproj.
    /// Missing values are returned as empty strings.
    /// </remarks>
    public static class AppInfo
    {
        /// <summary>
        /// Reads an assembly metadata value by its exact key.
        /// </summary>
        /// <param name="key">Assembly metadata key.</param>
        /// <returns>The metadata value, or an empty string if missing.</returns>
        private static string Get(string key)
        {
            return Assembly.GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == key)
                ?.Value ?? string.Empty;
        }

        public static string Name => Get("AppName");
        public static string Description => Get("AppDescription");
        public static string Company => Get("Company");
        public static string Version => Get("FileVersion");
    }
}