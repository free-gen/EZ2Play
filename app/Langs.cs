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
            
            ["NoShortcutsMessage1"] = "Поместите ваши ярлыки в папку shortcuts",
            ["NoShortcutsMessage2"] = "Для выхода нажмите [ ESC ]",
            
            ["ExitMessage"] = "Assembled by"
        };
        
        private static readonly Dictionary<string, string> EnglishStrings = new Dictionary<string, string>
        {
            ["Launch"] = "Launch",
            ["Exit"] = "Exit",
            ["ScreenSwap"] = "Screen Swap",
            
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

        /// <summary>
        /// Применяет локализацию ко всем UI элементам окна
        /// </summary>
        public static void ApplyLocalization(FrameworkElement window)
        {
            try
            {
                // Вертикальная панель
                if (window.FindName("VerticalLaunchText") is System.Windows.Controls.TextBlock verticalLaunchText)
                    verticalLaunchText.Text = GetString("Launch");
                
                if (window.FindName("VerticalExitText") is System.Windows.Controls.TextBlock verticalExitText)
                    verticalExitText.Text = GetString("Exit");
                
                if (window.FindName("VerticalScreenSwapText") is System.Windows.Controls.TextBlock verticalScreenSwapText)
                    verticalScreenSwapText.Text = GetString("ScreenSwap");
                
                // Горизонтальная панель
                if (window.FindName("HorizontalLaunchText") is System.Windows.Controls.TextBlock horizontalLaunchText)
                    horizontalLaunchText.Text = GetString("Launch");
                
                if (window.FindName("HorizontalExitText") is System.Windows.Controls.TextBlock horizontalExitText)
                    horizontalExitText.Text = GetString("Exit");
                
                if (window.FindName("HorizontalScreenSwapText") is System.Windows.Controls.TextBlock horizontalScreenSwapText)
                    horizontalScreenSwapText.Text = GetString("ScreenSwap");
                
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