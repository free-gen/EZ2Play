using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Diagnostics;

namespace EZ2Play.App
{
    public static class Locals
    {
        // null = System
        // 0 = English
        // 1 = Russian
        // 2 = German
        // 3 = French
        // 4 = Chinese (Simplified)
        private static int? _forceLang = null;

        private static int _currentLang = 0;

        private static readonly Dictionary<string, string[]> Translations = new Dictionary<string, string[]>
        {
            // Hints
            ["Launch"] = new[]
            {
                "Launch", "Запуск", "Starten", "Lancer", "启动"
            },

            ["Select"] = new[]
            {
                "Select", "Выбрать", "Auswählen", "Sélectionner", "选择"
            },

            ["SwitchTabs"] = new[]
            {
                "Switch tabs", "Переключение вкладок",
                "Tabs wechseln", "Changer d’onglet",
                "切换标签页"
            },

            ["Back"] = new[]
            {
                "Back", "Назад", "Zurück", "Retour", "返回"
            },

            ["Exit"] = new[]
            {
                "Exit", "Выход", "Beenden", "Quitter", "退出"
            },

            ["SettingsOverlay"] = new[]
            {
                "Settings", "Настройки", "Einstellungen", "Paramètres", "设置"
            },

            ["ParserOverlay"] = new[]
            {
                "Artwork", "Оформление", "Grafik", "Illustration", "美术"
            },

            ["Search"] = new[]
            {
                "Search", "Поиск", "Suche", "Recherche", "搜索"
            },

            // Parser
            ["SearchCovers"] = new[]
            {
                "Searching SteamGridDB", "Поиск в базе SteamGridDB",
                "Suche in der SteamGridDB-Datenbank", "Recherche dans la base SteamGridDB",
                "正在 SteamGridDB 数据库中搜索"
            },

            ["NoGamesFound"] = new[]
            {
                "No matches found",  "Совпадения не найдены",
                "Keine Übereinstimmungen gefunden", "Aucune correspondance trouvée",
                "未找到匹配项"
            },

            ["ManualSearchHint"] = new[]
            {
                "No matches found. Try entering the title manually.",
                "Совпадений не найдено, попробуйте ручной ввод.",
                "Keine Übereinstimmungen gefunden. Versuchen Sie, den Titel manuell einzugeben.",
                "Aucune correspondance trouvée. Essayez de saisir le titre manuellement.",
                "未找到匹配项。请尝试手动输入名称。"
            },

            ["ErrorGridDB"] = new[]
            {
                "Error connecting to SteamGridDB", "Ошибка подключения к SteamGridDB",
                "Fehler bei der Verbindung zu SteamGridDB", "Erreur de connexion à SteamGridDB",
                "连接 SteamGridDB 时出错"
            },

            ["SteamGridDbApiKeyMissing"] = new[]
            {
                "SteamGridDB API key is not configured",
                "API-ключ SteamGridDB не настроен",
                "Der SteamGridDB-API-Schlüssel ist nicht konfiguriert",
                "La clé API SteamGridDB n’est pas configurée",
                "未配置 SteamGridDB API 密钥"
            },

            ["SteamGridDbApiKeyInvalid"] = new[]
            {
                "SteamGridDB API key is invalid or unauthorized",
                "API-ключ SteamGridDB недействителен или не авторизован",
                "Der SteamGridDB-API-Schlüssel ist ungültig oder nicht autorisiert",
                "La clé API SteamGridDB est invalide ou non autorisée",
                "SteamGridDB API 密钥无效或未授权"
            },

            ["LoadingCovers"] = new[]
            {
                "Loading covers", "Загрузка обложек",
                "Cover werden geladen", "Chargement des couvertures",
                "正在加载封面"
            },

            ["NoCoversFound"] = new[]
            {
                "No covers found", "Обложки не найдены",
                "Keine Cover gefunden", "Aucune couverture trouvée",
                "未找到封面"
            },

            ["ParserCoversTabText"] = new[]
            {
                "Covers", "Обложки", "Cover", "Couvertures", "封面"
            },

            ["ParserBackgroundsTabText"] = new[]
            {
                "Backgrounds", "Фоны", "Hintergründe", "Arrière-plans", "背景"
            },

            ["LoadingBackgrounds"] = new[]
            {
                "Loading backgrounds", "Загрузка фонов",
                "Hintergründe werden geladen", "Chargement des arrière-plans",
                "正在加载背景"
            },

            ["NoBackgroundsFound"] = new[]
            {
                "No backgrounds found", "Фоны не найдены",
                "Keine Hintergründe gefunden", "Aucun arrière-plan trouvé",
                "未找到背景"
            },

            ["LoadingCoversError"] = new[]
            {
                "Loading error", "Ошибка загрузки",
                "Fehler beim Laden", "Erreur de chargement",
                "加载出错"
            },

            ["SavingCover"] = new[]
            {
                "Saving cover", "Сохранение обложки",
                "Cover wird gespeichert", "Enregistrement de la couverture",
                "正在保存封面"
            },

            // Settings
            ["SettingsSourceDisplayLabel"] = new[]
            {
                "Switch display", "Переключить дисплей",
                "Anzeige wechseln", "Changer d’affichage",
                "切换显示器"
            },

            ["SettingsSourceDisplayDesc"] = new[]
            {
                "Switch image output to an external monitor or TV.",
                "Переключение изображения между основным и внешним дисплеем.",
                "Bildausgabe zwischen Haupt- und externem Monitor umschalten.",
                "Basculer l’affichage entre l’écran principal et un moniteur externe ou un téléviseur.",
                "将图像输出在主显示器与外部显示器或电视之间切换。"
            },

            ["SettingsAutorunAppLabel"] = new[]
            {
                "Run launcher using a gamepad",
                "Запускать лаунчер при помощи геймпада",
                "Launcher mit einem Gamepad starten",
                "Lancer le lanceur avec une manette",
                "使用手柄启动启动器"
            },

            ["SettingsAutorunAppDesc"] = new[]
            {
                "Hold {ICON} to start the launcher.\nThis option starts a background process and adds it to startup.",
                "Удерживайте {ICON} для запуска лаунчера.\nЭтот параметр активирует фоновый процесс и добавляет его в автозагрузку.",
                "Halten Sie {ICON} gedrückt, um den Launcher zu starten.\nDiese Option startet einen Hintergrundprozess und fügt ihn dem Autostart hinzu.",
                "Maintenez {ICON} enfoncé pour lancer le lanceur.\nCette option démarre un processus en arrière-plan et l'ajoute au démarrage automatique.",
                "按住 {ICON} 启动启动器。\n此选项会启动后台进程并将其添加到开机启动。"
            },

            ["SettingsFpsMonitorLabel"] = new[]
            {
                "Performance monitoring",
                "Мониторинг производительности",
                "Leistungsüberwachung",
                "Surveillance des performances",
                "性能监控"
            },

            ["SettingsFpsMonitorDesc"] = new[]
            {
                "FPS Monitor is used for monitoring.\nIt is recommended to enable launch in minimized state in its settings.",
                "Для мониторинга используется FPS Monitor.\nРекомендуется включить в его настройках запуск в свернутом состоянии.",
                "Für die Überwachung wird FPS Monitor verwendet.\nEs wird empfohlen, in seinen Einstellungen den Start im minimierten Zustand zu aktivieren.",
                "FPS Monitor est utilisé pour la surveillance.\nIl est recommandé d'activer le lancement en état réduit dans ses paramètres.",
                "用于监控的是 FPS Monitor。\n建议在其设置中启用最小化状态启动。"
            },

            ["SettingsExitAppLabel"] = new[]
            {
                "Exit to desktop", "Выход на рабочий стол",
                "Zum Desktop", "Retour au bureau",
                "返回桌面"
            },

            ["SettingsTreeNoSplash"] = new[]
            {
                "Skip splash", "Без заставки",
                "Splash überspringen", "Ignorer l’écran",
                "跳过启动画面"
            },

            ["SettingsTreeNoMusic"] = new[]
            {
                "No music", "Без музыки",
                "Ohne Musik", "Sans musique",
                "无音乐"
            },

            ["SettingsTreeHotSwap"] = new[]
            {
                "External display", "Внешний дисплей",
                "Externer Bildschirm", "Écran externe",
                "外接显示器"
            },

            // Confirmation
            ["ConfirmYes"] = new[]
            {
                "Yes", "Да", "Ja", "Oui", "是"
            },

            ["ConfirmNo"] = new[]
            {
                "No", "Нет", "Nein", "Non", "否"
            },

            ["ExitConfirmText"] = new[]
            {
                "Switch the display to the main one?",
                "Переключить дисплей на основной?",
                "Display auf den Hauptbildschirm umschalten?",
                "Changer l'affichage sur l'écran principal ?",
                "将显示切换到主屏幕？"
            },

            // Empty state
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

            // Messages
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
                "Xbox Game Bar detected.\nGame and display control is handled by the system.",
                "Xbox Game Bar обнаружен.\nУправление играми и дисплеем осуществляется системой.",
                "Xbox Game Bar erkannt.\nDie Steuerung von Spielen und Anzeige erfolgt durch das System.",
                "Xbox Game Bar détecté.\nLe contrôle des jeux et de l'affichage est assuré par le système.",
                "检测到 Xbox Game Bar。\n游戏和显示控制由系统处理。"
            },

