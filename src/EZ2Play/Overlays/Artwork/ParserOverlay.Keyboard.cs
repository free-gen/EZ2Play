
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.UI.ViewManagement.Core;

namespace EZ2Play.App
{
    /// <summary>
    /// Handles manual game search input and integration
    /// with the Windows system keyboard.
    /// </summary>
    /// <remarks>
    /// This partial class shares its state and XAML elements
    /// with ParserOverlay.xaml.cs.
    ///
    /// The keyboard lifecycle includes:
    /// - Capturing and restoring the input language.
    /// - Preparing and focusing the manual search field.
    /// - Displaying the system keyboard for gamepad input.
    /// - Moving the parser surface above keyboard occlusions.
    /// - Restoring the parser interface when the keyboard hides.
    ///
    /// The original dispatcher priority, keyboard delay,
    /// and event subscription order are preserved.
    /// </remarks>
    public partial class ParserOverlay
    {
        /// <summary>
        /// Saves the current input language and switches
        /// to English for manual game search.
        /// </summary>
        /// <remarks>
        /// The language is captured only once until
        /// RestoreInputLanguage is called.
        /// </remarks>
        private void CaptureInputLanguage()
        {
            if (_inputLanguageCaptured)
                return;

            _inputLanguageBeforeManualSearch =
                SystemProvider.ForceEnglishInputLanguage();

            _inputLanguageCaptured = true;
        }

        /// <summary>
        /// Restores the input language that was active
        /// before entering manual search.
        /// </summary>
        private void RestoreInputLanguage()
        {
            if (!_inputLanguageCaptured)
                return;

            SystemProvider.RestoreInputLanguage(
                _inputLanguageBeforeManualSearch);

            _inputLanguageBeforeManualSearch = null;
            _inputLanguageCaptured = false;
        }

        /// <summary>
        /// Opens the manual game search interface.
        /// </summary>
        /// <param name="showHint">
        /// Whether to display the message explaining
        /// that no matching games were found.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token for the current parser session.
        /// </param>
        /// <remarks>
        /// The method first prepares the search interface,
        /// waits for WPF layout, and focuses the input field.
        ///
        /// After a 100 ms delay, the Windows system keyboard
        /// is requested when a gamepad is connected.
        ///
        /// All session checks and input language restoration
        /// paths follow the original implementation.
        /// </remarks>
        private async void ShowManualSearch(
            bool showHint,
            CancellationToken cancellationToken)
        {
            if (!IsSessionActive(cancellationToken))
                return;

            _manualSearchFromNoMatches = showHint;

            HideStatus();

            GamesListBox.Visibility = Visibility.Collapsed;
            CoversListBox.Visibility = Visibility.Collapsed;

            _selectorCoordinator.ShowOverlay(
                SelectorCoordinator.Owner.Parser,
                null,
                null);

            SearchInputBox.Text = string.Empty;

            // Display the optional no-results hint.
            if (showHint)
            {
                ManualSearchHintText.Text =
                    Locals.GetString("ManualSearchHint");

                ManualSearchHintText.Visibility = Visibility.Visible;
            }
            else
            {
                ManualSearchHintText.Text = string.Empty;
                ManualSearchHintText.Visibility = Visibility.Collapsed;
            }

            // Show the manual search interface.
            ShowParserSurface();

            ManualSearchPanel.Visibility = Visibility.Visible;
            ParserSurface.RenderTransform = null;

            // Manual search uses the settings-style hint layout.
            _mainWindow.SetHintsMode(HintPanel.HintMode.Settings);

            // Switch input language to English before
            // focusing the search field.
            CaptureInputLanguage();

            SearchInputBox.UpdateLayout();

            // Wait for the WPF layout pass to complete.
            await Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Loaded);

            if (!IsSessionActive(cancellationToken))
            {
                RestoreInputLanguage();
                return;
            }

            // Focus the native TextBox before requesting
            // the system keyboard.
            SearchInputBox.Focus();
            Keyboard.Focus(SearchInputBox);
            SearchInputBox.SelectAll();

            try
            {
                // Preserve the original keyboard delay.
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

            // The system keyboard is requested only
            // when a gamepad is connected.
            if (_mainWindow.IsGamepadConnected)
            {
                try
                {
                    _manualSearchInputView =
                        CoreInputView.GetForCurrentView();

                    if (_manualSearchInputView != null)
                    {
                        // Subscribe to keyboard hiding events.
                        _manualSearchInputView.PrimaryViewHiding -=
                            ManualSearchInputView_Hiding;

                        _manualSearchInputView.PrimaryViewHiding +=
                            ManualSearchInputView_Hiding;

                        // Subscribe to keyboard occlusion changes.
                        _manualSearchInputView.OcclusionsChanged -=
                            ManualSearchInputView_OcclusionsChanged;

                        _manualSearchInputView.OcclusionsChanged +=
                            ManualSearchInputView_OcclusionsChanged;
                    }
                }
                catch
                {
                    // Preserve the original handling of
                    // CoreInputView initialization failures.
                }

                SystemProvider.ShowGamepadKeyboard();
            }
        }

