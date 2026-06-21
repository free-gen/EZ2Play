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
                ["Select"] = new[] 
                { 
                    "Select", "Выбрать",
                    "Auswählen", "Sélectionner", "Seleccionar",
                    "Selecionar", "Seleziona", "Wybierz",
                    "选择", "선택", "選択"
                },
                ["SwitchTabs"] = new[] 
                { 
                    "Switch tabs", "Переключение вкладок",
                    "Tabs wechseln", "Changer d’onglet", "Cambiar pestañas",
                    "Trocar abas", "Cambia schede", "Przełączanie kart",
                    "切换标签页", "탭 전환", "タブ切り替え"
                },
                ["Back"] = new[] 
                { 
                    "Back", "Назад",
                    "Zurück", "Retour", "Atrás",
                    "Voltar", "Indietro", "Wstecz",
                    "返回", "뒤로", "戻る"
                },
                ["Exit"] = new[] 
                { 
                    "Exit", "Выход", 
                    "Beenden", "Quitter", "Salir",
                    "Sair", "Esci", "Wyjście",
                    "退出", "종료", "終了"
                },
                ["SettingsOverlay"] = new[] 
                { 
                    "Settings", "Настройки", 
                    "Einstellungen", "Paramètres", "Configuración",
                    "Configurações", "Impostazioni", "Ustawienia",
                    "设置", "設定", "設定"
                },
                ["ScreenSwap"] = new[] 
                { 
                    "Screen swap", "Свап дисплея", 
                    "Bildschirm tauschen", "Écran swap", "Intercambiar pantalla",
                    "Trocar tela", "Scambia schermo", "Zamiana ekranów",
                    "屏幕交换", "화면 전환", "画面入れ替え"
                },

                // Settings
                ["SettingsSourceDisplayLabel"] = new[]
                {
                    "Switch Display", "Переключить дисплей",
                    "Anzeige wechseln", "Changer d’affichage", "Cambiar pantalla",
                    "Alternar monitor", "Cambia display", "Przełącz wyświetlacz",
                    "切换显示器", "디스플레이 전환", "ディスプレイを切り替え"
                },

                ["SettingsSourceDisplayDesc"] = new[]
                {
                    "Switch image output to an external monitor or TV.",
                    "Переключить вывод изображения на внешний монитор или телевизор.",
                    "Bildausgabe auf einen externen Monitor oder Fernseher umschalten.",
                    "Basculer l’affichage vers un moniteur externe ou un téléviseur.",
                    "Cambiar la salida de imagen a un monitor externo o televisor.",
                    "Alternar a saída de vídeo para um monitor externo ou TV.",
                    "Passa l’uscita video a un monitor esterno o TV.",
                    "Przełącz wyjście obrazu na monitor zewnętrzny lub telewizor.",
                    "将图像输出切换到外部显示器或电视。",
                    "외부 모니터 또는 TV로 영상 출력을 전환합니다.",
                    "映像出力を外部モニターまたはテレビに切り替えます。"
                },

                ["SettingsAutorunAppLabel"] = new[]
                {
                    "Gamepad Launch", "Запуск с геймпада",
                    "Start per Gamepad", "Lancement par manette", "Inicio con mando",
                    "Iniciar pelo controle", "Avvio da gamepad", "Uruchamianie padem",
                    "手柄启动", "게임패드 실행", "ゲームパッド起動"
                },

                ["SettingsAutorunAppDesc"] = new[]
                {
                    "Hold {ICON} to launch the launcher. Starts a background process and adds it to startup.",
                    "Удерживайте {ICON} для запуска лаунчера. Запускает фоновый процесс и добавляет его в автозагрузку.",
                    "Halten Sie {ICON}, um den Launcher zu starten. Startet einen Hintergrundprozess und fügt ihn dem Autostart hinzu.",
                    "Maintenez {ICON} pour lancer le lanceur. Démarre un processus en arrière-plan et l'ajoute au démarrage.",
                    "Mantenga {ICON} para iniciar el lanzador. Inicia un proceso en segundo plano y lo agrega al inicio automático.",
                    "Mantenha {ICON} para iniciar o lançador. Inicia um processo em segundo plano e o adiciona à inicialização automática.",
                    "Tieni premuto {ICON} per avviare il launcher. Avvia un processo in background e lo aggiunge all'avvio automatico.",
                    "Przytrzymaj {ICON}, aby uruchomić launcher. Uruchamia proces w tle i dodaje go do autostartu.",
                    "按住 {ICON} 启动启动器。启动后台进程并将其添加到开机启动。",
                    "{ICON}을 길게 눌러 런처를 실행합니다. 백그라운드 프로세스를 시작하고 자동 시작에 추가합니다.",
                    "{ICON}を長押ししてランチャーを起動します。バックグラウンドプロセスを開始し、自動起動に追加します。"
                },

                ["SettingsExitAppLabel"] = new[]
                {
                    "Exit to Desktop", "Выход на рабочий стол",
                    "Zum Desktop", "Retour au bureau", "Volver al escritorio",
                    "Voltar para a área de trabalho", "Torna al desktop", "Powrót do pulpitu",
                    "返回桌面", "바탕 화면으로 돌아가기", "デスクトップに戻る"
                },

                ["SettingsTreeNoSplash"] = new[]
                {
                    "Skip Splash Screen", "Пропускать заставку",
                    "Startbildschirm überspringen", "Ignorer l’écran de démarrage", "Omitir pantalla de inicio",
                    "Pular tela de abertura", "Ignora schermata iniziale", "Pomiń ekran startowy",
                    "跳过启动画面", "시작 화면 건너뛰기", "スプラッシュ画面をスキップ"
                },

                ["SettingsTreeNoMusic"] = new[]
                {
                    "Launch Without Music", "Запускать без музыки",
                    "Ohne Musik starten", "Lancer sans musique", "Iniciar sin música",
                    "Iniciar sem música", "Avvia senza musica", "Uruchom bez muzyki",
                    "无音乐启动", "음악 없이 실행", "音楽なしで起動"
                },

                ["SettingsTreeHotSwap"] = new[]
                {
                    "Switch Display", "Переключать дисплей",
                    "Anzeige wechseln", "Changer d’affichage", "Cambiar pantalla",
                    "Alternar monitor", "Cambia display", "Przełącz wyświetlacz",
                    "切换显示器", "디스플레이 전환", "ディスプレイを切り替え"
                },

                ["ConfirmYes"] = new[] 
                { 
                    "Yes", "Да", 
                    "Ja", "Oui", "Sí",
                    "Sim", "Sì", "Tak",
                    "是", "예", "はい"
                },

                ["ConfirmNo"] = new[] 
                { 
                    "No", "Нет", 
                    "Nein", "Non", "No",
                    "Não", "No", "Nie",
                    "否", "아니요", "いいえ"
                },

                ["ExitConfirmText"] = new[] 
                { 
                    "Switch the display to the main one?",
                    "Переключить дисплей на основной?",
                    "Display auf den Hauptbildschirm umschalten?",
                    "Changer l'affichage sur l'écran principal ?",
                    "¿Cambiar la pantalla a la principal?",
                    "Mudar a tela para a principal?",
                    "Passare allo schermo principale?",
                    "Przełączyć ekran na główny?",
                    "将显示切换到主屏幕？",
                    "디스플레이를 메인으로 전환하시겠습니까?",
                    "メインディスプレイに切り替えますか？"
                },

                ["NoShortcutsMessageTop"] = new[]
                {
                    "Place your shortcuts in the shortcuts folder and restart the application.",
                    "Поместите ваши ярлыки в папку shortcuts и запустите приложение заново.",
                    "Legen Sie Ihre Verknüpfungen im Ordner shortcuts ab und starten Sie die Anwendung neu.",
                    "Placez vos raccourcis dans le dossier shortcuts et redémarrez l'application.",
                    "Coloque sus accesos directos en la carpeta shortcuts y reinicie la aplicación.",
                    "Coloque seus atalhos na pasta shortcuts e reinicie o aplicativo.",
                    "Inserisci i collegamenti nella cartella shortcuts e riavvia l'applicazione.",
                    "Umieść skróty w folderze shortcuts i uruchom aplikację ponownie.",
                    "请将快捷方式放入 shortcuts 文件夹并重新启动应用程序。",
                    "바로가기를 shortcuts 폴더에 넣고 애플리케이션을 다시 시작하세요.",
                    "ショートカットを shortcuts フォルダーに配置し、アプリケーションを再起動してください。"
                },

                ["NoShortcutsMessageBottom"] = new[]
                {
                    "Press Esc to exit",
                    "Для выхода нажмите Esc.",
                    "Drücken Sie Esc zum Beenden",
                    "Appuyez sur Échap pour quitter",
                    "Presione Esc para salir",
                    "Pressione Esc para sair",
                    "Premi Esc per uscire",
                    "Naciśnij Esc, aby wyjść",
                    "按 Esc 键退出",
                    "Esc를 눌러 종료하세요",
                    "Escキーで終了"
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
                    "应用程序已在 HotSwap 模式下启动！\n退出后显示将恢复原始状态。",
                    "HotSwap 모드로 애플리케이션이 실행되었습니다!\n종료 시 화면이 원래 상태로 돌아갑니다.",
                    "HotSwapモードでアプリケーションが起動しました！\n終了時に画面は元の状態に戻ります。"
                },

                ["MessagePlugGamepad"] = new[]
                {
                    "Input device detected:",
                    "Обнаружено устройство ввода:",
                    "Ein Eingabegerät wurde erkannt:",
                    "Un périphérique d'entrée a été détecté :",
                    "Se ha detectado un dispositivo de entrada:",
                    "Um dispositivo de entrada foi detectado:",
                    "È stato rilevato un dispositivo di input:",
                    "Wykryto urządzenie wejściowe:",
                    "已检测到输入设备：",
                    "입력 장치가 감지되었습니다:",
                    "入力デバイスが検出されました："
                },

                ["MessageGameBarDetected"] = new[]
                {
                    "XBOX Game Bar detected.\nGame and display control is handled by the system.",
                    "XBOX Game Bar обнаружен.\nУправление играми и дисплеем осуществляется системой.",
                    "XBOX Game Bar erkannt.\nDie Steuerung von Spielen und Anzeige erfolgt durch das System.",
                    "XBOX Game Bar détecté.\nLe contrôle des jeux et de l'affichage est assuré par le système.",
                    "XBOX Game Bar detectado.\nEl control de juegos y pantalla es gestionado por el sistema.",
                    "XBOX Game Bar detectado.\nO controle de jogos e exibição é gerenciado pelo sistema.",
                    "XBOX Game Bar rilevato.\nIl controllo di giochi e display è gestito dal sistema.",
                    "Wykryto XBOX Game Bar.\nSterowanie grami i wyświetlaczem jest obsługiwane przez system.",
                    "检测到 XBOX Game Bar。\n游戏和显示控制由系统处理。",
                    "XBOX Game Bar가 감지되었습니다.\n게임 및 디스플레이 제어는 시스템이 처리합니다.",
                    "XBOX Game Bar が検出されました。\nゲームとディスプレイの制御はシステムが担当します。"
                },

                ["MessageGameBarNotDetected"] = new[]
                {
                    "XBOX Game Bar not detected.\nGame and display control is handled by the application.",
                    "XBOX Game Bar не обнаружен.\nУправление играми и дисплеем осуществляется приложением.",
                    "XBOX Game Bar nicht erkannt.\nDie Steuerung von Spielen und Anzeige erfolgt durch die Anwendung.",
                    "XBOX Game Bar non détecté.\nLe contrôle des jeux et de l'affichage est assuré par l'application.",
                    "XBOX Game Bar no detectado.\nEl control de juegos y pantalla es gestionado por la aplicación.",
                    "XBOX Game Bar não detectado.\nO controle de jogos e exibição é gerenciado pelo aplicativo.",
                    "XBOX Game Bar non rilevato.\nIl controllo di giochi e display è gestito dall'applicazione.",
                    "Nie wykryto XBOX Game Bar.\nSterowanie grami i wyświetlaczem jest obsługiwane przez aplikację.",
                    "未检测到 XBOX Game Bar。\n游戏和显示控制由应用程序处理。",
                    "XBOX Game Bar가 감지되지 않았습니다.\n게임 및 디스플레이 제어는 애플리케이션이 처리합니다.",
                    "XBOX Game Bar が検出されませんでした。\nゲームとディスプレイの制御はアプリケーションが担当します。"
                },

                ["MessageTest"] = new[]
                {
                    "Debug notification:\nUsed for configuration and testing. Does not affect anything.",
                    "Отладочное уведомление:\nИспользуется для настройки и тестирования. Ни на что не влияет.",
                    "Debug-Benachrichtigung:\nWird für Konfiguration und Tests verwendet. Beeinflusst nichts.",
                    "Notification de débogage :\nUtilisé pour la configuration et les tests. N'affecte rien.",
                    "Notificación de depuración:\nUtilizado para configuración y pruebas. No afecta nada.",
                    "Notificação de depuração:\nUsado para configuração e testes. Não afeta nada.",
                    "Notifica di debug:\nUtilizzato per configurazione e test. Non influisce su nulla.",
                    "Powiadomienie debugowania:\nUżywane do konfiguracji i testowania. Nie wpływa na nic.",
                    "调试通知：\n用于配置和测试。不影响任何内容。",
                    "디버그 알림:\n구성 및 테스트에 사용됩니다. 아무 영향도 주지 않습니다.",
                    "デバッグ通知：\n設定とテストに使用されます。何にも影響しません。"
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
                    "Jogos recentes", "Giochi recenti", "Ostatnio grane gry",
                    "最近的游戏", "최근 플레이한 게임", "最近プレイしたゲーム"
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

                if (window.FindName("SelectText") is System.Windows.Controls.TextBlock selectText)
                    selectText.Text = GetString("Select");

                if (window.FindName("SwitchTabsText") is System.Windows.Controls.TextBlock switchTabsText)
                    switchTabsText.Text = GetString("SwitchTabs");

                if (window.FindName("BackText") is System.Windows.Controls.TextBlock backText)
                    backText.Text = GetString("Back");

                if (window.FindName("ExitText") is System.Windows.Controls.TextBlock exitText)
                    exitText.Text = GetString("Exit");

                if (window.FindName("ScreenSwapText") is System.Windows.Controls.TextBlock swapText)
                    swapText.Text = GetString("ScreenSwap");

                if (window.FindName("SettingsOverlayText") is System.Windows.Controls.TextBlock settingsOverlayText)
                    settingsOverlayText.Text = GetString("SettingsOverlay");

                if (window.FindName("NoShortcutsMessageTop") is System.Windows.Documents.Run noFolderMsgTop)
                    noFolderMsgTop.Text = GetString("NoShortcutsMessageTop");

                if (window.FindName("NoShortcutsMessageBottom") is System.Windows.Documents.Run noFolderMsgBottom)
                    noFolderMsgBottom.Text = GetString("NoShortcutsMessageBottom");

                if (window.FindName("MessageHotSwapRun") is System.Windows.Documents.Run hotswapMsg)
                    hotswapMsg.Text = GetString("MessageHotSwap");

                if (window.FindName("MessageTest") is System.Windows.Documents.Run testMsg)
                    testMsg.Text = GetString("MessageTest");

                if (window.FindName("TabGamelistText") is System.Windows.Controls.TextBlock gamelistTab)
                    gamelistTab.Text = GetString("TabDefault");

                if (window.FindName("TabLastPlayedText") is System.Windows.Controls.TextBlock lastPlayedTab)
                    lastPlayedTab.Text = GetString("TabLastPlayed");

                if (window.FindName("SettingsSourceDisplayLabel") is System.Windows.Controls.TextBlock sourceDisplayLabel)
                    sourceDisplayLabel.Text = GetString("SettingsSourceDisplayLabel");

                if (window.FindName("SettingsSourceDisplayDesc") is System.Windows.Controls.TextBlock sourceDisplayDesc)
                    sourceDisplayDesc.Text = GetString("SettingsSourceDisplayDesc");

                if (window.FindName("SettingsForceDisplayLabel") is System.Windows.Controls.TextBlock saveDisplayLabel)
                    saveDisplayLabel.Text = GetString("SettingsForceDisplayLabel");

                if (window.FindName("SettingsForceDisplayDesc") is System.Windows.Controls.TextBlock saveDisplayDesc)
                    saveDisplayDesc.Text = GetString("SettingsForceDisplayDesc");

                if (window.FindName("SettingsAutorunAppLabel") is System.Windows.Controls.TextBlock autorunAppLabel)
                    autorunAppLabel.Text = GetString("SettingsAutorunAppLabel");

                if (window.FindName("SettingsAutorunAppDesc") is System.Windows.Controls.TextBlock autorunAppDesc)
                    autorunAppDesc.Text = GetString("SettingsAutorunAppDesc");

                if (window.FindName("SettingsExitAppLabel") is System.Windows.Controls.TextBlock exitAppLabel)
                    exitAppLabel.Text = GetString("SettingsExitAppLabel");

                if (window.FindName("SettingsTreeNoSplash") is System.Windows.Controls.TextBlock noSplashText)
                    noSplashText.Text = GetString("SettingsTreeNoSplash");

                if (window.FindName("SettingsTreeNoMusic") is System.Windows.Controls.TextBlock noMusicText)
                    noMusicText.Text = GetString("SettingsTreeNoMusic");

                if (window.FindName("SettingsTreeHotSwap") is System.Windows.Controls.TextBlock hotSwapText)
                    hotSwapText.Text = GetString("SettingsTreeHotSwap");

                if (window.FindName("ConfirmYes") is System.Windows.Controls.TextBlock confirmYes)
                    confirmYes.Text = GetString("ConfirmYes");

                if (window.FindName("ConfirmNo") is System.Windows.Controls.TextBlock confirmNo)
                    confirmNo.Text = GetString("ConfirmNo");

                if (window.FindName("ExitConfirmText") is System.Windows.Controls.TextBlock exitConfirmText)
                    exitConfirmText.Text = GetString("ExitConfirmText");
            }
            catch { }
        }
    }
}