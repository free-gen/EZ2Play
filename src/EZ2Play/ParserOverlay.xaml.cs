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
using System.Globalization;
using System.Net;
using System.Windows.Input;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using DrawingImaging = System.Drawing.Imaging;

using Windows.UI.ViewManagement.Core;

namespace EZ2Play.App
{
    public partial class ParserOverlay : UserControl, IDisposable
    {
        private const string ApiKey = "1a3c71cbf451d97cd5659e036d88b431";
        private const string BaseUrl = "https://www.steamgriddb.com/api/v2";

        private const int GridColumns = 4;
        private const int MaxGames = 15;
        private const int MaxCovers = 30;
        private const int MaxBackgrounds = 30;
        private const double FadeDuration = 0.1;
        private const double ManualSearchKeyboardGap = 32;

        private readonly InputHandler _inputHandler;
        private readonly MainWindow _mainWindow;
        private readonly AppConfig _config;

        private readonly HttpClient _httpClient;
        private readonly HttpClient _imageHttpClient;

        private CancellationTokenSource _sessionCts;
        private bool _disposed;

        private CultureInfo _inputLanguageBeforeManualSearch;
        private bool _inputLanguageCaptured;

        private readonly ObservableCollection<ParserGameResult> _gameResults = new ObservableCollection<ParserGameResult>();
        private readonly ObservableCollection<ParserGridResult> _gridResults = new ObservableCollection<ParserGridResult>();
        private readonly ObservableCollection<ParserGridResult> _heroResults = new ObservableCollection<ParserGridResult>();

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
            _config = _mainWindow.GetConfig();

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("EZ2Play/1.0");

            _imageHttpClient = new HttpClient();
            _imageHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("EZ2Play/1.0");

            GamesListBox.ItemsSource = _gameResults;
            CoversListBox.ItemsSource = _gridResults;

            Opacity = 0;
            Visibility = Visibility.Collapsed;
        }

        private string ResolveApiKey()
        {
            if (!string.IsNullOrWhiteSpace(ApiKey))
                return ApiKey.Trim();

            if (!string.IsNullOrWhiteSpace(_config?.SteamGridDbApiKey))
                return _config.SteamGridDbApiKey.Trim();

            return string.Empty;
        }

        private bool ConfigureApiAuthorization()
        {
            string apiKey = ResolveApiKey();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
                return false;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            return true;
        }

        private bool IsCurrentSession(CancellationToken cancellationToken)
        {
            return !_disposed && _sessionCts != null && cancellationToken == _sessionCts.Token;
        }

        private bool IsSessionActive(CancellationToken cancellationToken)
        {
            return IsCurrentSession(cancellationToken) &&
                   !cancellationToken.IsCancellationRequested &&
                   Visibility == Visibility.Visible;
        }

        private void CancelSession()
        {
            try
            {
                _sessionCts?.Cancel();
            }

            catch
            {
            }
        }

        private void CaptureInputLanguage()
        {
            if (_inputLanguageCaptured) return;

            _inputLanguageBeforeManualSearch = SystemProvider.ForceEnglishInputLanguage();
            _inputLanguageCaptured = true;
        }

        private void RestoreInputLanguage()
        {
            if (!_inputLanguageCaptured) return;

            SystemProvider.RestoreInputLanguage(_inputLanguageBeforeManualSearch);

            _inputLanguageBeforeManualSearch = null;
            _inputLanguageCaptured = false;
        }

        private void CancelCoverLoading()
        {
            _coversLoadCts?.Cancel();

            CoversProgressBar.IsIndeterminate = false;
            CoversProgressBar.Visibility = Visibility.Collapsed;
            CoversProgressBar.Value = 0;

            CoversListBox.Opacity = 1.0;
        }

        public async void Open()
        {
            if (_disposed || Visibility == Visibility.Visible) return;

            _mainWindow.GetSound()?.PlayLaunchSound();

            var launcher = _mainWindow.GetLauncher();

            if (launcher == null || launcher.SelectedIndex < 0 || launcher.SelectedIndex >= launcher.Shortcuts.Length)
                return;

            _sessionCts?.Dispose();
            _sessionCts = new CancellationTokenSource();

            CancellationToken cancellationToken = _sessionCts.Token;

            _shortcut = launcher.Shortcuts[launcher.SelectedIndex];
            _mode = ParserMode.Games;
            _isBusy = false;
            _manualSearchFromNoMatches = false;

            _gameResults.Clear();
            _gridResults.Clear();

            GamesListBox.Visibility = Visibility.Visible;
            CoversListBox.Visibility = Visibility.Collapsed;
            ManualSearchPanel.Visibility = Visibility.Collapsed;

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

            if (!ConfigureApiAuthorization())
            {
                DebugLog.Write("Parser", "SteamGridDB API key is not configured.");
                ShowStatus(Locals.GetString("SteamGridDbApiKeyMissing"));
                return;
            }

            await SearchCurrentGameAsync(null, cancellationToken);
        }

