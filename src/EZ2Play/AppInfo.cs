using System.Linq;
using System.Reflection;

namespace EZ2Play.App
{
    // --------------- Информация о приложении из AssemblyInfo ---------------

    public static class AppInfo
    {
        // --------------- Приватные методы ---------------

        // Получение значения атрибута по ключу
        private static string Get(string key)
        {
            return Assembly.GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == key)
                ?.Value ?? string.Empty;
        }

        // --------------- Публичные свойства ---------------

        public static string Name        => Get("AppName");
        public static string Description => Get("AppDescription");
        public static string Company     => Get("Company");
        public static string Version     => Get("FileVersion");
    }
}