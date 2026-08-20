using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using System.Threading;

using System.Windows.Input;
using System.Windows.Threading;

using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using DrawingImaging = System.Drawing.Imaging;

using Windows.UI.ViewManagement.Core;

namespace EZ2Play.App
{
    public partial class ParserOverlay : UserControl
    {
        private const string ApiKey = "";
        private const string BaseUrl = "https://www.steamgriddb.com/api/v2";

        private const int GridColumns = 4;
        private const int MaxGames = 15;
        private const int MaxCovers = 30;
        private const double FadeDuration = 0.1;

        private readonly InputHandler _inputHandler;
        private readonly MainWindow _mainWindow;
        private readonly HttpClient _httpClient;
        private readonly HttpClient _imageHttpClient;

        private readonly ObservableCollection<ParserGameResult> _gameResults = new ObservableCollection<ParserGameResult>();
        private readonly ObservableCollection<ParserGridResult> _gridResults = new ObservableCollection<ParserGridResult>();

        private ShortcutInfo _shortcut;
        private ParserMode _mode = ParserMode.Games;
        private bool _isBusy;

        private CancellationTokenSource _coversLoadCts;
        private CoreInputView _manualSearchInputView;
        private bool _manualSearchFromNoMatches;

        private enum ParserMode
        {
            Games,
            Covers
        }

        public ParserOverlay(InputHandler inputHandler, MainWindow mainWindow)
        {
            InitializeComponent();

            _inputHandler = inputHandler;
            _mainWindow = mainWindow;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("EZ2Play/1.0");

            _imageHttpClient = new HttpClient();
            _imageHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("EZ2Play/1.0");

            GamesListBox.ItemsSource = _gameResults;
            CoversListBox.ItemsSource = _gridResults;

            Opacity = 0;
            Visibility = Visibility.Collapsed;
        }

        private void CancelCoverLoading()
        {
            _coversLoadCts?.Cancel();

            CoversProgressBar.IsIndeterminate = false;
            CoversProgressBar.Visibility = Visibility.Collapsed;
            CoversProgressBar.Value = 0;

            CoversListBox.Opacity = 1.0;
        }

        // ----------------- OPEN / CLOSE -----------------

