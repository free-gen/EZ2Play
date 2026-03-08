using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EZParser.Models;
using EZParser.Services;
using Wpf.Ui.Controls;
using Wpf.Ui;
using Microsoft.Win32;
using System.Threading;

namespace EZParser
{
    public partial class MainWindow : FluentWindow
    {
        // private readonly SteamGridDBApi _sgdbApi;
        private SteamGridDBApi _sgdbApi;
        private readonly PlayStationStoreParser _psParser;
        private readonly ImageConverter _converter;
        private readonly HttpClient _httpClient;
        private readonly IContentDialogService _contentDialogService = new ContentDialogService();

        private string _currentSource = "PsStore";
        private int? _currentGameId;
        private string _currentGameName = "";
        private ObservableCollection<GameResult> _psResults;
        private ObservableCollection<GridResult> _gridResults;
        private bool _isSearching = false;
        
        // Timer для автодополнения
        private DispatcherTimer _autocompleteTimer;
        private string _lastSearchQuery = "";

        public MainWindow()
        {
            InitializeComponent();

            // _sgdbApi = new SteamGridDBApi();
            _psParser = new PlayStationStoreParser();
            _converter = new ImageConverter("icons");
            _httpClient = new HttpClient();

            _psResults = new ObservableCollection<GameResult>();
            _gridResults = new ObservableCollection<GridResult>();

            ResultsItemsControl.ItemsSource = _psResults;

            SourceCombo.SelectionChanged += SourceCombo_SelectionChanged;

            // Настройка таймера автодополнения
            _autocompleteTimer = new DispatcherTimer();
            _autocompleteTimer.Interval = TimeSpan.FromMilliseconds(280);
            _autocompleteTimer.Tick += AutocompleteTimer_Tick;
            _autocompleteTimer.IsEnabled = false;

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            FocusManager.SetFocusedElement(this, SearchBox);
            
            // Проверка пути сохранения
            var savePath = _converter.GetSaveDirectory();
            System.Diagnostics.Debug.WriteLine($"Save directory: {savePath}");
            System.Diagnostics.Debug.WriteLine($"Directory exists: {Directory.Exists(savePath)}");

            // _contentDialogService.SetDialogHost(RootContentDialogPresenter);
            _contentDialogService.SetDialogHost(RootContentDialogHost);
        }

        // Работа с реестром
        private async Task<bool> EnsureApiKeyAsync()
        {
            // Если API уже создан, значит ключ есть
            // System.Windows.MessageBox.Show("EnsureApiKeyAsync вызван!");
            if (_sgdbApi != null)
                return true;

            // Проверяем реестр
            var key = GetApiKeyFromRegistry();
            if (!string.IsNullOrEmpty(key))
            {
                _sgdbApi = new SteamGridDBApi(key);
                return true;
            }

            // Ключа нет — показываем диалог
            key = await ShowApiKeyDialogAsync();
            if (!string.IsNullOrEmpty(key))
            {
                SaveApiKeyToRegistry(key);
                _sgdbApi = new SteamGridDBApi(key);
                return true;
            }

            // Пользователь отменил ввод
            StatusLabel.Text = "API key required for GridDB";
            return false;
        }

        private string GetApiKeyFromRegistry()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\EZParser"))
            {
                return key?.GetValue("SteamGridDBApiKey") as string;
            }
        }

