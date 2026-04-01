using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

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
        // 4 = Spanish
        // 5 = Portuguese
        // 6 = Italian
        // 7 = Polish
        // 8 = Chinese (Simplified)
        // 9 = Korean
        // 10 = Japanese
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
                    "Starten", "Lancer", "Iniciar",
                    "Iniciar", "Avvia", "Uruchom",
                    "启动", "시작", "起動"
                },
                ["Exit"] = new[] 
                { 
                    "Exit", "Выход", 
                    "Beenden", "Quitter", "Salir",
                    "Sair", "Esci", "Wyjście",
                    "退出", "종료", "終了"
                },
                ["Sorting"] = new[] 
                { 
                    "Switch tabs", "Смена вкладок", 
                    "Tabs wechseln", "Changer d'onglet", "Cambiar pestañas",
                    "Trocar abas", "Cambia scheda", "Zmień zakładki",
                    "切换标签页", "탭 전환", "タブ切り替え"
                },
                ["ScreenSwap"] = new[] 
                { 
                    "Screen swap", "Свап дисплея", 
                    "Bildschirm tauschen", "Écran swap", "Intercambiar pantalla",
                    "Trocar tela", "Scambia schermo", "Zamiana ekranów",
                    "屏幕交换", "화면 전환", "画面入れ替え"
                },

                ["NoShortcutsMessage1"] = new[]
                {
                    "Place your shortcuts in the shortcuts folder and restart the application.",
                    "Поместите ваши ярлыки в папку shortcuts и запустите приложение заново.",
                    "Platzieren Sie Ihre Verknüpfungen im Ordner shortcuts und starten Sie die Anwendung neu.",
                    "Placez vos raccourcis dans le dossier shortcuts et redémarrez l'application.",
                    "Coloque sus accesos directos en la carpeta shortcuts y reinicie la aplicación.",
                    "Coloque seus atalhos na pasta shortcuts e reinicie o aplicativo.",
                    "Inserisci i tuoi collegamenti nella cartella shortcuts e riavvia l'applicazione.",
                    "Umieść swoje skróty w folderze shortcuts i uruchom ponownie aplikację.",
                    "请将快捷方式放入 shortcuts 文件夹并重新启动应用程序。",
                    "바로가기를 shortcuts 폴더에 넣고 애플리케이션을 다시 시작하세요.",
                    "ショートカットを shortcuts フォルダに置いて、アプリケーションを再起動してください。"
                },

                ["NoShortcutsMessage2"] = new[]
                {
                    "Press Esc to exit",
                    "Для выхода нажмите Esc.",
                    "Drücken Sie Esc zum Beenden",
                    "Appuyez sur Échap pour quitter",
                    "Presione Esc para salir",
                    "Pressione Esc para sair",
                    "Premi Esc per uscire",
                    "Naciśnij Esc, aby wyjść",
                    "按 Esc 退出",
                    "Esc를 눌러 종료하세요",
                    "Escキーを押して終了"
                },

                ["MessageHotSwap"] = new[]
                {
                    "Application launched in HotSwap mode!\nThe display will revert upon exit.",
                    "Приложение запущено в HotSwap режиме!\nПри выходе дисплей вернется к исходному.",
                    "Anwendung im HotSwap-Modus gestartet!\nBeim Beenden wird die ursprüngliche Anzeige wiederhergestellt.",
                    "Application lancée en mode HotSwap!\nL'écran reviendra à son état d'origine à la sortie.",
                    "¡Aplicación iniciada en modo HotSwap!\nLa pantalla volverá a la normalidad al salir.",
                    "Aplicativo iniciado em modo HotSwap!\nA tela será restaurada ao sair.",
                    "Applicazione avviata in modalità HotSwap!\nAll'uscita lo schermo tornerà come prima.",
                    "Aplikacja uruchomiona w trybie HotSwap!\nPo wyjściu ekran powróci do pierwotnego stanu.",
                    "应用程序已在 HotSwap 模式下启动\n退出时将恢复原始显示。",
                    "HotSwap 모드로 애플리케이션이 실행되었습니다!\n종료 시 디스플레이가 원래대로 돌아갑니다.",
                    "HotSwap モードでアプリケーションが起動しました！\n終了時にディスプレイは元に戻ります。"
                },

                ["MessagePlugGamepad"] = new[]
                {
                    "An input device has been detected:",
                    "Обнаружено устройство ввода:",
                    "Ein Eingabegerät wurde erkannt:",
                    "Un périphérique d'entrée a été détecté :",
                    "Se ha detectado un dispositivo de entrada:",
                    "Um dispositivo de entrada foi detectado:",
                    "È stato rilevato un dispositivo di input:",
                    "Wykryto urządzenie wejściowe:",
                    "检测到输入设备：",
                    "입력 장치가 감지되었습니다:",
                    "入力デバイスが検出されました："
                },

                ["MessageTest"] = new[]
                {
                    "Debug notification:\nUsed for configuration and testing. Does not affect anything."
                },

                ["TabDefault"] = new[] 
                { 
                    "Library", "Библиотека", 
                    "Bibliothek", "Bibliothèque", "Biblioteca",
                    "Biblioteca", "Libreria", "Biblioteka",
                    "游戏库", "라이브러리", "ライブラリ"
                },
                ["TabLastPlayed"] = new[] 
                { 
                    "Recent games", "Недавние игры", 
                    "Letzte Spiele", "Jeux récents", "Juegos recientes",
                    "Jogos recentes", "Giochi recenti", "Ostatnie gry",
                    "最近游戏", "최근 게임", "最近のゲーム"
                },
                
                ["HoursShort"] = new[] 
                { 
                    "h", "ч", 
                    "h", "h", "h",
                    "h", "h", "h",
                    "时", "시", "時"
                },
                ["MinutesShort"] = new[] 
                { 
                    "m", "м", 
                    "m", "m", "m",
                    "m", "m", "m",
                    "分", "분", "分"
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
            else if (lang == "es")
                _currentLang = 4;
            else if (lang == "pt" || lang == "pt-BR")
                _currentLang = 5;
            else if (lang == "it")
                _currentLang = 6;
            else if (lang == "pl")
                _currentLang = 7;
            else if (lang == "zh" || lang == "zh-CN" || lang == "zh-Hans")
                _currentLang = 8;
            else if (lang == "ko")
                _currentLang = 9;
            else if (lang == "ja")
                _currentLang = 10;
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

        public static string GetFormattedTime(int value, bool isHours)
        {
            string unit = isHours ? GetString("HoursShort") : GetString("MinutesShort");
            return $"{value}{unit}";
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

                if (window.FindName("SortingText") is System.Windows.Controls.TextBlock sortingText)
                    sortingText.Text = GetString("Sorting");

                if (window.FindName("NoShortcutsMessage1") is System.Windows.Documents.Run msg1)
                    msg1.Text = GetString("NoShortcutsMessage1");

                if (window.FindName("NoShortcutsMessage2") is System.Windows.Documents.Run msg2)
                    msg2.Text = GetString("NoShortcutsMessage2");

                if (window.FindName("MessageHotSwapRun") is System.Windows.Documents.Run hotswapMsg)
                    hotswapMsg.Text = GetString("MessageHotSwap");

                if (window.FindName("MessageTest") is System.Windows.Documents.Run testMsg)
                    testMsg.Text = GetString("MessageTest");

                if (window.FindName("TabGamelistText") is System.Windows.Controls.TextBlock gamelistTab)
                    gamelistTab.Text = GetString("TabDefault");

                if (window.FindName("TabLastPlayedText") is System.Windows.Controls.TextBlock lastPlayedTab)
                    lastPlayedTab.Text = GetString("TabLastPlayed");
            }
            catch { }
        }
    }
}