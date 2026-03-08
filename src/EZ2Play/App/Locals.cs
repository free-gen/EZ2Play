using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace EZ2Play.App
{
    public static class Locals
    {
        // 0 = English
        // 1 = Russian
        private static int _currentLang = 0;

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

            // ["MsgHotSwap"] = new[]
            // {
            //     "Launching in hotswap mode: when you exit, the display will automatically switch back to the original display.\nDo not switch the display yourself before exiting!",
            //     "Запуск в режиме hotswap: при выходе произойдёт автоматическое переключение на исходный дисплей.\nНе переключайте дисплей самостоятельно перед выходом!"
            // },

            ["ExitMessage"] = new[]
            {
                "Assembled by",
                "Assembled by"
            }
        };

        // Автоопределение языка из системы
        public static void InitFromSystem()
        {
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            _currentLang = lang == "ru" ? 1 : 0;
        }

        // Ручная установка языка
        // public static void SetLanguage(string langCode)
        // {
        //     _currentLang = !string.IsNullOrEmpty(langCode) && langCode.StartsWith("ru")
        //         ? 1
        //         : 0;
        // }

        public static string GetString(string key)
        {
            if (Translations.TryGetValue(key, out var values) && values.Length > _currentLang)
                return values[_currentLang];

            return $"[{key}]";
        }

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

                if (window.FindName("ExitMessageRun") is System.Windows.Documents.Run exitMsg)
                    exitMsg.Text = GetString("ExitMessage");
            }
            catch { }
        }
    }
}