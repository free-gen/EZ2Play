using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace EZ2Play
{
    public partial class App : Application
    {
        public static LinearGradientBrush GlowBrush { get; set; }
        public static bool EnableLogging { get; private set; } = false;
        private SplashScreen _splashScreen;
        private MainWindow _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool noSplash = false;
            bool hotSwap = false;
            foreach (var arg in e.Args)
            {
                if (string.Equals(arg, "--log", StringComparison.OrdinalIgnoreCase))
                {
                    EnableLogging = true;
                }
                else if (string.Equals(arg, "--nosplash", StringComparison.OrdinalIgnoreCase))
                {
                    noSplash = true;
                }
                else if (string.Equals(arg, "--hotswap", StringComparison.OrdinalIgnoreCase))
                {
                    hotSwap = true;
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
                _splashScreen = new SplashScreen();
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