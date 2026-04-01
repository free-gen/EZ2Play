using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using EZ2Play.App;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Linq;

namespace EZ2Play.Main
{
    public partial class App : Application
    {
        private MainWindow _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            // --------------- ЗАПРЕТ ВТОРОГО ЭКЗЕМПЛЯРА ---------------
            
            string processName = Process.GetCurrentProcess().ProcessName;
            int count = Process.GetProcessesByName(processName).Length;
            
            if (count > 1)
                Environment.Exit(0);
            
            // --------------- ОСНОВНОЙ ЗАПУСК ---------------
            
            try
            {
                Locals.Init();
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);

                bool noSplash = false;
                bool hotSwap = false;
                bool noMusic = false;

                foreach (string arg in e.Args)
                {
                    if (string.Equals(arg, "--nosplash", StringComparison.OrdinalIgnoreCase))
                        noSplash = true;
                    else if (string.Equals(arg, "--hotswap", StringComparison.OrdinalIgnoreCase))
                        hotSwap = true;
                    else if (string.Equals(arg, "--nomusic", StringComparison.OrdinalIgnoreCase))
                        noMusic = true;
                }

                if (noMusic)
                    Sound.DisableMusic = true;

                _mainWindow = new MainWindow(hotSwap);

                EventManager.RegisterClassHandler(typeof(UIElement),
                    UIElement.GotFocusEvent,
                    new RoutedEventHandler(OnAnyElementGotFocus));

                _mainWindow.Visibility = Visibility.Hidden;
                _mainWindow.ShowInTaskbar = false;
                _mainWindow.ShowWithAnimation(noSplash);

                base.OnStartup(e);
            }
            catch
            {
                throw;
            }
        }

        private void OnAnyElementGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.FocusVisualStyle = null;

                if (element is Rectangle rect)
                    rect.Focusable = false;
                else if (element is TextBlock textBlock)
                    textBlock.Focusable = false;
                else if (element is Border border)
                    border.Focusable = false;
            }
        }
    }
}