using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using EZ2Play.App;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace EZ2Play.Main
{
    // --------------- Главное приложение (Entry Point) ---------------

    public partial class App : Application
    {
        // --------------- Публичные свойства ---------------

        // Использование кастомного логотипа
        // public static bool UseCustomLogoImage { get; private set; } = false;

        // Путь к кастомному логотипу
        // public static string CustomLogo { get; private set; } = null;

        // --------------- Поля класса ---------------

        private MainWindow _mainWindow;

        // --------------- Запуск приложения ---------------

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Инициализация локализации
                Locals.InitFromSystem();

                // Применение тёмной темы
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);

                // Парсинг аргументов командной строки
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

                // Отключение музыки если указано
                if (noMusic)
                    Sound.DisableMusic = true;

                // Запуск с hotswap
                _mainWindow = new MainWindow(hotSwap);

                // Глобальный обработчик для отключения фокуса
                // EventManager.RegisterClassHandler(typeof(UIElement),
                //     UIElement.GotFocusEvent,
                //     new RoutedEventHandler(OnAnyElementGotFocus));

                // Настройка видимости и запуск с анимацией
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

        // --------------- Обработчики событий ---------------

        // Глобальная обработка получения фокуса элементами
        // private void OnAnyElementGotFocus(object sender, RoutedEventArgs e)
        // {
        //     if (sender is FrameworkElement element)
        //     {
        //         // Отключаем визуальное отображение фокуса
        //         element.FocusVisualStyle = null;

        //         // Для Rectangle (_cover)
        //         if (element is Rectangle rect)
        //         {
        //             rect.Focusable = false;
        //         }

        //         // Для TextBlock
        //         if (element is TextBlock textBlock)
        //         {
        //             textBlock.Focusable = false;
        //         }

        //         // Для Border
        //         if (element is Border border)
        //         {
        //             border.Focusable = false;
        //         }
        //     }
        // }
    }
}