            ["MessageGameBarNotDetected"] = new[]
            {
                "Xbox Game Bar not detected.\nGame and display control is handled by the application.",
                "Xbox Game Bar не обнаружен.\nУправление играми и дисплеем осуществляется приложением.",
                "Xbox Game Bar nicht erkannt.\nDie Steuerung von Spielen und Anzeige erfolgt durch die Anwendung.",
                "Xbox Game Bar non détecté.\nLe contrôle des jeux et de l'affichage est assuré par l'application.",
                "未检测到 Xbox Game Bar。\n游戏和显示控制由应用程序处理。"
            },

            ["MessageDebugBuild"] = new[]
            {
                "Debug build is running.\nDiagnostic logging is enabled.",
                "Запущена отладочная сборка.\nДиагностическое логирование включено.",
                "Debug-Build wird ausgeführt.\nDie Diagnoseprotokollierung ist aktiviert.",
                "La version de débogage est en cours d'exécution.\nLa journalisation de diagnostic est activée.",
                "正在运行调试版本。\n诊断日志记录已启用。"
            },

            // Tabs
            ["TabGamelistText"] = new[]
            {
                "Library", "Библиотека", "Bibliothek", "Bibliothèque", "游戏库"
            },

            ["TabLastPlayedText"] = new[]
            {
                "Recent games", "Недавние игры", "Letzte Spiele", "Jeux récents", "最近的游戏"
            },

