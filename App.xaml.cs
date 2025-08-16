using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace EZ2Play.Main
{
    public partial class App : Application
    {
        public static LinearGradientBrush GlowBrush { get; set; }
        public static LinearGradientBrush HorizontalGlowBrush { get; set; }
        public static bool EnableLogging { get; private set; } = false;
        public static bool UseCustomBackground { get; private set; } = false;
        public static bool UseCustomLogoImage { get; private set; } = false;
        public static string CustomLogo { get; private set; } = null;
        public static string CustomSlogan { get; private set; } = null;
        public static bool IsHorizontalMode { get; private set; } = false;
        private EZ2Play.App.SplashScreen _splashScreen;
        private MainWindow _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool noSplash = false;
            bool hotSwap = false;
            

            
            // Обработка аргументов с поддержкой кавычек
            for (int i = 0; i < e.Args.Length; i++)
            {
                string arg = e.Args[i];
                
                // Логирование
                if (string.Equals(arg, "--log", StringComparison.OrdinalIgnoreCase))
                {
                    EnableLogging = true;
                }
                // Запуск без заставки
                else if (string.Equals(arg, "--nosplash", StringComparison.OrdinalIgnoreCase))
                {
                    noSplash = true;
                }
                // Запуск с переключением дисплея
                else if (string.Equals(arg, "--swap", StringComparison.OrdinalIgnoreCase))
                {
                    hotSwap = true;
                }
                // Пользовательский фон
                else if (string.Equals(arg, "--bg", StringComparison.OrdinalIgnoreCase))
                {
                    UseCustomBackground = true;
                }
                // Пользовательский логотип
                else if (string.Equals(arg, "--logo", StringComparison.OrdinalIgnoreCase))
                {
                    UseCustomLogoImage = true;
                }
                // Пользовательский текст логотипа
                else if (arg.StartsWith("--logo-", StringComparison.OrdinalIgnoreCase))
                {
                    string logoText = arg.Substring("--logo-".Length);
                    if (logoText.StartsWith("\"") && logoText.EndsWith("\""))
                    {
                        CustomLogo = logoText.Substring(1, logoText.Length - 2);
                    }
                }
                // Пользовательский текст слогана
                else if (arg.StartsWith("--slogan-", StringComparison.OrdinalIgnoreCase))
                {
                    string sloganText = arg.Substring("--slogan-".Length);
                    if (sloganText.StartsWith("\"") && sloganText.EndsWith("\""))
                    {
                        CustomSlogan = sloganText.Substring(1, sloganText.Length - 2);
                    }
                    else
                    {
                        // Если слоган не в кавычках, берем весь текст после --slogan-
                        CustomSlogan = sloganText;
                    }
                }
                // Горизонтальный режим
                else if (string.Equals(arg, "--wide", StringComparison.OrdinalIgnoreCase))
                {
                    IsHorizontalMode = true;
                }
                // Английский язык
                else if (string.Equals(arg, "--eng", StringComparison.OrdinalIgnoreCase))
                {
                    EZ2Play.App.Langs.SetLanguage(true);
                }
            }
            
            _mainWindow = new MainWindow(hotSwap);
            _mainWindow.Visibility = Visibility.Hidden;
            _mainWindow.ShowInTaskbar = false;
            
            if (noSplash)
            {
                _mainWindow.ShowWithAnimation();
            }
            else
            {
                _splashScreen = new EZ2Play.App.SplashScreen();
                _splashScreen.SplashCompleted += OnSplashCompleted;
                
                _splashScreen.PlaySplashAnimation();
            }
            
            base.OnStartup(e);
        }

        private void OnSplashCompleted(object sender, EventArgs e)
        {
            _mainWindow.ShowWithAnimation();
            
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                _splashScreen.CloseSplash();
            };
            timer.Start();
        }
    }
}