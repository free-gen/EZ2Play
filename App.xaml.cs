using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using EZ2Play.App; 
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace EZ2Play.Main
{
    public partial class App : Application
    {
        public static bool UseCustomLogoImage { get; private set; } = false;
        public static string CustomLogo { get; private set; } = null;
        private MainWindow _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                Locals.InitFromSystem();
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);

                bool noSplash = false;
                bool hotSwap = false;
                bool noMusic = false;

                for (int i = 0; i < e.Args.Length; i++)
                {
                    string arg = e.Args[i];
                    if (string.Equals(arg, "--nosplash", StringComparison.OrdinalIgnoreCase))
                        noSplash = true;
                    else if (string.Equals(arg, "--hotswap", StringComparison.OrdinalIgnoreCase))
                        hotSwap = true;
                    else if (string.Equals(arg, "--nomusic", StringComparison.OrdinalIgnoreCase))
                        noMusic = true;
                }

                try
                {
                    if (noMusic)
                        Sound.DisableMusic = true;

                    _mainWindow = new MainWindow(hotSwap);
                }
                catch
                {
                    throw;
                }

                // Глобальный обработчик для отключения фокуса
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
            // Если элемент получил фокус - отключаем его визуальное отображение
            if (sender is FrameworkElement element)
            {
                element.FocusVisualStyle = null;
                
                // Для _cover
                if (element is Rectangle rect)
                {
                    rect.Focusable = false;
                }
                
                // Для TextBlock
                if (element is TextBlock textBlock)
                {
                    textBlock.Focusable = false;
                }
                
                // Для Border
                if (element is Border border)
                {
                    border.Focusable = false;
                }
            }
        }
    }
}