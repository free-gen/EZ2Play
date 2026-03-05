using System;
using System.IO;
using System.Windows;
using Wpf.Ui.Appearance;

namespace EZParser
{
    public partial class App : Application
    {
        private string _logFilePath;
        private bool _hasErrorOccurred = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Создаём путь для лог-файла рядом с exe
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt");

            // Добавляем обработку исключений с записью в файл
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                _hasErrorOccurred = true;
                LogError("Необработанное исключение", ex);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                _hasErrorOccurred = true;
                LogError("Dispatcher исключение", args.Exception);
                args.Handled = true;
            };

            try
            {
                ApplicationThemeManager.ApplySystemTheme(true);
                
                var window = new MainWindow();
                MainWindow = window;
                window.Show();
            }
            catch (Exception ex)
            {
                _hasErrorOccurred = true;
                LogError("Ошибка при запуске", ex);
                throw;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Если ошибок не было, удаляем лог-файл
            if (!_hasErrorOccurred && File.Exists(_logFilePath))
            {
                try
                {
                    File.Delete(_logFilePath);
                }
                catch
                {
                    // Игнорируем ошибки удаления файла
                }
            }
            
            base.OnExit(e);
        }

        private void LogError(string context, Exception ex)
        {
            try
            {
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}\n" +
                               $"Сообщение: {ex?.Message}\n" +
                               $"StackTrace: {ex?.StackTrace}\n" +
                               $"Source: {ex?.Source}\n" +
                               $"TargetSite: {ex?.TargetSite}\n" +
                               new string('-', 80) + "\n";

                File.AppendAllText(_logFilePath, logMessage);
            }
            catch
            {
                // Игнорируем ошибки записи в лог
            }
        }
    }
}