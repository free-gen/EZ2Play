
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace EZ2Play.App
{
    /// <summary>
    /// Handles artwork retrieval, thumbnail processing,
    /// and coordinates image persistence through ArtworkFileStore.
    /// </summary>
    /// <remarks>
    /// This partial class shares its fields, session state,
    /// and XAML controls with ParserOverlay.xaml.cs.
    ///
    /// Artwork is retrieved through SteamGridDbClient.
    /// Thumbnail decoding and file processing use background
    /// tasks, while UI updates remain on the WPF UI thread.
    ///
    /// All cancellation checks, concurrency limits,
    /// image constraints, and file operations preserve
    /// the original implementation.
    /// </remarks>
    public partial class ParserOverlay
    {
        /// <summary>
        /// Calculates the content viewport height for
        /// a fixed number of visible artwork rows.
        /// </summary>
        /// <param name="listBox">
        /// Artwork list whose outer margins are included
        /// in the height calculation.
        /// </param>
        /// <param name="visibleRows">
        /// Number of artwork rows to display.
        /// </param>
        /// <param name="mediaHeightKey">
        /// Resource key containing the artwork item height.
        /// </param>
        /// <remarks>
        /// The calculation includes rounded item heights,
        /// vertical gaps, and list margins.
        ///
        /// The height is assigned only when the current
        /// value differs by more than the original threshold.
        /// </remarks>
        private void UpdateAssetViewportHeight(
            ListBox listBox,
            int visibleRows,
            string mediaHeightKey)
        {
            double itemHeight =
                Math.Ceiling((double)FindResource(mediaHeightKey));

            Thickness gap =
                (Thickness)FindResource(UiScaleKeys.ParserAssetsGap);

            double verticalGap =
                Math.Ceiling(gap.Top + gap.Bottom);

            double contentHeight = Math.Ceiling(
                itemHeight * visibleRows +
                verticalGap * Math.Max(0, visibleRows - 1));

            double outerMargin =
                listBox.Margin.Top + listBox.Margin.Bottom;

            double targetHeight =
                Math.Ceiling(contentHeight + outerMargin);

            if (double.IsNaN(ParserContentGrid.Height) ||
                Math.Abs(ParserContentGrid.Height - targetHeight) > 0.01)
            {
                ParserContentGrid.Height = targetHeight;
            }
        }

        /// <summary>
        /// Retrieves cover artwork for the selected game
        /// and downloads its thumbnails.
        /// </summary>
        /// <param name="game">
        /// SteamGridDB game whose covers are requested.
        /// </param>
        /// <param name="sessionToken">
        /// Cancellation token of the active parser session.
        /// </param>
        /// <returns>
        /// A task representing the artwork loading operation.
        /// </returns>
        /// <remarks>
        /// Creates a linked cancellation source for this load.
        ///
        /// Thumbnail downloads are limited to six concurrent
        /// operations using SemaphoreSlim.
        ///
        /// Progress is updated after every four completed
        /// thumbnails or when the final thumbnail completes.
        ///
        /// The original error handling and cleanup sequence
        /// are preserved.
        /// </remarks>
        private async Task LoadCoversAsync(
            ParserGameResult game,
            CancellationToken sessionToken)
        {
            if (!IsSessionActive(sessionToken))
                return;

            CancelAssetLoading();

            var cts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    sessionToken);

            _assetLoadCts = cts;

            var cancellationToken = cts.Token;

            _isBusy = true;
            _mode = ParserMode.Covers;

            UpdateAssetViewportHeight(
                CoversListBox,
                CoversVisibleRows,
                UiScaleKeys.ParserCoverSize);

            _assetGame = game;
            _backgroundsLoaded = false;
            _heroResults.Clear();

            AssetTabsPanel.Visibility = Visibility.Visible;
            BackgroundsListBox.Visibility = Visibility.Collapsed;

            UpdateAssetTabs();

            _mainWindow.SetHintsMode(
                HintPanel.HintMode.ParserAssets);

            GamesListBox.Visibility = Visibility.Collapsed;
            CoversListBox.Visibility = Visibility.Collapsed;
            CoversListBox.Opacity = 0.45;

            ScheduleParserSelectorUpdate();

            AssetsProgressBar.Value = 0;
            AssetsProgressBar.IsIndeterminate = true;
            AssetsProgressBar.Visibility = Visibility.Visible;

            try
            {
                // Request square artwork metadata.
                var results =
                    await _steamGridDbClient.GetSquareGridsAsync(
                        game.Id,
                        MaxCovers,
                        cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(sessionToken))
                    return;

                _gridResults.Clear();

                foreach (var result in results)
                    _gridResults.Add(result);

                // Handle an empty cover result.
                if (_gridResults.Count == 0)
                {
                    AssetsProgressBar.IsIndeterminate = false;
                    AssetsProgressBar.Visibility = Visibility.Collapsed;
                    CoversListBox.Opacity = 1.0;

                    ShowStatus(Locals.GetString("NoResultsFound"));
                    return;
                }

                HideStatus();

                CoversListBox.Visibility = Visibility.Visible;

                UpdateAssetViewportHeight(
                    CoversListBox,
                    CoversVisibleRows,
                    UiScaleKeys.ParserCoverSize);

                CoversListBox.SelectedIndex = 0;
                CoversListBox.ScrollIntoView(CoversListBox.SelectedItem);
                CoversListBox.Focus();

                ScheduleParserSelectorUpdate();

                // Switch from indeterminate progress to
                // counting completed thumbnail downloads.
                AssetsProgressBar.IsIndeterminate = false;
                AssetsProgressBar.Minimum = 0;
                AssetsProgressBar.Maximum = _gridResults.Count;
                AssetsProgressBar.Value = 0;

                int loadedCount = 0;
                int totalThumbnails = _gridResults.Count;

                // Preserve the original concurrency limit.
                using (var thumbnailSemaphore = new SemaphoreSlim(6, 6))
                {
                    var tasks = _gridResults.Select(async grid =>
                    {
                        await thumbnailSemaphore.WaitAsync(
                            cancellationToken);

                        try
                        {
                            await LoadThumbnailAsync(
                                grid,
                                cancellationToken,
                                512,
                                512);

                            cancellationToken.ThrowIfCancellationRequested();

                            int completed =
                                Interlocked.Increment(ref loadedCount);

                            // Avoid updating the UI for every
                            // individual thumbnail.
                            if (completed % 4 == 0 ||
                                completed == totalThumbnails)
                            {
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    if (!cancellationToken.IsCancellationRequested &&
                                        ReferenceEquals(_assetLoadCts, cts))
                                    {
                                        AssetsProgressBar.Value = completed;
                                    }
                                });
                            }
                        }
                        finally
                        {
                            thumbnailSemaphore.Release();
                        }
                    });

                    await Task.WhenAll(tasks);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(sessionToken))
                    return;

                CoversListBox.Opacity = 1.0;
                AssetsProgressBar.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException)
            {
                // Expected when the user goes back
                // or closes the parser.
            }
            catch (SteamGridDbApiKeyMissingException ex)
            {
                DebugLog.Error(
                    "Parser",
                    ex,
                    "SteamGridDB API key is not configured.");

                if (IsSessionActive(sessionToken) &&
                    ReferenceEquals(_assetLoadCts, cts) &&
                    !cancellationToken.IsCancellationRequested)
                {
                    ShowErrorNotification(
                        Locals.GetString("GridDbApiMiss"));
                }
            }
            catch (SteamGridDbAuthException ex)
            {
                DebugLog.Error(
                    "Parser",
                    ex,
                    "SteamGridDB authorization failed while loading covers.");

                if (IsSessionActive(sessionToken))
                {
                    CoversListBox.Opacity = 1.0;
                    AssetsProgressBar.IsIndeterminate = false;
                    AssetsProgressBar.Visibility = Visibility.Collapsed;

                    HideParserSurface();

                    ShowErrorNotification(
                        Locals.GetString("GridDbInvalidApi"));
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Parser",
                    ex,
                    "SteamGridDB cover search failed.");

                if (!IsSessionActive(sessionToken))
                    return;

                CoversListBox.Opacity = 1.0;
                AssetsProgressBar.IsIndeterminate = false;
                AssetsProgressBar.Visibility = Visibility.Collapsed;

                HideParserSurface();

                ShowErrorNotification(
                    Locals.GetString("GridDBError"));
            }
            finally
            {
                // Only the active load may clear
                // the shared loading state.
                bool isCurrentLoad =
                    ReferenceEquals(_assetLoadCts, cts);

                if (isCurrentLoad)
                    _assetLoadCts = null;

                cts.Dispose();

                if (isCurrentLoad && IsCurrentSession(sessionToken))
                    _isBusy = false;
            }
        }

        /// <summary>
        /// Retrieves background artwork for the selected game
        /// and downloads its thumbnails.
        /// </summary>
        /// <param name="game">
        /// SteamGridDB game whose backgrounds are requested.
        /// </param>
        /// <param name="sessionToken">
        /// Cancellation token of the active parser session.
        /// </param>
        /// <returns>
        /// A task representing the background loading operation.
        /// </returns>
        /// <remarks>
        /// Requests background artwork with a minimum width
        /// parameter of 3840 pixels.
        ///
        /// Background thumbnails are decoded at width 768.
        /// Up to six downloads are processed concurrently.
        ///
        /// Successful results are cached for the current
        /// game through the _backgroundsLoaded flag.
        /// </remarks>
        private async Task LoadBackgroundsAsync(
            ParserGameResult game,
            CancellationToken sessionToken)
        {
            if (!IsSessionActive(sessionToken))
                return;

            CancelAssetLoading();

            var cts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    sessionToken);

            _assetLoadCts = cts;

            var cancellationToken = cts.Token;

            _isBusy = true;
            _mode = ParserMode.Backgrounds;

            UpdateAssetViewportHeight(
                BackgroundsListBox,
                BackgroundsVisibleRows,
                UiScaleKeys.ParserBackgroundHeight);

            UpdateAssetTabs();

            CoversListBox.Visibility = Visibility.Collapsed;
            BackgroundsListBox.Visibility = Visibility.Collapsed;
            BackgroundsListBox.Opacity = 0.45;

            ScheduleParserSelectorUpdate();

            AssetsProgressBar.Value = 0;
            AssetsProgressBar.IsIndeterminate = true;
            AssetsProgressBar.Visibility = Visibility.Visible;

            try
            {
                // Request background artwork metadata.
                var results = await _steamGridDbClient.GetHeroesAsync(
                    game.Id,
                    MaxBackgrounds,
                    3840,
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(sessionToken))
                    return;

                _heroResults.Clear();

                foreach (var result in results)
                    _heroResults.Add(result);

                _backgroundsLoaded = true;

                // Handle an empty background result.
                if (_heroResults.Count == 0)
                {
                    AssetsProgressBar.IsIndeterminate = false;
                    AssetsProgressBar.Visibility = Visibility.Collapsed;
                    BackgroundsListBox.Opacity = 1.0;

                    ShowStatus(Locals.GetString("NoResultsFound"));
                    return;
                }

                HideStatus();

                BackgroundsListBox.Visibility = Visibility.Visible;

                UpdateAssetViewportHeight(
                    BackgroundsListBox,
                    BackgroundsVisibleRows,
                    UiScaleKeys.ParserBackgroundHeight);

                BackgroundsListBox.SelectedIndex = 0;

                BackgroundsListBox.ScrollIntoView(
                    BackgroundsListBox.SelectedItem);

                BackgroundsListBox.Focus();

                ScheduleParserSelectorUpdate();

                AssetsProgressBar.IsIndeterminate = false;
                AssetsProgressBar.Minimum = 0;
                AssetsProgressBar.Maximum = _heroResults.Count;
                AssetsProgressBar.Value = 0;

                int loadedCount = 0;
                int totalThumbnails = _heroResults.Count;

                using (var thumbnailSemaphore = new SemaphoreSlim(6, 6))
                {
                    var tasks = _heroResults.Select(async hero =>
                    {
                        await thumbnailSemaphore.WaitAsync(
                            cancellationToken);

                        try
                        {
                            await LoadThumbnailAsync(
                                hero,
                                cancellationToken,
                                768);

                            cancellationToken.ThrowIfCancellationRequested();

                            int completed =
                                Interlocked.Increment(ref loadedCount);

                            if (completed % 4 == 0 ||
                                completed == totalThumbnails)
                            {
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    if (!cancellationToken.IsCancellationRequested &&
                                        ReferenceEquals(_assetLoadCts, cts))
                                    {
                                        AssetsProgressBar.Value = completed;
                                    }
                                });
                            }
                        }
                        finally
                        {
                            thumbnailSemaphore.Release();
                        }
                    });

                    await Task.WhenAll(tasks);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(sessionToken))
                    return;

                BackgroundsListBox.Opacity = 1.0;
                AssetsProgressBar.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException)
            {
                // Expected when the user goes back
                // or closes the parser.
            }
            catch (SteamGridDbApiKeyMissingException ex)
            {
                DebugLog.Error(
                    "Parser",
                    ex,
                    "SteamGridDB API key is not configured.");

                if (IsSessionActive(sessionToken) &&
                    ReferenceEquals(_assetLoadCts, cts) &&
                    !cancellationToken.IsCancellationRequested)
                {
                    ShowErrorNotification(
                        Locals.GetString("GridDbApiMiss"));
                }
            }
            catch (SteamGridDbAuthException ex)
            {
                DebugLog.Error(
                    "Parser",
                    ex,
                    "SteamGridDB authorization failed while loading backgrounds.");

                if (IsSessionActive(sessionToken))
                {
                    BackgroundsListBox.Opacity = 1.0;
                    AssetsProgressBar.IsIndeterminate = false;
                    AssetsProgressBar.Visibility = Visibility.Collapsed;

                    HideParserSurface();

                    ShowErrorNotification(
                        Locals.GetString("GridDbInvalidApi"));
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Parser",
                    ex,
                    "SteamGridDB background search failed.");

                if (!IsSessionActive(sessionToken))
                    return;

                BackgroundsListBox.Opacity = 1.0;
                AssetsProgressBar.IsIndeterminate = false;
                AssetsProgressBar.Visibility = Visibility.Collapsed;

                HideParserSurface();

                ShowErrorNotification(
                    Locals.GetString("GridDBError"));
            }
            finally
            {
                bool isCurrentLoad =
                    ReferenceEquals(_assetLoadCts, cts);

                if (isCurrentLoad)
                    _assetLoadCts = null;

                cts.Dispose();

                if (isCurrentLoad && IsCurrentSession(sessionToken))
                    _isBusy = false;
            }
        }

        /// <summary>
        /// Downloads and decodes an artwork thumbnail.
        /// </summary>
        /// <param name="result">
        /// Artwork result to update with the decoded image.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token controlling the download.
        /// </param>
        /// <param name="decodePixelWidth">
        /// Target bitmap decoding width.
        /// </param>
        /// <param name="decodePixelHeight">
        /// Optional bitmap decoding height.
        /// </param>
        /// <returns>
        /// A task representing the thumbnail operation.
        /// </returns>
        /// <remarks>
        /// Validates the downloaded image header before
        /// constructing a BitmapImage.
        ///
        /// Decoding is performed in a background task.
        /// The resulting image is frozen before assignment.
        ///
        /// Failure of one thumbnail does not cancel
        /// the remaining thumbnail operations.
        /// </remarks>
        private async Task LoadThumbnailAsync(
            ParserGridResult result,
            CancellationToken cancellationToken,
            int decodePixelWidth,
            int decodePixelHeight = 0)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte[] bytes =
                    await _steamGridDbClient.DownloadImageAsync(
                        result.Thumb,
                        cancellationToken,
                        MaxThumbnailDownloadBytes);

                cancellationToken.ThrowIfCancellationRequested();

                BitmapImage bitmap = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ArtworkFileStore.ValidateSourceImageHeader(bytes);

                    using (var stream = new MemoryStream(bytes))
                    {
                        var image = new BitmapImage();

                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.DecodePixelWidth = decodePixelWidth;

                        if (decodePixelHeight > 0)
                            image.DecodePixelHeight = decodePixelHeight;

                        image.StreamSource = stream;
                        image.EndInit();
                        image.Freeze();

                        cancellationToken.ThrowIfCancellationRequested();

                        return image;
                    }
                }, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                // Return to the UI thread to update the binding.
                result.ImageSource = bitmap;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Failure of one thumbnail does not
                // cancel the remaining thumbnails.
            }
        }

        /// <summary>
        /// Downloads, crops, and saves the selected cover.
        /// </summary>
        /// <param name="cover">
        /// SteamGridDB artwork result to download.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token of the active parser session.
        /// </param>
        /// <returns>
        /// A task representing the download and save operation.
        /// </returns>
        /// <remarks>
        /// The image is center-cropped and resized to 512x512.
        ///
        /// The resulting PNG is first written to a temporary
        /// file, verified, and then moved or replaced at
        /// its final destination.
        ///
        /// Temporary files are removed on failure.
        /// </remarks>
        private async Task DownloadCoverAsync(
            ParserGridResult cover,
            CancellationToken cancellationToken)
        {
            _isBusy = true;

            string coverTempPath = null;

            AssetsProgressBar.Value = 0;
            AssetsProgressBar.IsIndeterminate = true;
            AssetsProgressBar.Visibility = Visibility.Visible;

            try
            {
                byte[] bytes =
                    await _steamGridDbClient.DownloadImageAsync(
                        cover.Url,
                        cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(cancellationToken))
                    return;

                string shortcutName = _shortcut.Name;

                await Task.Run(() =>
                {
                    ArtworkFileStore.SaveCover(
                        bytes,
                        shortcutName,
                        cancellationToken,
                        path => coverTempPath = path);
                }, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(cancellationToken))
                    return;

                _mainWindow.GetLauncher()?.RefreshSelectedCover();

                Close();
            }
            catch (OperationCanceledException)
            {
                // Expected when the parser is closed during download.
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Parser",
                    ex,
                    "Failed to download or save cover.");

                if (IsSessionActive(cancellationToken))
                {
                    CoversListBox.Visibility = Visibility.Visible;

                    ShowStatus(
                        Locals.GetString("ParserOperationError") +
                        ": " + ex.Message);
                }
            }
            finally
            {
                if (IsCurrentSession(cancellationToken))
                {
                    AssetsProgressBar.IsIndeterminate = false;
                    AssetsProgressBar.Visibility = Visibility.Collapsed;
                    AssetsProgressBar.Value = 0;
                }

                // Remove an unfinished temporary file.
                if (coverTempPath != null)
                {
                    try
                    {
                        if (File.Exists(coverTempPath))
                            File.Delete(coverTempPath);
                    }
                    catch
                    {
                    }
                }

                if (IsCurrentSession(cancellationToken))
                    _isBusy = false;
            }
        }

        /// <summary>
        /// Downloads, validates, and saves the selected
        /// background image.
        /// </summary>
        /// <param name="background">
        /// SteamGridDB background result to download.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token of the active parser session.
        /// </param>
        /// <returns>
        /// A task representing the download and save operation.
        /// </returns>
        /// <remarks>
        /// Background images must have a minimum width
        /// of 3840 pixels.
        ///
        /// The original image format is preserved as PNG
        /// or JPEG, without resizing or recompression.
        ///
        /// The downloaded file is validated before replacing
        /// the existing background. Old background files with
        /// other supported extensions are then removed.
        /// </remarks>
        private async Task DownloadBackgroundAsync(
            ParserGridResult background,
            CancellationToken cancellationToken)
        {
            _isBusy = true;

            AssetsProgressBar.Value = 0;
            AssetsProgressBar.IsIndeterminate = true;
            AssetsProgressBar.Visibility = Visibility.Visible;

            string tempPath = null;

            try
            {
                byte[] bytes =
                    await _steamGridDbClient.DownloadImageAsync(
                        background.Url,
                        cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(cancellationToken))
                    return;

                string shortcutName = _shortcut.Name;

                await Task.Run(() =>
                {
                    ArtworkFileStore.SaveBackground(
                        bytes,
                        shortcutName,
                        cancellationToken,
                        path => tempPath = path);
                }, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(cancellationToken))
                    return;

                _mainWindow.InvalidateBackgroundCache(
                    _shortcut.FullPath);

                _mainWindow.RefreshSelectedBackground();

                Close();
            }
            catch (OperationCanceledException)
            {
                // Expected when the parser is closed during download.
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Parser",
                    ex,
                    "Failed to download or save background.");

                if (IsSessionActive(cancellationToken))
                {
                    BackgroundsListBox.Visibility = Visibility.Visible;

                    ShowStatus(
                        Locals.GetString("ParserOperationError") +
                        ": " + ex.Message);
                }
            }
            finally
            {
                if (IsCurrentSession(cancellationToken))
                {
                    AssetsProgressBar.IsIndeterminate = false;
                    AssetsProgressBar.Visibility = Visibility.Collapsed;
                    AssetsProgressBar.Value = 0;
                }

                // Remove an unfinished temporary file.
                if (tempPath != null)
                {
                    try
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    catch
                    {
                    }
                }

                if (IsCurrentSession(cancellationToken))
                    _isBusy = false;
            }
        }
    }
}
