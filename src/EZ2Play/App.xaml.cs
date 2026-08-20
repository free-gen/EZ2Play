using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using EZ2Play.App;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Threading;

namespace EZ2Play.Main
{
    public partial class App : Application
    {
        private MainWindow _mainWindow;

        private Mutex _singleInstanceMutex;
        private bool _ownsSingleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // --------------- ЗАПРЕТ ВТОРОГО ЭКЗЕМПЛЯРА ---------------

            bool createdNew;

            _singleInstanceMutex = new Mutex(
                true,
                @"Local\EZ2Play.SingleInstance",
                out createdNew);

            _ownsSingleInstanceMutex = createdNew;

            if (!createdNew)
            {
                DebugLog.Write("App", "Second instance blocked.");

                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;

                Shutdown(0);
                return;
            }

            DebugLog.Write("App", "Application startup.");
            
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
            catch (Exception ex)
            {
                DebugLog.Error("App", ex, "Application startup failed.");
                throw;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            DebugLog.Write("App", "Application shutdown.");

            if (_ownsSingleInstanceMutex && _singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                }

                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                _ownsSingleInstanceMutex = false;
            }

            base.OnExit(e);
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