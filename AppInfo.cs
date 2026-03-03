using System;
using System.Linq;
using System.Reflection;

namespace EZ2Play.App
{
    public static class AppInfo
    {
        private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();

        private static string GetMetadata(string key)
        {
            return _assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == key)
                ?.Value ?? string.Empty;
        }

        private static string GetAttribute<T>() where T : Attribute
        {
            return _assembly
                .GetCustomAttribute<T>()?
                .ToString() ?? string.Empty;
        }

        public static string GetProductName()
        {
            return GetMetadata("AppName");
        }

        public static string GetProductDescription()
        {
            return GetMetadata("AppDescription");
        }

        public static string GetVersion(bool shortFormat = false)
        {
            var version = GetMetadata("FileVersion");

            if (shortFormat && Version.TryParse(version, out var v))
                return $"{v.Major}.{v.Minor}.{v.Build}";

            return version;
        }

        public static string GetCompanyName()
        {
            return GetMetadata("Company");
        }
    }
}