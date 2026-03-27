using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace EZ2Play.App
{
    // --------------- Локализация текста приложения ---------------

    public static class Locals
    {
        // --------------- Настройки ---------------

        // 0 = English, 1 = Russian
        private static int _currentLang = 0;

        // Временный проброс текста для тестов
        public static string MessageHotSwap => GetString("MessageHotSwap");

        // --------------- Словарь переводов ---------------

        private static readonly Dictionary<string, string[]> Translations =
            new Dictionary<string, string[]>
            {
                ["Launch"] = new[] { "Launch", "Запуск" },
                ["Exit"] = new[] { "Exit", "Выход" },
                ["ScreenSwap"] = new[] { "Screen Swap", "Свап дисплея" },

                ["NoShortcutsMessage1"] = new[]
                {
                    "Place your shortcuts in the shortcuts folder and restart the application.",
                    "Поместите ваши ярлыки в папку shortcuts и запустите приложение заново."
                },

                ["NoShortcutsMessage2"] = new[]
                {
                    "Press Esc to exit",
                    "Для выхода нажмите Esc."
                },

                ["MessageHotSwap"] = new[]
                {
                    "Application launched in HotSwap mode!\nThe display will revert upon exit.",
                    "Приложение запущено в HotSwap режиме!\nПри выходе дисплей вернется к исходному."
                },

                ["MessagePlugGamepad"] = new[]
                {
                    "An input device has been detected:",
                    "Обнаружено устройство ввода:"
                },

                ["MessageTest"] = new[]
                {
                    "Debug notification:\nUsed for configuration and testing. Does not affect anything.",
                    "Отладочное уведомление:\nИспользуется для настройки и тестов. Ни на что не влияет."
                },

                ["ExitMessage"] = new[]
                {
                    "Assembled by",
                    "Assembled by"
                },

                ["HoursShort"] = new[] { "h", "ч" },
                
                ["MinutesShort"] = new[] { "m", "м" }
            };

        // --------------- Инициализация ---------------

        // Автоопределение языка из системы
        public static void InitFromSystem()
        {
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            _currentLang = lang == "ru" ? 1 : 0;
        }

        // --------------- Публичные методы ---------------

        // Получение строки по ключу
        public static string GetString(string key)
        {
            if (Translations.TryGetValue(key, out var values) && values.Length > _currentLang)
                return values[_currentLang];

            return $"[{key}]";
        }

        // Получение форматированного времени для отображения
        public static string GetFormattedTime(int value, bool isHours)
        {
            string unit = isHours ? GetString("HoursShort") : GetString("MinutesShort");
            return $"{value}{unit}";
        }

        // Применение локализации к окну
        public static void ApplyLocalization(FrameworkElement window)
        {
            try
            {
                if (window.FindName("LaunchText") is System.Windows.Controls.TextBlock launchText)
                    launchText.Text = GetString("Launch");

                if (window.FindName("ExitText") is System.Windows.Controls.TextBlock exitText)
                    exitText.Text = GetString("Exit");

                if (window.FindName("ScreenSwapText") is System.Windows.Controls.TextBlock swapText)
                    swapText.Text = GetString("ScreenSwap");

                if (window.FindName("NoShortcutsMessage1") is System.Windows.Documents.Run msg1)
                    msg1.Text = GetString("NoShortcutsMessage1");

                if (window.FindName("NoShortcutsMessage2") is System.Windows.Documents.Run msg2)
                    msg2.Text = GetString("NoShortcutsMessage2");

                if (window.FindName("MessageHotSwapRun") is System.Windows.Documents.Run hotswapMsg)
                    hotswapMsg.Text = GetString("MessageHotSwap");

                if (window.FindName("MessageTest") is System.Windows.Documents.Run testMsg)
                    testMsg.Text = GetString("MessageTest");

                if (window.FindName("ExitMessageRun") is System.Windows.Documents.Run exitMsg)
                    exitMsg.Text = GetString("ExitMessage");
            }
            catch { }
        }
    }
}