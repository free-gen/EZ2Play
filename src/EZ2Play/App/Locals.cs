using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Reflection;

namespace EZ2Play.App
{
    public static class Locals
    {
        // --------------- НАСТРОЙКА ---------------

        // null = System
        // 0 = English
        // 1 = Russian
        // 2 = German
        // 3 = French
        // 4 = Chinese (Simplified)
        private static int? _forceLang = null;

        // --------------- Состояние ---------------

        private static int _currentLang = 0;

        // --------------- Словарь ---------------

        private static readonly Dictionary<string, string[]> Translations =
            new Dictionary<string, string[]>
            {
                ["Launch"] = new[] 
                { 
                    "Launch", "Запуск", 
                    "Starten", "Lancer", "启动"
                },
                ["Select"] = new[] 
                { 
                    "Select", "Выбрать",
                    "Auswählen", "Sélectionner", "选择"
                },
                ["SwitchTabs"] = new[] 
                { 
                    "Switch tabs", "Переключение вкладок",
                    "Tabs wechseln", "Changer d’onglet", "切换标签页"
                },
                ["Back"] = new[] 
                { 
                    "Back", "Назад",
                    "Zurück", "Retour", "返回"
                },
                ["Exit"] = new[] 
                { 
                    "Exit", "Выход", 
                    "Beenden", "Quitter", "退出"
                },
                ["SettingsOverlay"] = new[] 
                { 
                    "Settings", "Настройки", 
                    "Einstellungen", "Paramètres", "设置"
                },
                ["ScreenSwap"] = new[] 
                { 
                    "Screen swap", "Свап дисплея", 
                    "Bildschirm tauschen", "Écran swap", "屏幕交换"
                },

                // Settings
                ["SettingsSourceDisplayLabel"] = new[]
                {
                    "Switch Display", "Переключить дисплей",
                    "Anzeige wechseln", "Changer d’affichage", "切换显示器"
                },

                ["SettingsSourceDisplayDesc"] = new[]
                {
                    "Switch image output to an external monitor or TV.",
                    "Переключить вывод изображения на внешний монитор или телевизор.",
                    "Bildausgabe auf einen externen Monitor oder Fernseher umschalten.",
                    "Basculer l’affichage vers un moniteur externe ou un téléviseur.",
                    "将图像输出切换到外部显示器或电视。"
                },

                ["SettingsAutorunAppLabel"] = new[]
                {
                    "Run launcher using a gamepad", "Запускать лаунчер при помощи геймпада",
                    "Launcher mit einem Gamepad starten", "Lancer le lanceur avec une manette", "使用手柄启动启动器"
                },

                ["SettingsAutorunAppDesc"] = new[]
                {
                    "Hold {ICON} to start the launcher.\nThis option starts a background process and adds it to startup.",
                    "Удерживайте {ICON} для запуска лаунчера.\nЭтот параметр активирует фоновый процесс и добавляет его в автозагрузку.",
                    "Halten Sie {ICON} gedrückt, um den Launcher zu starten.\nDiese Option startet einen Hintergrundprozess und fügt ihn dem Autostart hinzu.",
                    "Maintenez {ICON} enfoncé pour lancer le lanceur.\nCette option démarre un processus en arrière-plan et l'ajoute au démarrage automatique.",
                    "按住 {ICON} 启动启动器。\n此选项会启动后台进程并将其添加到开机启动。"
                },

                ["SettingsExitAppLabel"] = new[]
                {
                    "Exit to Desktop", "Выход на рабочий стол",
                    "Zum Desktop", "Retour au bureau", "返回桌面"
                },

                ["SettingsTreeNoSplash"] = new[]
                {
                    "Skip Splash", "Без заставки",
                    "Splash überspringen", "Ignorer l’écran", "跳过启动画面"
                },

                ["SettingsTreeNoMusic"] = new[]
                {
                    "No Music", "Без музыки",
                    "Ohne Musik", "Sans musique", "无音乐"
                },

                ["SettingsTreeHotSwap"] = new[]
                {
                    "External Display", "Внешний дисплей",
                    "Externer Bildschirm", "Écran externe", "外接显示器"
                },

                ["ConfirmYes"] = new[] 
                { 
                    "Yes", "Да", 
                    "Ja", "Oui", "是"
                },

                ["ConfirmNo"] = new[] 
                { 
                    "No", "Нет", 
                    "Nein", "Non", "否"
                },

                ["ExitConfirmText"] = new[] 
                { 
                    "Switch the display to the main one?",
                    "Переключить дисплей на основной?",
                    "Display auf den Hauptbildschirm umschalten?",
                    "Changer l'affichage sur l'écran principal ?",
                    "将显示切换到主屏幕？"
                },

                ["NoShortcutsMessageTop"] = new[]
                {
                    "Place your shortcuts in the shortcuts folder and restart the application.",
                    "Поместите ваши ярлыки в папку shortcuts и запустите приложение заново.",
                    "Legen Sie Ihre Verknüpfungen im Ordner shortcuts ab und starten Sie die Anwendung neu.",
                    "Placez vos raccourcis dans le dossier shortcuts et redémarrez l'application.",
                    "请将快捷方式放入 shortcuts 文件夹并重新启动应用程序。"
                },

                ["NoShortcutsMessageBottom"] = new[]
                {
                    "Press Esc to exit",
                    "Для выхода нажмите Esc.",
                    "Drücken Sie Esc zum Beenden",
                    "Appuyez sur Échap pour quitter",
                    "按 Esc 键退出"
                },

                ["MessageHotSwap"] = new[]
                {
                    "Application launched in HotSwap mode!\nThe display will revert upon exit.",
                    "Приложение запущено в HotSwap режиме!\nПри выходе дисплей вернется к исходному.",
                    "Anwendung im HotSwap-Modus gestartet!\nBeim Beenden wird die ursprüngliche Anzeige wiederhergestellt.",
                    "Application lancée en mode HotSwap!\nL'écran reviendra à son état d'origine à la sortie.",
                    "应用程序已在 HotSwap 模式下启动！\n退出后显示将恢复原始状态。"
                },

                ["MessagePlugGamepad"] = new[]
                {
                    "Input device detected:",
                    "Обнаружено устройство ввода:",
                    "Ein Eingabegerät wurde erkannt:",
                    "Un périphérique d'entrée a été détecté :",
                    "已检测到输入设备："
                },

                ["MessageGameBarDetected"] = new[]
                {
                    "XBOX Game Bar detected.\nGame and display control is handled by the system.",
                    "XBOX Game Bar обнаружен.\nУправление играми и дисплеем осуществляется системой.",
                    "XBOX Game Bar erkannt.\nDie Steuerung von Spielen und Anzeige erfolgt durch das System.",
                    "XBOX Game Bar détecté.\nLe contrôle des jeux et de l'affichage est assuré par le système.",
                    "检测到 XBOX Game Bar。\n游戏和显示控制由系统处理。"
                },

                ["MessageGameBarNotDetected"] = new[]
                {
                    "XBOX Game Bar not detected.\nGame and display control is handled by the application.",
                    "XBOX Game Bar не обнаружен.\nУправление играми и дисплеем осуществляется приложением.",
                    "XBOX Game Bar nicht erkannt.\nDie Steuerung von Spielen und Anzeige erfolgt durch die Anwendung.",
                    "XBOX Game Bar non détecté.\nLe contrôle des jeux et de l'affichage est assuré par l'application.",
                    "未检测到 XBOX Game Bar。\n游戏和显示控制由应用程序处理。"
                },

                ["MessageTest"] = new[]
                {
                    "Debug notification:\nUsed for configuration and testing. Does not affect anything.",
                    "Отладочное уведомление:\nИспользуется для настройки и тестирования. Ни на что не влияет.",
                    "Debug-Benachrichtigung:\nWird für Konfiguration und Tests verwendet. Beeinflusst nichts.",
                    "Notification de débogage :\nUtilisé pour la configuration et les tests. N'affecte rien.",
                    "调试通知：\n用于配置和测试。不影响任何内容。"
                },

                ["TabGamelistText"] = new[] 
                { 
                    "Library", "Библиотека", 
                    "Bibliothek", "Bibliothèque", "游戏库"
                },

                ["TabLastPlayedText"] = new[] 
                { 
                    "Recent games", "Недавние игры", 
                    "Letzte Spiele", "Jeux récents", "最近的游戏"
                },
                
                ["HoursShort"] = new[] 
                { 
                    "h", "ч", 
                    "h", "h", "时"
                },
                ["MinutesShort"] = new[] 
                { 
                    "m", "м", 
                    "m", "m", "分"
                }
            };