        public async void Open()
        {
            if (Visibility == Visibility.Visible)
                return;

            var launcher = _mainWindow.GetLauncher();

            if (launcher == null || launcher.SelectedIndex < 0 || launcher.SelectedIndex >= launcher.Shortcuts.Length)
                return;

            _shortcut = launcher.Shortcuts[launcher.SelectedIndex];
            _mode = ParserMode.Games;

            _gameResults.Clear();
            _gridResults.Clear();

            GamesListBox.Visibility = Visibility.Visible;
            CoversListBox.Visibility = Visibility.Collapsed;

            ShowStatus(Locals.GetString("SearchCovers"));

            _inputHandler.SetParserOpen(true);
            _mainWindow.SetHintsMode(HintPanel.HintMode.Settings);

            Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(FadeDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            BeginAnimation(OpacityProperty, fadeIn);

            await SearchCurrentGameAsync();
        }

        public void Close()
        {
            if (Visibility != Visibility.Visible)
                return;

            CancelCoverLoading();

            if (_manualSearchInputView != null)
            {
                _manualSearchInputView.PrimaryViewHiding -= ManualSearchInputView_Hiding;
                _manualSearchInputView = null;
            }

            SystemProvider.HideSystemKeyboard();
            ManualSearchPanel.Visibility = Visibility.Collapsed;

            var fadeOut = new DoubleAnimation
            {
                From = Opacity,
                To = 0,
                Duration = TimeSpan.FromSeconds(FadeDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                Visibility = Visibility.Collapsed;
                _inputHandler.SetParserOpen(false);
                _mainWindow.SetHintsMode(HintPanel.HintMode.Main);
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }

        public void Back()
        {
            if (_mode == ParserMode.Covers)
            {
                CancelCoverLoading();

                _mode = ParserMode.Games;

                CoversListBox.Visibility = Visibility.Collapsed;
                GamesListBox.Visibility = Visibility.Visible;

                _mainWindow.SetHintsMode(HintPanel.HintMode.ParserGames);

                HideStatus();

                if (_gameResults.Count > 0 && GamesListBox.SelectedIndex < 0)
                    GamesListBox.SelectedIndex = 0;

                GamesListBox.Focus();
                return;
            }

            Close();
        }

        // ----------------- NAVIGATION -----------------

        public void NavigateHorizontal(int direction)
        {
            if (_isBusy || _mode != ParserMode.Covers || CoversListBox.Items.Count == 0)
                return;

            int index = Math.Max(0, CoversListBox.SelectedIndex);
            int column = index % GridColumns;

            if (direction < 0 && column == 0)
                return;

            if (direction > 0 && column == GridColumns - 1)
                return;

            MoveSelection(CoversListBox, index + Math.Sign(direction));
        }

        public void NavigateVertical(int direction)
        {
            if (_isBusy)
                return;

            if (_mode == ParserMode.Games)
            {
                if (GamesListBox.Items.Count == 0)
                    return;

                int index = Math.Max(0, GamesListBox.SelectedIndex);
                MoveSelection(GamesListBox, index + Math.Sign(direction));
                return;
            }

            if (CoversListBox.Items.Count == 0)
                return;

            int indexCover = Math.Max(0, CoversListBox.SelectedIndex);
            MoveSelection(CoversListBox, indexCover + Math.Sign(direction) * GridColumns);
        }

        public void Search()
        {
            if (_isBusy)
                return;

            if (_mode != ParserMode.Games)
                return;

            if (GamesListBox.Visibility != Visibility.Visible)
                return;

            ShowManualSearch(false);
        }

        public async void Confirm()
        {
            if (_isBusy)
                return;

            if (_mode == ParserMode.Games)
            {
                var game = GamesListBox.SelectedItem as ParserGameResult;

                if (game != null)
                    await LoadCoversAsync(game);

                return;
            }

            var cover = CoversListBox.SelectedItem as ParserGridResult;

            if (cover != null)
                await DownloadCoverAsync(cover);
        }

        private void MoveSelection(ListBox listBox, int targetIndex)
        {
            if (listBox.Items.Count == 0)
                return;

            targetIndex = Math.Max(0, Math.Min(targetIndex, listBox.Items.Count - 1));

            if (targetIndex == listBox.SelectedIndex)
                return;

            listBox.SelectedIndex = targetIndex;
            listBox.ScrollIntoView(listBox.SelectedItem);
        }

        private async void ShowManualSearch(bool showHint)
        {
            _manualSearchFromNoMatches = showHint;

            HideStatus();

            GamesListBox.Visibility = Visibility.Collapsed;
            CoversListBox.Visibility = Visibility.Collapsed;

            SearchInputBox.Text = string.Empty;

            if (showHint)
            {
                ManualSearchHintText.Text = Locals.GetString("ManualSearchHint");
                ManualSearchHintText.Visibility = Visibility.Visible;
            }
            else
            {
                ManualSearchHintText.Text = string.Empty;
                ManualSearchHintText.Visibility = Visibility.Collapsed;
            }

            ManualSearchPanel.Visibility = Visibility.Visible;

            // Поле ввода уже не является списком игр.
            _mainWindow.SetHintsMode(HintPanel.HintMode.Settings);

            // Перед фокусом переключаем язык ввода на English.
            SystemProvider.ForceEnglishInputLanguage();

            SearchInputBox.UpdateLayout();

            await Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.ApplicationIdle);

            SearchInputBox.Focus();
            Keyboard.Focus(SearchInputBox);
            SearchInputBox.SelectAll();

            await Task.Delay(100);

            if (_mainWindow.IsGamepadConnected)
            {
                try
                {
                    _manualSearchInputView = CoreInputView.GetForCurrentView();

                    if (_manualSearchInputView != null)
                    {
                        _manualSearchInputView.PrimaryViewHiding -= ManualSearchInputView_Hiding;
                        _manualSearchInputView.PrimaryViewHiding += ManualSearchInputView_Hiding;
                    }
                }
                catch
                {
                }

                SystemProvider.ShowGamepadKeyboard();
            }
        }

        private void ManualSearchInputView_Hiding(CoreInputView sender, CoreInputViewHidingEventArgs args)
        {
            if (ManualSearchPanel.Visibility != Visibility.Visible)
                return;

            if (_manualSearchInputView != null)
            {
                _manualSearchInputView.PrimaryViewHiding -= ManualSearchInputView_Hiding;
                _manualSearchInputView = null;
            }

            ManualSearchPanel.Visibility = Visibility.Collapsed;

            if (_manualSearchFromNoMatches)
            {
                _manualSearchFromNoMatches = false;
                Close();
                return;
            }

            GamesListBox.Visibility = Visibility.Visible;
            _mainWindow.SetHintsMode(HintPanel.HintMode.ParserGames);

            if (_gameResults.Count > 0 && GamesListBox.SelectedIndex < 0)
                GamesListBox.SelectedIndex = 0;

            GamesListBox.Focus();
        }

        // ----------------- GAME SEARCH -----------------

        private async Task SearchCurrentGameAsync(string customQuery = null)
        {
            _isBusy = true;

            try
            {
                string query = string.IsNullOrWhiteSpace(customQuery)
                    ? (_shortcut.DisplayName ?? _shortcut.Name)
                    : customQuery.Trim();
                var results = await SearchGamesAsync(query);

                _gameResults.Clear();

                foreach (var result in results)
                    _gameResults.Add(result);

                HideStatus();

                if (_gameResults.Count == 0)
                {
                    ShowManualSearch(true);
                    return;
                }

                ManualSearchPanel.Visibility = Visibility.Collapsed;
                GamesListBox.Visibility = Visibility.Visible;

                _mainWindow.SetHintsMode(HintPanel.HintMode.ParserGames);

                GamesListBox.SelectedIndex = 0;
                GamesListBox.ScrollIntoView(GamesListBox.SelectedItem);
                GamesListBox.Focus();
            }
            catch
            {
                ShowStatus(Locals.GetString("ErrorGridDB"));
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async void SearchInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;

            string query = SearchInputBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(query))
                return;

            SystemProvider.HideSystemKeyboard();

            ManualSearchPanel.Visibility = Visibility.Collapsed;

            ShowStatus(Locals.GetString("SearchCovers"));

            await SearchCurrentGameAsync(query);
        }

        private async Task<List<ParserGameResult>> SearchGamesAsync(string query)
        {
            string encoded = Uri.EscapeDataString(query.Trim());
            string url = $"{BaseUrl}/search/autocomplete/{encoded}";

            using (var response = await _httpClient.GetAsync(url))
            {
                if (!response.IsSuccessStatusCode)
                    return new List<ParserGameResult>();

                string content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var data = json["data"] as JArray;

                if (json["success"]?.ToObject<bool>() != true || data == null)
                    return new List<ParserGameResult>();

                return data
                    .Where(item => item["id"] != null && item["name"] != null)
                    .Take(MaxGames)
                    .Select(item => new ParserGameResult
                    {
                        Id = item["id"].ToObject<int>(),
                        Name = item["name"].ToString()
                    })
                    .ToList();
            }
        }

        // ----------------- COVER SEARCH -----------------

        private async Task LoadCoversAsync(ParserGameResult game)
        {
            CancelCoverLoading();

            var cts = new CancellationTokenSource();
            _coversLoadCts = cts;

            var cancellationToken = cts.Token;

            _isBusy = true;
            _mode = ParserMode.Covers;

            _mainWindow.SetHintsMode(HintPanel.HintMode.Settings);

            GamesListBox.Visibility = Visibility.Collapsed;
            CoversListBox.Visibility = Visibility.Collapsed;
            CoversListBox.Opacity = 0.45;

            CoversProgressBar.Value = 0;
            CoversProgressBar.IsIndeterminate = true;
            CoversProgressBar.Visibility = Visibility.Visible;

            ShowStatus(Locals.GetString("LoadingCovers"));

            try
            {
                var results = await GetSquareGridsAsync(
                    game.Id,
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                _gridResults.Clear();

                foreach (var result in results)
                    _gridResults.Add(result);

                if (_gridResults.Count == 0)
                {
                    CoversProgressBar.IsIndeterminate = false;
                    CoversProgressBar.Visibility = Visibility.Collapsed;
                    CoversListBox.Opacity = 1.0;

                    ShowStatus(Locals.GetString("NoCoversFound"));
                    return;
                }

                HideStatus();

                CoversListBox.Visibility = Visibility.Visible;
                CoversListBox.SelectedIndex = 0;
                CoversListBox.ScrollIntoView(CoversListBox.SelectedItem);
                CoversListBox.Focus();

                CoversProgressBar.IsIndeterminate = false;
                CoversProgressBar.Minimum = 0;
                CoversProgressBar.Maximum = _gridResults.Count;
                CoversProgressBar.Value = 0;

                int loadedCount = 0;

                var tasks = _gridResults.Select(async grid =>
                {
                    await LoadThumbnailAsync(
                        grid,
                        cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    int completed = Interlocked.Increment(ref loadedCount);

                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                            CoversProgressBar.Value = completed;
                    });
                });

                await Task.WhenAll(tasks);

                cancellationToken.ThrowIfCancellationRequested();

                CoversListBox.Opacity = 1.0;
                CoversProgressBar.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException)
            {
                // Пользователь нажал Back или закрыл ParserOverlay.
            }
            catch
            {
                CoversListBox.Opacity = 1.0;

                CoversProgressBar.IsIndeterminate = false;
                CoversProgressBar.Visibility = Visibility.Collapsed;

                ShowStatus(Locals.GetString("LoadingCoversError"));
            }
            finally
            {
                if (_coversLoadCts == cts)
                {
                    _coversLoadCts = null;
                    cts.Dispose();
                    _isBusy = false;
                }
            }
        }

        private async Task<List<ParserGridResult>> GetSquareGridsAsync(int gameId, CancellationToken cancellationToken)
        {
            string url = $"{BaseUrl}/grids/game/{gameId}?dimensions=512x512,1024x1024&mimes=image/png,image/jpeg&nsfw=false&types=static";

            using (var response = await _httpClient.GetAsync(url, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    return new List<ParserGridResult>();

                string content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var data = json["data"] as JArray;

                if (json["success"]?.ToObject<bool>() != true || data == null)
                    return new List<ParserGridResult>();

                return data
                    .Where(item => item["url"] != null)
                    .Take(MaxCovers)
                    .Select(item => new ParserGridResult
                    {
                        Id = item["id"]?.ToObject<int>() ?? 0,
                        Url = item["url"].ToString(),
                        Thumb = item["thumb"]?.ToString() ?? item["url"].ToString()
                    })
                    .ToList();
            }
        }

        // ----------------- THUMBNAILS -----------------

        private async Task LoadThumbnailAsync(ParserGridResult result, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var response = await _imageHttpClient.GetAsync(result.Url, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();

                    byte[] bytes = await response.Content.ReadAsByteArrayAsync();

                    cancellationToken.ThrowIfCancellationRequested();

                    using (var stream = new MemoryStream(bytes))
                    {
                        var bitmap = new BitmapImage();

                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 512;
                        bitmap.DecodePixelHeight = 512;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        cancellationToken.ThrowIfCancellationRequested();

                        result.ImageSource = bitmap;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Одна битая картинка не должна отменять загрузку остальных.
            }
        }

        // ----------------- DOWNLOAD -----------------

        private async Task DownloadCoverAsync(ParserGridResult cover)
        {
            _isBusy = true;

            CoversListBox.Visibility = Visibility.Collapsed;
            ShowStatus(Locals.GetString("SavingCover"));

            try
            {
                byte[] bytes = await _imageHttpClient.GetByteArrayAsync(cover.Url);

                string coversDirectory = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "shortcuts",
                    "covers");

                Directory.CreateDirectory(coversDirectory);

                // Используем настоящее имя ярлыка, а не DisplayName.
                string coverPath = Path.Combine(coversDirectory, _shortcut.Name + ".png");

                using (var input = new MemoryStream(bytes))
                using (var sourceImage = Drawing.Image.FromStream(input, true, true))
                using (var resizedImage = new Drawing.Bitmap(
                    512,
                    512,
                    DrawingImaging.PixelFormat.Format32bppArgb))
                {
                    using (var graphics = Drawing.Graphics.FromImage(resizedImage))
                    {
                        graphics.Clear(Drawing.Color.Transparent);
                        graphics.CompositingQuality = Drawing2D.CompositingQuality.HighQuality;
                        graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = Drawing2D.SmoothingMode.HighQuality;
                        graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality;

                        float sourceSize = Math.Min(sourceImage.Width, sourceImage.Height);
                        float sourceX = (sourceImage.Width - sourceSize) / 2f;
                        float sourceY = (sourceImage.Height - sourceSize) / 2f;

                        var destRect = new Drawing.RectangleF(0, 0, 512, 512);
                        var sourceRect = new Drawing.RectangleF(sourceX, sourceY, sourceSize, sourceSize);

                        graphics.DrawImage(
                            sourceImage,
                            destRect,
                            sourceRect,
                            Drawing.GraphicsUnit.Pixel);
                    }

                    using (var output = new FileStream(
                        coverPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None))
                    {
                        resizedImage.Save(output, DrawingImaging.ImageFormat.Png);
                    }
                }

                _mainWindow.GetLauncher()?.RefreshSelectedCover();

                Close();
            }
            catch (Exception ex)
            {
                CoversListBox.Visibility = Visibility.Visible;
                ShowStatus(Locals.GetString("LoadingCoversError") + ": " + ex.Message);
            }
            finally
            {
                _isBusy = false;
            }
        }

        // ----------------- STATUS -----------------

        private void ShowStatus(string text)
        {
            StatusText.Text = text;
            StatusText.Visibility = Visibility.Visible;
        }

        private void HideStatus()
        {
            StatusText.Visibility = Visibility.Collapsed;
        }
    }

    // ----------------- MODELS -----------------

    public class ParserGameResult
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public string DisplayText => $"{Name} (ID: {Id})";
    }

    public class ParserGridResult : INotifyPropertyChanged
    {
        private ImageSource _imageSource;

        public int Id { get; set; }
        public string Url { get; set; }
        public string Thumb { get; set; }

        public ImageSource ImageSource
        {
            get => _imageSource;
            set
            {
                _imageSource = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageSource)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}