        private void SaveApiKeyToRegistry(string apiKey)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\EZParser"))
            {
                key.SetValue("SteamGridDBApiKey", apiKey);
            }
        }

        // Получение или создание экземпляра API с проверкой ключа
        private async Task<SteamGridDBApi> GetOrCreateSgdbApiAsync()
        {
            if (_sgdbApi != null)
                return _sgdbApi;

            // Если мы дошли сюда, значит EnsureApiKeyAsync не сработал (например, при первом поиске без переключения)
            var key = GetApiKeyFromRegistry();
            if (string.IsNullOrEmpty(key))
            {
                key = await ShowApiKeyDialogAsync();
                if (string.IsNullOrEmpty(key))
                    return null;
                SaveApiKeyToRegistry(key);
            }

            _sgdbApi = new SteamGridDBApi(key);
            return _sgdbApi;
        }

        private async Task<string> ShowApiKeyDialogAsync()
        {
            var page = new Views.ApiKeyDialogPage();

            var dialog = new ContentDialog
            {
                Title = "API Key Required",
                Content = page,
                PrimaryButtonText = "Save Key",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                IsPrimaryButtonEnabled = false,
                Padding = new Thickness(20)
            };

            page.SetParentDialog(dialog);

            var result = await _contentDialogService.ShowAsync(dialog, CancellationToken.None);

            return result == ContentDialogResult.Primary 
                ? page.ApiKey?.Trim() 
                : null;
        }

        private async void SourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourceCombo.SelectedItem is ComboBoxItem item)
            {
                _currentSource = item.Content.ToString();
                ClearAll();

                if (_currentSource == "GridDB")
                {
                    // Временно отписываемся от события, чтобы избежать рекурсии при возможном переключении
                    SourceCombo.SelectionChanged -= SourceCombo_SelectionChanged;
                    bool keyOk = await EnsureApiKeyAsync();
                    SourceCombo.SelectionChanged += SourceCombo_SelectionChanged;

                    if (!keyOk)
                    {
                        // Опционально: переключить обратно на PsStore
                        SourceCombo.SelectedIndex = 0; // Индекс PsStore (предполагается, что он первый)
                    }
                }
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (_currentSource != "GridDB" || _isSearching)
                return;

            var text = sender.Text.Trim();
            
            if (text.Length < 2)
            {
                sender.ItemsSource = null;
                return;
            }

            // Перезапуск таймера (debounce)
            _autocompleteTimer.Stop();
            _autocompleteTimer.Start();
            _lastSearchQuery = text;
        }

        private async void AutocompleteTimer_Tick(object sender, EventArgs e)
        {
            _autocompleteTimer.Stop();
            
            var query = _lastSearchQuery.Trim();
            if (query != SearchBox.Text.Trim())
                return;

            await DoAutocompleteAsync(query);
        }

        private async Task DoAutocompleteAsync(string query)
        {
            _isSearching = true;
            StatusLabel.Text = "Searching...";

            try
            {
                var api = await GetOrCreateSgdbApiAsync();
                if (api == null)
                {
                    StatusLabel.Text = "API key required";
                    return;
                }
                var suggestions = await api.SearchGamesAsync(query);
                
                if (suggestions.Count > 0)
                {
                    SearchBox.ItemsSource = suggestions;
                    StatusLabel.Text = $"Found {suggestions.Count}";
                }
                else
                {
                    SearchBox.ItemsSource = null;
                    StatusLabel.Text = "Nothing was found.";
                }
            }
            finally
            {
                _isSearching = false;
            }
        }

        private async void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            var selectedText = args.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedText)) return;

            // Извлекаем ID из строки вида "Game Name (ID: 12345)"
            var idStart = selectedText.LastIndexOf("ID: ") + 4;
            var idEnd = selectedText.LastIndexOf(')');
            if (idStart > 3 && idEnd > idStart)
            {
                var idStr = selectedText.Substring(idStart, idEnd - idStart);
                if (int.TryParse(idStr, out int id))
                {
                    _currentGameId = id;
                    _currentGameName = selectedText.Substring(0, selectedText.LastIndexOf(" (ID:"));
                    await LoadGridsAsync();
                }
            }
        }

        private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var query = args.QueryText.Trim();
            if (string.IsNullOrEmpty(query))
                return;

            ClearAll();
            _currentGameName = query;

            if (_currentSource == "GridDB")
            {
                // Если уже есть результат автодополнения - используем его
                if (_currentGameId.HasValue)
                {
                    await LoadGridsAsync();
                }
                else
                {
                    // Иначе ищем через автодополнение
                    await DoAutocompleteAsync(query);
                }
            }
            else
            {
                await SearchPsAsync(query);
            }
        }

        private async Task SearchPsAsync(string query)
        {
            _isSearching = true;
            StatusLabel.Text = "Searching...";

            try
            {
                var results = await _psParser.SearchAsync(query, maxPages: 2);
                
                _psResults.Clear();
                ResultsItemsControl.ItemsSource = _psResults;
                
                // Сначала добавляем все элементы в коллекцию
                foreach (var result in results)
                {
                    _psResults.Add(result);
                }

                // Затем загружаем изображения параллельно
                var tasks = results.Select(result => LoadImageAsync(result.CoverUrl, result));
                await Task.WhenAll(tasks);

                StatusLabel.Text = $"Found {results.Count}";
            }
            finally
            {
                _isSearching = false;
            }
        }

        private async Task LoadGridsAsync()
        {
            if (!_currentGameId.HasValue)
                return;

            StatusLabel.Text = "Downloading covers...";

            try
            {
                var api = await GetOrCreateSgdbApiAsync();
                if (api == null)
                {
                    StatusLabel.Text = "API key required";
                    return;
                }

                var grids = await api.GetSquareGridsAsync(_currentGameId.Value, _currentGameName, 30);
                
                _gridResults.Clear();
                ResultsItemsControl.ItemsSource = _gridResults;
                
                foreach (var grid in grids)
                {
                    _gridResults.Add(grid);
                }

                // Затем загружаем изображения параллельно
                var tasks = grids.Select(grid => LoadImageAsync(grid.Thumb, grid));
                await Task.WhenAll(tasks);

                StatusLabel.Text = $"Found {grids.Count}";
            }
            catch
            {
                StatusLabel.Text = "Downloading Error";
            }
        }

        private async Task LoadImageAsync(string url, object item)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                using (var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync()))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.DecodePixelWidth = 150;
                    bitmap.DecodePixelHeight = 150;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    // Возвращаемся в UI поток для обновления свойства
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (item is GameResult game)
                            game.ImageSource = bitmap;
                        else if (item is GridResult grid)
                            grid.ImageSource = bitmap;
                    });
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        private async void DownloadSelected_Click(object sender, RoutedEventArgs e)
        {
            var formatItem = FormatCombo.SelectedItem as ComboBoxItem;
            if (formatItem == null) return;
            
            var format = formatItem.Content.ToString().ToLower();
            StatusLabel.Text = "Downloading...";

            if (_currentSource == "GridDB")
            {
                var selected = new List<GridResult>();
                foreach (var grid in _gridResults)
                {
                    if (grid.IsSelected)
                        selected.Add(grid);
                }
                
                if (selected.Count == 0)
                {
                    StatusLabel.Text = "Nothing is selected";
                    return;
                }
                
                await DownloadGridsAsync(selected, format);
            }
            else
            {
                var selected = new List<GameResult>();
                foreach (var game in _psResults)
                {
                    if (game.IsSelected)
                        selected.Add(game);
                }
                
                if (selected.Count == 0)
                {
                    StatusLabel.Text = "Nothing is selected";
                    return;
                }
                
                await DownloadPsAsync(selected, format);
            }
        }

        private async void DownloadAll_Click(object sender, RoutedEventArgs e)
        {
            var formatItem = FormatCombo.SelectedItem as ComboBoxItem;
            if (formatItem == null) return;
            
            var format = formatItem.Content.ToString().ToLower();
            StatusLabel.Text = "Downloading...";

            if (_currentSource == "GridDB")
            {
                if (_gridResults.Count == 0)
                {
                    StatusLabel.Text = "No results";
                    return;
                }
                await DownloadGridsAsync(new List<GridResult>(_gridResults), format);
            }
            else
            {
                if (_psResults.Count == 0)
                {
                    StatusLabel.Text = "No results";
                    return;
                }
                await DownloadPsAsync(new List<GameResult>(_psResults), format);
            }
        }

        private async Task DownloadGridsAsync(List<GridResult> grids, string format)
        {
            using (var session = new HttpClient())
            {
                var safeName = SanitizeFilename(_currentGameName);
                int successCount = 0;
                int failCount = 0;
                string lastError = "";

                foreach (var grid in grids)
                {
                    try
                    {
                        (bool success, string filename, string message) result;
                        
                        if (format == "ico")
                        {
                            result = await _converter.DownloadAndConvertToIcoAsync(session, grid.Url, safeName);
                        }
                        else
                        {
                            result = await _converter.DownloadPngAsync(session, grid.Url, safeName);
                        }

                        if (result.success)
                        {
                            successCount++;
                        }
                        else
                        {
                            failCount++;
                            lastError = result.message;
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        lastError = ex.Message;
                    }
                }

                if (failCount > 0)
                    StatusLabel.Text = $"Downloaded: {successCount}, Errors: {failCount}";
                else
                    StatusLabel.Text = $"Downloaded {successCount} covers";
            }
        }

        private async Task DownloadPsAsync(List<GameResult> items, string format)
        {
            using (var session = new HttpClient())
            {
                int successCount = 0;
                int failCount = 0;
                string lastError = "";

                foreach (var item in items)
                {
                    try
                    {
                        var safeName = SanitizeFilename(item.Name);
                        (bool success, string filename, string message) result;
                        
                        if (format == "ico")
                        {
                            result = await _converter.DownloadAndConvertToIcoAsync(session, item.CoverUrl, safeName);
                        }
                        else
                        {
                            result = await _converter.DownloadPngAsync(session, item.CoverUrl, safeName);
                        }

                        if (result.success)
                        {
                            successCount++;
                        }
                        else
                        {
                            failCount++;
                            lastError = result.message;
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        lastError = ex.Message;
                    }
                }

                if (failCount > 0)
                    StatusLabel.Text = $"Downloaded: {successCount}, Errors: {failCount}";
                else
                    StatusLabel.Text = $"Downloaded {successCount} covers";
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSource == "GridDB")
            {
                foreach (var grid in _gridResults)
                    grid.IsSelected = false;
            }
            else
            {
                foreach (var game in _psResults)
                    game.IsSelected = false;
            }
        }

        private void ClearAll()
        {
            _currentGameId = null;
            _currentGameName = "";
            _psResults.Clear();
            _gridResults.Clear();
            SearchBox.Text = "";
            SearchBox.ItemsSource = null;
            StatusLabel.Text = "Ready";
        }

        private string SanitizeFilename(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Length > 80 ? name.Substring(0, 80) : name;
        }
    }
}