        /// <summary>
        /// Adjusts the parser surface position when
        /// the system keyboard overlaps the search field.
        /// </summary>
        /// <remarks>
        /// The search field is translated into MainWindow
        /// coordinates and checked against the keyboard's
        /// reported occlusion rectangles.
        ///
        /// The parser surface is moved upward only when
        /// the calculated overlap requires it.
        ///
        /// The original 32-unit keyboard gap is preserved.
        /// </remarks>
        private void ManualSearchInputView_OcclusionsChanged(
            CoreInputView sender,
            CoreInputViewOcclusionsChangedEventArgs args)
        {
            if (ManualSearchPanel.Visibility != Visibility.Visible)
                return;

            double currentOffset =
                (ParserSurface.RenderTransform as TranslateTransform)?.Y ?? 0;

            Point inputPosition = SearchInputBox.TranslatePoint(
                new Point(0, 0),
                _mainWindow);

            double inputLeft = inputPosition.X;

            double inputRight =
                inputPosition.X + SearchInputBox.ActualWidth;

            double inputBottom =
                inputPosition.Y +
                SearchInputBox.ActualHeight -
                currentOffset;

            double requiredOffset = 0;

            foreach (var occlusion in args.Occlusions)
            {
                var rect = occlusion.OccludingRect;

                // Ignore occlusions outside the relevant area.
                if (rect.Y <= 0)
                    continue;

                if (rect.X >= inputRight ||
                    rect.X + rect.Width <= inputLeft)
                {
                    continue;
                }

                requiredOffset = Math.Max(
                    requiredOffset,
                    inputBottom + ManualSearchKeyboardGap - rect.Y);
            }

            // Restore the original position when no offset
            // is required.
            ParserSurface.RenderTransform = requiredOffset > 0
                ? new TranslateTransform(0, -requiredOffset)
                : null;
        }

        /// <summary>
        /// Handles the system keyboard hiding event.
        /// </summary>
        /// <remarks>
        /// Restores the input language, removes keyboard
        /// event handlers, and resets the parser transform.
        ///
        /// When manual search was opened because no games
        /// were found, the parser closes.
        ///
        /// Otherwise, the game results list is restored.
        /// </remarks>
        private void ManualSearchInputView_Hiding(
            CoreInputView sender,
            CoreInputViewHidingEventArgs args)
        {
            if (ManualSearchPanel.Visibility != Visibility.Visible)
                return;

            RestoreInputLanguage();

            // Remove CoreInputView subscriptions.
            if (_manualSearchInputView != null)
            {
                _manualSearchInputView.PrimaryViewHiding -=
                    ManualSearchInputView_Hiding;

                _manualSearchInputView.OcclusionsChanged -=
                    ManualSearchInputView_OcclusionsChanged;

                _manualSearchInputView = null;
            }

            // Restore the parser's original position.
            ParserSurface.RenderTransform = null;
            ManualSearchPanel.Visibility = Visibility.Collapsed;

            // Close when manual search followed an empty result.
            if (_manualSearchFromNoMatches)
            {
                _manualSearchFromNoMatches = false;

                Close();
                return;
            }

            // Return to the game search results.
            GamesListBox.Visibility = Visibility.Visible;

            _mainWindow.SetHintsMode(
                HintPanel.HintMode.ParserGames);

            if (_gameResults.Count > 0 &&
                GamesListBox.SelectedIndex < 0)
            {
                GamesListBox.SelectedIndex = 0;
            }

            GamesListBox.Focus();

            ScheduleParserSelectorUpdate();
        }

        /// <summary>
        /// Handles Enter in the manual game search field.
        /// </summary>
        /// <remarks>
        /// Non-empty queries close the system keyboard,
        /// restore the input language, and start a new
        /// SteamGridDB game search.
        ///
        /// Other keys are not handled by this method.
        /// </remarks>
        private async void SearchInputBox_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;

            string query = SearchInputBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(query))
                return;

            SystemProvider.HideSystemKeyboard();
            RestoreInputLanguage();

            ManualSearchPanel.Visibility = Visibility.Collapsed;

            HideParserSurface();

            ShowStatus(Locals.GetString("GridDBSearch"));

            if (_sessionCts == null)
                return;

            _mainWindow.GetSound()?.PlayLaunchSound();

            await SearchCurrentGameAsync(
                query,
                _sessionCts.Token);
        }
    }
}
