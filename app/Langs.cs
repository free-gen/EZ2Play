using System;
using System.Collections.Generic;
using System.Windows;

namespace EZ2Play.App
{
    public static class Langs
    {
        public static bool IsEnglish { get; private set; } = false;
        
        private static readonly Dictionary<string, string> RussianStrings = new Dictionary<string, string>
        {
            ["Launch"] = "Запуск",
            ["Exit"] = "Выход", 
            ["ScreenSwap"] = "Свап экрана",
            // ["CreateCover"] = "Тест",
            
            ["NoShortcutsMessage1"] = "Поместите ваши ярлыки в папку shortcuts",
            ["NoShortcutsMessage2"] = "Для выхода нажмите [ ESC ]",
            
            ["ExitMessage"] = "Assembled by"
        };
        
        private static readonly Dictionary<string, string> EnglishStrings = new Dictionary<string, string>
        {
            ["Launch"] = "Launch",
            ["Exit"] = "Exit",
            ["ScreenSwap"] = "Screen Swap",
            // ["CreateCover"] = "Test",
            
            ["NoShortcutsMessage1"] = "Place your shortcuts in the shortcuts folder",
            ["NoShortcutsMessage2"] = "Press [ ESC ] to exit",
            
            ["ExitMessage"] = "Assembled by"
        };
        
        public static void SetLanguage(bool isEnglish)
        {
            IsEnglish = isEnglish;
        }
        
        public static string GetString(string key)
        {
            var dictionary = IsEnglish ? EnglishStrings : RussianStrings;
            return dictionary.TryGetValue(key, out string value) ? value : key;
        }
        
        public static string GetString(string key, params object[] args)
        {
            var format = GetString(key);
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }

        /// Применяет локализацию ко всем UI элементам окна
        public static void ApplyLocalization(FrameworkElement window)
        {
            try
            {
                // Унификация: применяем к обеим ориентациям по префиксам
                string[] prefixes = new[] { "Vertical", "Horizontal" };
                foreach (var prefix in prefixes)
                {
                    if (window.FindName(prefix + "LaunchText") is System.Windows.Controls.TextBlock launchText)
                        launchText.Text = GetString("Launch");

                    if (window.FindName(prefix + "ExitText") is System.Windows.Controls.TextBlock exitText)
                        exitText.Text = GetString("Exit");

                    if (window.FindName(prefix + "ScreenSwapText") is System.Windows.Controls.TextBlock swapText)
                        swapText.Text = GetString("ScreenSwap");

                    if (window.FindName(prefix + "CreateCoverText") is System.Windows.Controls.TextBlock coverText)
                        coverText.Text = GetString("CreateCover");
                }
                
                // Сообщение об отсутствии ярлыков
                if (window.FindName("NoShortcutsMessage1") is System.Windows.Documents.Run noShortcutsMessage1)
                    noShortcutsMessage1.Text = GetString("NoShortcutsMessage1");
                
                if (window.FindName("NoShortcutsMessage2") is System.Windows.Documents.Run noShortcutsMessage2)
                    noShortcutsMessage2.Text = GetString("NoShortcutsMessage2");
                
                // Сообщение при выходе
                if (window.FindName("ExitMessageRun") is System.Windows.Documents.Run exitMessageRun)
                    exitMessageRun.Text = GetString("ExitMessage");
            }
            catch (Exception ex)
            {
                // Логирование ошибок локализации
                if (EZ2Play.Main.App.EnableLogging)
                {
                    try 
                    { 
                        System.IO.File.AppendAllText("EZ2Play.log", 
                            $"[{DateTime.Now}] Langs: Error applying localization: {ex.Message}\n"); 
                    }
                    catch { /* Ignore */ }
                }
            }
        }
    }
} 