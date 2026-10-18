
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace EZ2Play.App
{
    /// <summary>
    /// Handles SteamGridDB game searches and API authorization
    /// for the parser overlay.
    /// </summary>
    /// <remarks>
    /// This partial class shares its state and UI elements
    /// with ParserOverlay.xaml.cs.
    ///
    /// Search operations are associated with the current
    /// parser session and use a separate timeout token.
    ///
    /// Manual search field and keyboard handling are
    /// implemented in ParserOverlay.Keyboard.cs.
    /// </remarks>
    public partial class ParserOverlay
    {
        /// <summary>
        /// Configures SteamGridDB authorization using
        /// the API key from application settings.
        /// </summary>
        /// <remarks>
        /// A configured key takes precedence over the Worker.
        /// Without a key, the Worker provides API access.
        ///
        /// Invalid keys use the existing authorization
        /// error handling without falling back.
        /// </remarks>
        private void ConfigureApiAuthorization()
        {
            string apiKey = _config?.SteamGridDbApiKey?.Trim();

            _steamGridDbClient.ConfigureAuthorization(apiKey);
        }

        /// <summary>
        /// Opens manual game search when the parser
        /// is displaying game search results.
        /// </summary>
        /// <remarks>
        /// Manual search cannot be opened while another
        /// operation is in progress or while the parser
        /// is displaying artwork.
        ///
        /// Keyboard initialization is delegated to
        /// ShowManualSearch without changing its behavior.
        /// </remarks>
        public void Search()
        {
            if (_isBusy)
                return;

            if (_mode != ParserMode.Games)
                return;

            if (GamesListBox.Visibility != Visibility.Visible)
                return;

            if (_sessionCts == null)
                return;

            ShowManualSearch(false, _sessionCts.Token);
        }

        /// <summary>
        /// Searches SteamGridDB for games matching the
        /// selected shortcut or a custom search query.
        /// </summary>
        /// <param name="customQuery">
        /// Optional manually entered game title.
        /// When null or whitespace, the current shortcut
        /// name is used.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token associated with the
        /// current parser session.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous search.
        /// </returns>
        /// <remarks>
        /// Each search uses a linked cancellation token
        /// with the original three-second timeout.
        ///
        /// Results are added to the observable collection
        /// only while the current session remains active.
        ///
        /// An empty result opens manual search with a hint.
        ///
        /// API key, authorization, cancellation, and general
        /// errors retain their original handling paths.
        /// </remarks>
        private async Task SearchCurrentGameAsync(
            string customQuery,
            CancellationToken cancellationToken)
        {
            if (!IsSessionActive(cancellationToken))
                return;

            _isBusy = true;

            try
            {
                // Use the selected shortcut name unless
                // a custom search query was provided.
                string query = string.IsNullOrWhiteSpace(customQuery)
                    ? (_shortcut.DisplayName ?? _shortcut.Name)
                    : customQuery.Trim();

                List<ParserGameResult> results;

                // Link the search timeout to the current
                // parser session cancellation token.
                using (var timeoutCts =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken))
                {
                    timeoutCts.CancelAfter(
                        TimeSpan.FromSeconds(GameSearchTimeoutSeconds));

                    try
                    {
                        results = await _steamGridDbClient.SearchGamesAsync(
                            query,
                            MaxGames,
                            timeoutCts.Token);
                    }
                    catch (OperationCanceledException) when (
                        !cancellationToken.IsCancellationRequested &&
                        timeoutCts.IsCancellationRequested)
                    {
                        // Handle a search timeout separately
                        // from user-initiated cancellation.
                        if (IsSessionActive(cancellationToken))
                        {
                            ShowErrorNotification(
                                Locals.GetString("GridDBError"));
                        }

                        return;
                    }
                }

                // Discard results from an inactive session.
                if (!IsSessionActive(cancellationToken))
                    return;

                // Replace the current game search results.
                _gameResults.Clear();

                foreach (var result in results)
                    _gameResults.Add(result);

                HideStatus();

                // Allow the user to enter a different title
                // when SteamGridDB returns no matches.
                if (_gameResults.Count == 0)
                {
                    ShowManualSearch(true, cancellationToken);
                    return;
                }

                // Display the game results and restore focus.
                ShowParserSurface();

                ManualSearchPanel.Visibility = Visibility.Collapsed;
                GamesListBox.Visibility = Visibility.Visible;

                _mainWindow.SetHintsMode(
                    HintPanel.HintMode.ParserGames);

                GamesListBox.SelectedIndex = 0;
                GamesListBox.ScrollIntoView(GamesListBox.SelectedItem);
                GamesListBox.Focus();

                ScheduleParserSelectorUpdate();
            }
            catch (OperationCanceledException)
            {
                // Expected when the user closes the parser
                // or cancels the current operation.
            }
            catch (SteamGridDbApiKeyMissingException ex)
            {
                DebugLog.Error(
                    "Parser",
                    ex,
                    "SteamGridDB API key is not configured.");

                if (IsSessionActive(cancellationToken))
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
                    "SteamGridDB authorization failed.");

                if (IsSessionActive(cancellationToken))
                {
                    ShowErrorNotification(
                        Locals.GetString("GridDbInvalidApi"));
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Parser",
                    ex,
                    "SteamGridDB game search failed.");

                if (IsSessionActive(cancellationToken))
                {
                    ShowErrorNotification(
                        Locals.GetString("GridDBError"));
                }
            }
            finally
            {
                // Clear the busy state only if this operation
                // still belongs to the current parser session.
                if (IsCurrentSession(cancellationToken))
                    _isBusy = false;
            }
        }
    }
}