        // --------------- ИНИЦИАЛИЗАЦИЯ ---------------

        public static void Init()
        {
            if (_forceLang.HasValue)
            {
                _currentLang = _forceLang.Value;
                return;
            }

            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            
            if (lang == "ru")
                _currentLang = 1;
            else if (lang == "de")
                _currentLang = 2;
            else if (lang == "fr")
                _currentLang = 3;
            else if (lang == "zh" || lang == "zh-CN" || lang == "zh-Hans")
                _currentLang = 4;
            else
                _currentLang = 0;
        }

        // --------------- API ---------------

        public static string GetString(string key)
        {
            if (Translations.TryGetValue(key, out var values))
            {
                if (values.Length == 1)
                    return values[0];
                
                if (values.Length > _currentLang)
                    return values[_currentLang];
            }
            
            return $"[{key}]";
        }

        // GameMetadata Helper
        public static string GetFormattedTime(int value, bool isHours)
        {
            string unit = isHours ? GetString("HoursShort") : GetString("MinutesShort");
            return $"{value}{unit}";
        }

        // --------------- УМНАЯ ЛОКАЛИЗАЦИЯ ---------------

        public static void ApplyLocalization(FrameworkElement window)
        {
            try
            {
                var fields = window.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                
                foreach (var field in fields)
                {
                    var value = field.GetValue(window);
                    if (value == null) continue;

                    string name = field.Name;
                    
                    string key = name;
                    
                    if (!Translations.ContainsKey(key) && key.EndsWith("Text"))
                        key = key.Substring(0, key.Length - 4);

                    if (!Translations.ContainsKey(key))
                        continue;

                    string translatedText = GetString(key);

                    if (value is System.Windows.Controls.TextBlock textBlock)
                        textBlock.Text = translatedText;
                    else if (value is System.Windows.Documents.Run run)
                        run.Text = translatedText;
                }
            }
            catch { }
        }
    }
}