        public void Close()
        {
            if (Visibility != Visibility.Visible) return;

            _mainWindow.GetSound()?.PlayBackSound();
            CancelSession();
            CancelCoverLoading();

            if (_manualSearchInputView != null)
            {
                _manualSearchInputView.PrimaryViewHiding -= ManualSearchInputView_Hiding;
                _manualSearchInputView.OcclusionsChanged -= ManualSearchInputView_OcclusionsChanged;
                _manualSearchInputView = null;
            }

            ParserSurface.RenderTransform = null;

            SystemProvider.HideSystemKeyboard();
            RestoreInputLanguage();

            ManualSearchPanel.Visibility = Visibility.Collapsed;
            _manualSearchFromNoMatches = false;

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
                _mainWindow.GetSound()?.PlayBackSound();
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

        public void NavigateHorizontal(int direction)
        {
            if (_isBusy || _mode != ParserMode.Covers || CoversListBox.Items.Count == 0) return;

            int index = Math.Max(0, CoversListBox.SelectedIndex);
            int column = index % GridColumns;

            if (direction < 0 && column == 0) return;
            if (direction > 0 && column == GridColumns - 1) return;

            MoveSelection(CoversListBox, index + Math.Sign(direction));
        }

        public void NavigateVertical(int direction)
        {
            if (_isBusy) return;

            if (_mode == ParserMode.Games)
            {
                if (GamesListBox.Items.Count == 0) return;

                int index = Math.Max(0, GamesListBox.SelectedIndex);
                MoveSelection(GamesListBox, index + Math.Sign(direction));
                return;
            }

            if (CoversListBox.Items.Count == 0) return;

            int indexCover = Math.Max(0, CoversListBox.SelectedIndex);
            MoveSelection(CoversListBox, indexCover + Math.Sign(direction) * GridColumns);
        }

        public void Search()
        {
            if (_isBusy) return;
            if (_mode != ParserMode.Games) return;
            if (GamesListBox.Visibility != Visibility.Visible) return;
            if (_sessionCts == null) return;

            if (!ConfigureApiAuthorization())
            {
                ShowStatus(Locals.GetString("SteamGridDbApiKeyMissing"));
                return;
            }

            ShowManualSearch(false, _sessionCts.Token);
        }

        public async void Confirm()
        {
            if (_isBusy || _sessionCts == null) return;

            if (_mode == ParserMode.Games)
            {
                var game = GamesListBox.SelectedItem as ParserGameResult;

                if (game != null)
                {
                    _mainWindow.GetSound()?.PlayLaunchSound();
                    await LoadCoversAsync(game, _sessionCts.Token);
                }

                return;
            }

            var cover = CoversListBox.SelectedItem as ParserGridResult;

            if (cover != null)
            {
                _mainWindow.GetSound()?.PlayLaunchSound();
                await DownloadCoverAsync(cover, _sessionCts.Token);
            }
        }

        private void MoveSelection(ListBox listBox, int targetIndex)
        {
            if (listBox.Items.Count == 0) return;

            targetIndex = Math.Max(0, Math.Min(targetIndex, listBox.Items.Count - 1));

            if (targetIndex == listBox.SelectedIndex) return;

            listBox.SelectedIndex = targetIndex;
            listBox.ScrollIntoView(listBox.SelectedItem);
            _mainWindow.GetSound()?.PlayMoveSound();
        }

        private async void ShowManualSearch(bool showHint, CancellationToken cancellationToken)
        {
            if (!IsSessionActive(cancellationToken)) return;

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
            ParserSurface.RenderTransform = null;

            // Manual search uses the settings-style hint layout.
            _mainWindow.SetHintsMode(HintPanel.HintMode.Settings);

            // Switch input language to English before focusing the search field.
            CaptureInputLanguage();

            SearchInputBox.UpdateLayout();

            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

            if (!IsSessionActive(cancellationToken))
            {
                RestoreInputLanguage();
                return;
            }

            SearchInputBox.Focus();
            Keyboard.Focus(SearchInputBox);
            SearchInputBox.SelectAll();

            try
            {
                await Task.Delay(100, cancellationToken);
            }

            catch (OperationCanceledException)
            {
                RestoreInputLanguage();
                return;
            }

            if (!IsSessionActive(cancellationToken))
            {
                RestoreInputLanguage();
                return;
            }

            if (_mainWindow.IsGamepadConnected)
            {
                try
                {
                    _manualSearchInputView = CoreInputView.GetForCurrentView();

                    if (_manualSearchInputView != null)
                    {
                        _manualSearchInputView.PrimaryViewHiding -= ManualSearchInputView_Hiding;
                        _manualSearchInputView.PrimaryViewHiding += ManualSearchInputView_Hiding;
                        _manualSearchInputView.OcclusionsChanged -= ManualSearchInputView_OcclusionsChanged;
                        _manualSearchInputView.OcclusionsChanged += ManualSearchInputView_OcclusionsChanged;
                    }
                }

                catch
                {
                }

                SystemProvider.ShowGamepadKeyboard();
            }
        }

        private void ManualSearchInputView_OcclusionsChanged(CoreInputView sender, CoreInputViewOcclusionsChangedEventArgs args)
        {
            if (ManualSearchPanel.Visibility != Visibility.Visible) return;

            double currentOffset = (ParserSurface.RenderTransform as TranslateTransform)?.Y ?? 0;
            Point inputPosition = SearchInputBox.TranslatePoint(new Point(0, 0), _mainWindow);

            double inputLeft = inputPosition.X;
            double inputRight = inputPosition.X + SearchInputBox.ActualWidth;
            double inputBottom = inputPosition.Y + SearchInputBox.ActualHeight - currentOffset;
            double requiredOffset = 0;

            foreach (var occlusion in args.Occlusions)
            {
                var rect = occlusion.OccludingRect;

                if (rect.Y <= 0) continue;
                if (rect.X >= inputRight || rect.X + rect.Width <= inputLeft) continue;

                requiredOffset = Math.Max(requiredOffset, inputBottom + ManualSearchKeyboardGap - rect.Y);
            }

            ParserSurface.RenderTransform = requiredOffset > 0 ? new TranslateTransform(0, -requiredOffset) : null;
        }

        private void ManualSearchInputView_Hiding(CoreInputView sender, CoreInputViewHidingEventArgs args)
        {
            if (ManualSearchPanel.Visibility != Visibility.Visible) return;

            RestoreInputLanguage();

            if (_manualSearchInputView != null)
            {
                _manualSearchInputView.PrimaryViewHiding -= ManualSearchInputView_Hiding;
                _manualSearchInputView.OcclusionsChanged -= ManualSearchInputView_OcclusionsChanged;
                _manualSearchInputView = null;
            }

            ParserSurface.RenderTransform = null;
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

        private async Task SearchCurrentGameAsync(string customQuery, CancellationToken cancellationToken)
        {
            if (!IsSessionActive(cancellationToken)) return;

            _isBusy = true;

            try
            {
                string query = string.IsNullOrWhiteSpace(customQuery)
                    ? (_shortcut.DisplayName ?? _shortcut.Name)
                    : customQuery.Trim();

                var results = await SearchGamesAsync(query, cancellationToken);

                if (!IsSessionActive(cancellationToken)) return;

                _gameResults.Clear();

                foreach (var result in results)
                    _gameResults.Add(result);

                HideStatus();

                if (_gameResults.Count == 0)
                {
                    ShowManualSearch(true, cancellationToken);
                    return;
                }

                ManualSearchPanel.Visibility = Visibility.Collapsed;
                GamesListBox.Visibility = Visibility.Visible;
                _mainWindow.SetHintsMode(HintPanel.HintMode.ParserGames);
                GamesListBox.SelectedIndex = 0;
                GamesListBox.ScrollIntoView(GamesListBox.SelectedItem);
                GamesListBox.Focus();
            }

            catch (OperationCanceledException)
            {
                // Expected when the parser is closed or the operation is canceled.
            }

            catch (SteamGridDbAuthException ex)
            {
                DebugLog.Error("Parser", ex, "SteamGridDB authorization failed.");

                if (IsSessionActive(cancellationToken))
                    ShowStatus(Locals.GetString("SteamGridDbApiKeyInvalid"));
            }

            catch (Exception ex)
            {
                DebugLog.Error("Parser", ex, "SteamGridDB game search failed.");

                if (IsSessionActive(cancellationToken))
                    ShowStatus(Locals.GetString("ErrorGridDB"));
            }

            finally
            {
                if (IsCurrentSession(cancellationToken))
                    _isBusy = false;
            }
        }

        private async void SearchInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            e.Handled = true;

            string query = SearchInputBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(query)) return;

            SystemProvider.HideSystemKeyboard();
            RestoreInputLanguage();

            ManualSearchPanel.Visibility = Visibility.Collapsed;

            ShowStatus(Locals.GetString("SearchCovers"));

            if (_sessionCts == null) return;

            _mainWindow.GetSound()?.PlayLaunchSound();
            await SearchCurrentGameAsync(query, _sessionCts.Token);
        }