            // Time
            ["HoursShort"] = new[]
            {
                "h", "ч", "h", "h", "时"
            },

            ["MinutesShort"] = new[]
            {
                "m", "м", "m", "m", "分"
            }
        };

        public static void Init()
        {
            ValidateTranslations();

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
            else if (lang == "zh")
                _currentLang = 4;
            else
                _currentLang = 0;
        }

        [Conditional("DEBUG")]
        private static void ValidateTranslations()
        {
            const int expectedLanguages = 5;

            foreach (var pair in Translations)
            {
                var values = pair.Value;

                if (values == null || values.Length != expectedLanguages)
                {
                    DebugLog.Write("Localization", $"Key '{pair.Key}' has {values?.Length ?? 0} translations; expected {expectedLanguages}.");

                    continue;
                }

                for (int i = 0; i < values.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(values[i]))
                    {
                        DebugLog.Write("Localization", $"Key '{pair.Key}' has an empty translation at index {i}.");
                    }
                }
            }
        }

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

        public static string GetFormattedTime(int value, bool isHours)
        {
            string unit = isHours ? GetString("HoursShort") : GetString("MinutesShort");
            return $"{value}{unit}";
        }

        // Apply translations to matching TextBlock and Run fields.
        public static void ApplyLocalization(FrameworkElement window)
        {
            try
            {
                var fields = window.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

                foreach (var field in fields)
                {
                    var value = field.GetValue(window);

                    if (value == null)
                        continue;

                    string key = field.Name;

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

            catch
            {
            }
        }
    }
}