        private async Task<List<ParserGameResult>> SearchGamesAsync(string query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string encoded = Uri.EscapeDataString(query.Trim());
            string url = $"{BaseUrl}/search/autocomplete/{encoded}";

            using (var response = await _httpClient.GetAsync(url, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    throw new SteamGridDbAuthException($"HTTP {(int)response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException("SteamGridDB returned HTTP " + $"{(int)response.StatusCode}.");

                string content = await response.Content.ReadAsStringAsync();
                cancellationToken.ThrowIfCancellationRequested();

                var json = JObject.Parse(content);
                var data = json["data"] as JArray;

                if (json["success"]?.ToObject<bool>() != true)
                    throw new InvalidOperationException("SteamGridDB returned success=false.");

                if (data == null)
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

        private async Task LoadCoversAsync(ParserGameResult game, CancellationToken sessionToken)
        {
            if (!IsSessionActive(sessionToken)) return;

            CancelCoverLoading();

            var cts = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
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
                var results = await GetSquareGridsAsync(game.Id, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(sessionToken)) return;

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

                using (var thumbnailSemaphore = new SemaphoreSlim(6, 6))
                {
                    var tasks = _gridResults.Select(async grid =>
                    {
                        await thumbnailSemaphore.WaitAsync(cancellationToken);

                        try
                        {
                            await LoadThumbnailAsync(grid, cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();

                            int completed = Interlocked.Increment(ref loadedCount);

                            await Dispatcher.InvokeAsync(() =>
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                    CoversProgressBar.Value = completed;
                            });
                        }

                        finally
                        {
                            thumbnailSemaphore.Release();
                        }
                    });

                    await Task.WhenAll(tasks);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(sessionToken)) return;

                CoversListBox.Opacity = 1.0;
                CoversProgressBar.Visibility = Visibility.Collapsed;
            }

            catch (OperationCanceledException)
            {
                // Expected when the user goes back or closes the parser.
            }

            catch (SteamGridDbAuthException ex)
            {
                DebugLog.Error("Parser", ex, "SteamGridDB authorization failed while loading covers.");

                if (IsSessionActive(sessionToken))
                {
                    CoversListBox.Opacity = 1.0;
                    CoversProgressBar.IsIndeterminate = false;
                    CoversProgressBar.Visibility = Visibility.Collapsed;

                    ShowStatus(Locals.GetString("SteamGridDbApiKeyInvalid"));
                }
            }

            catch (Exception ex)
            {
                DebugLog.Error("Parser", ex, "SteamGridDB cover search failed.");

                if (!IsSessionActive(sessionToken)) return;

                CoversListBox.Opacity = 1.0;
                CoversProgressBar.IsIndeterminate = false;
                CoversProgressBar.Visibility = Visibility.Collapsed;

                ShowStatus(Locals.GetString("ErrorGridDB"));
            }

            finally
            {
                if (_coversLoadCts == cts)
                    _coversLoadCts = null;

                cts.Dispose();

                if (IsCurrentSession(sessionToken))
                    _isBusy = false;
            }
        }

        private async Task<List<ParserGridResult>> GetSquareGridsAsync(int gameId, CancellationToken cancellationToken)
        {
            string url = $"{BaseUrl}/grids/game/{gameId}?dimensions=512x512,1024x1024&mimes=image/png,image/jpeg&nsfw=false&types=static";

            using (var response = await _httpClient.GetAsync(url, cancellationToken))
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    throw new SteamGridDbAuthException($"HTTP {(int)response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException("SteamGridDB returned HTTP " + $"{(int)response.StatusCode}.");

                string content = await response.Content.ReadAsStringAsync();
                cancellationToken.ThrowIfCancellationRequested();

                var json = JObject.Parse(content);
                var data = json["data"] as JArray;

                if (json["success"]?.ToObject<bool>() != true)
                    throw new InvalidOperationException("SteamGridDB returned success=false.");

                if (data == null)
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

        private async Task<List<ParserGridResult>> GetHeroesAsync(int gameId, CancellationToken cancellationToken)
        {
            string url = $"{BaseUrl}/heroes/game/{gameId}?dimensions=3840x1240&mimes=image/png,image/jpeg&nsfw=false&types=static";

            using (var response = await _httpClient.GetAsync(url, cancellationToken))
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    throw new SteamGridDbAuthException($"HTTP {(int)response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException("SteamGridDB returned HTTP " + $"{(int)response.StatusCode}.");

                string content = await response.Content.ReadAsStringAsync();
                cancellationToken.ThrowIfCancellationRequested();

                var json = JObject.Parse(content);
                var data = json["data"] as JArray;

                if (json["success"]?.ToObject<bool>() != true)
                    throw new InvalidOperationException("SteamGridDB returned success=false.");

                if (data == null)
                    return new List<ParserGridResult>();

                return data
                    .Where(item => item["url"] != null && (item["width"]?.ToObject<int>() ?? 0) >= 3840)
                    .Take(MaxBackgrounds)
                    .Select(item => new ParserGridResult
                    {
                        Id = item["id"]?.ToObject<int>() ?? 0,
                        Url = item["url"].ToString(),
                        Thumb = item["thumb"]?.ToString() ?? item["url"].ToString(),
                        Width = item["width"]?.ToObject<int>() ?? 0,
                        Height = item["height"]?.ToObject<int>() ?? 0
                    })
                    .ToList();
            }
        }

        private async Task LoadThumbnailAsync(ParserGridResult result, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var response = await _imageHttpClient.GetAsync(result.Thumb, cancellationToken))
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
                // A broken thumbnail must not cancel the remaining downloads.
            }
        }

        private async Task DownloadCoverAsync(ParserGridResult cover, CancellationToken cancellationToken)
        {
            _isBusy = true;

            CoversListBox.Visibility = Visibility.Collapsed;
            ShowStatus(Locals.GetString("SavingCover"));

            try
            {
                byte[] bytes;

                using (var response = await _imageHttpClient.GetAsync(cover.Url, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    bytes = await response.Content.ReadAsByteArrayAsync();
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(cancellationToken)) return;

                string coversDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shortcuts", "covers");

                Directory.CreateDirectory(coversDirectory);

                // Use the actual shortcut name instead of DisplayName.
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

                    cancellationToken.ThrowIfCancellationRequested();

                    using (var output = new FileStream(coverPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        resizedImage.Save(output, DrawingImaging.ImageFormat.Png);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(cancellationToken)) return;

                _mainWindow.GetLauncher()?.RefreshSelectedCover();

                Close();
            }

            catch (OperationCanceledException)
            {
                // Expected when the parser is closed during download.
            }

            catch (Exception ex)
            {
                DebugLog.Error("Parser", ex, "Failed to download or save cover.");

                if (IsSessionActive(cancellationToken))
                {
                    CoversListBox.Visibility = Visibility.Visible;
                    ShowStatus(Locals.GetString("LoadingCoversError") + ": " + ex.Message);
                }
            }

            finally
            {
                if (IsCurrentSession(cancellationToken))
                    _isBusy = false;
            }
        }

        private void ShowStatus(string text)
        {
            StatusText.Text = text;
            StatusText.Visibility = Visibility.Visible;
        }

        private void HideStatus()
        {
            StatusText.Visibility = Visibility.Collapsed;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            CancelSession();
            CancelCoverLoading();

            if (_manualSearchInputView != null)
            {
                _manualSearchInputView.PrimaryViewHiding -= ManualSearchInputView_Hiding;
                _manualSearchInputView.OcclusionsChanged -= ManualSearchInputView_OcclusionsChanged;
                _manualSearchInputView = null;
            }

            RestoreInputLanguage();

            _httpClient.Dispose();
            _imageHttpClient.Dispose();

            _sessionCts?.Dispose();
            _sessionCts = null;
        }

        private sealed class SteamGridDbAuthException : Exception
        {
            public SteamGridDbAuthException(string message) : base(message)
            {
            }
        }
    }

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
        public int Width { get; set; }
        public int Height { get; set; }

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