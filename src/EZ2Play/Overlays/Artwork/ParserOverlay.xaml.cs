
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.UI.ViewManagement.Core;

using Rectangle = System.Windows.Shapes.Rectangle;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using DrawingImaging = System.Drawing.Imaging;

namespace EZ2Play.App
{
    /// <summary>
    /// Provides the SteamGridDB artwork search interface,
    /// including game selection, artwork previews,
    /// downloads, and manual search.
    /// </summary>
    /// <remarks>
    /// This file owns shared state, initialization, session
    /// cancellation, overlay visibility, and disposal.
    ///
    /// Navigation, keyboard input, game search, and artwork
    /// loading are implemented in separate partial files.
    ///
    /// SelectorCoordinator manages selection highlighting.
    /// SystemProvider provides Windows keyboard integration.
    ///
    /// SteamGridDbClient downloads artwork, while
    /// ArtworkFileStore validates and saves image files.
    ///
    /// Asynchronous operations are associated with a parser
    /// session to prevent updates from outdated requests.
    /// </remarks>
    public partial class ParserOverlay : UserControl, IDisposable
    {
        // Artwork grid configuration.
        private const int GridColumns = 4;
        private const int BackgroundColumns = 2;

        // Maximum number of visible artwork rows.
        private const int CoversVisibleRows = 2;
        private const int BackgroundsVisibleRows = 3;

        // SteamGridDB result limits.
        private const int MaxGames = 15;
        private const int MaxCovers = 30;
        private const int MaxBackgrounds = 30;

        // Maximum downloaded thumbnail size.
        private const int MaxThumbnailDownloadBytes = 12 * 1024 * 1024;

        // Animation and timeout configuration.
        private const double FadeDuration = 0.1;
        private const int GameSearchTimeoutSeconds = 3;
        private const double ManualSearchKeyboardGap = 32;
        private const double StatusAutoHideSeconds = 5;

        // Application dependencies.
        private readonly InputHandler _inputHandler;
        private readonly MainWindow _mainWindow;
        private readonly AppConfig _config;
        private readonly SelectorCoordinator _selectorCoordinator;

        // Status notification timer.
        private readonly DispatcherTimer _statusTimer;

        // SteamGridDB API client.
        private readonly SteamGridDbClient _steamGridDbClient;

        // Session cancellation and lifetime.
        private CancellationTokenSource _sessionCts;
        private bool _disposed;

        // Input language restoration for manual search.
        private CultureInfo _inputLanguageBeforeManualSearch;
        private bool _inputLanguageCaptured;

        // Observable collections used by the three result lists.
        private readonly ObservableCollection<ParserGameResult> _gameResults =
            new ObservableCollection<ParserGameResult>();

        private readonly ObservableCollection<ParserGridResult> _gridResults =
            new ObservableCollection<ParserGridResult>();

        private readonly ObservableCollection<ParserGridResult> _heroResults =
            new ObservableCollection<ParserGridResult>();

        // Current game and artwork selection.
        private ShortcutInfo _shortcut;
        private ParserGameResult _assetGame;
        private ParserMode _mode = ParserMode.Games;

        private bool _backgroundsLoaded;
        private bool _isBusy;

        // Cancellation of artwork loading operations.
        private CancellationTokenSource _assetLoadCts;

        // Windows keyboard integration.
        private CoreInputView _manualSearchInputView;
        private bool _manualSearchFromNoMatches;

        // Selector update and closing state.
        private bool _parserSelectorUpdateScheduled;
        private bool _isClosing;

        /// <summary>
        /// Identifies the currently active parser screen.
        /// </summary>
        private enum ParserMode
        {
            Games,
            Covers,
            Backgrounds
        }

        /// <summary>
        /// Initializes the artwork parser and connects
        /// its lists to observable result collections.
        /// </summary>
        /// <remarks>
        /// The status timer hides messages automatically.
        /// When the parser surface is hidden and no operation
        /// is active, its callback closes the overlay instead.
        ///
        /// Selection handlers remain registered until Dispose.
        /// </remarks>
        public ParserOverlay(
            InputHandler inputHandler,
            MainWindow mainWindow,
            SelectorCoordinator selectorCoordinator)
        {
            InitializeComponent();
            Locals.ApplyLocalization(this);

            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(StatusAutoHideSeconds)
            };

            _statusTimer.Tick += (sender, args) =>
            {
                if (!_isBusy &&
                    ParserSurface.Visibility == Visibility.Collapsed)
                {
                    Close();
                    return;
                }

                HideStatus();
            };

            _inputHandler = inputHandler;
            _mainWindow = mainWindow;
            _config = _mainWindow.GetConfig();
            _selectorCoordinator = selectorCoordinator;

            _steamGridDbClient = new SteamGridDbClient();

            GamesListBox.ItemsSource = _gameResults;
            CoversListBox.ItemsSource = _gridResults;
            BackgroundsListBox.ItemsSource = _heroResults;

            GamesListBox.SelectionChanged += OnParserSelectionChanged;
            CoversListBox.SelectionChanged += OnParserSelectionChanged;
            BackgroundsListBox.SelectionChanged += OnParserSelectionChanged;

            Opacity = 0;
            Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Checks whether a token belongs to the current
        /// parser session and the overlay is not disposed.
        /// </summary>
        /// <remarks>
        /// Unlike IsSessionActive, this check does not require
        /// the token to be uncancelled or the overlay to be visible.
        ///
        /// It is also used when completing cleanup operations
        /// associated with the current session.
        /// </remarks>
        private bool IsCurrentSession(CancellationToken cancellationToken)
        {
            return !_disposed &&
                   _sessionCts != null &&
                   cancellationToken == _sessionCts.Token;
        }

        /// <summary>
        /// Checks whether the session is current,
        /// not cancelled, and currently visible.
        /// </summary>
        /// <remarks>
        /// Used to prevent asynchronous operations from
        /// updating the interface after cancellation,
        /// overlay closure, or session replacement.
        /// </remarks>
        private bool IsSessionActive(CancellationToken cancellationToken)
        {
            return IsCurrentSession(cancellationToken) &&
                   !cancellationToken.IsCancellationRequested &&
                   Visibility == Visibility.Visible;
        }

        /// <summary>
        /// Cancels asynchronous operations associated
        /// with the current parser session.
        /// </summary>
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

        /// <summary>
        /// Cancels the current artwork loading operation
        /// and resets progress and opacity indicators.
        /// </summary>
        /// <remarks>
        /// Artwork loading has a separate cancellation source
        /// from the parser session itself.
        ///
        /// This method requests cancellation and restores
        /// the progress UI without changing the current
        /// parser session.
        /// </remarks>
        private void CancelAssetLoading()
        {
            _assetLoadCts?.Cancel();

            AssetsProgressBar.IsIndeterminate = false;
            AssetsProgressBar.Visibility = Visibility.Collapsed;
            AssetsProgressBar.Value = 0;

            CoversListBox.Opacity = 1.0;
            BackgroundsListBox.Opacity = 1.0;
        }

        /// <summary>
        /// Makes the main parser surface and content visible.
        /// </summary>
        private void ShowParserSurface()
        {
            ParserSurface.Visibility = Visibility.Visible;
            ParserContentGrid.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hides the main parser surface and content.
        /// </summary>
        private void HideParserSurface()
        {
            ParserSurface.Visibility = Visibility.Collapsed;
            ParserContentGrid.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Opens the parser for the currently selected shortcut.
        /// </summary>
        /// <remarks>
        /// Initializes a new cancellation session and resets
        /// previous game and artwork selections.
        ///
        /// Switches input handling to Parser mode, activates
        /// the overlay selector, and starts the fade-in animation.
        ///
        /// API authorization is configured before the initial
        /// asynchronous game search begins.
        ///
        /// The original async void signature and initialization
        /// order are preserved.
        /// </remarks>
        public async void Open()
        {
            if (_disposed || Visibility == Visibility.Visible)
                return;

            _mainWindow.GetSound()?.PlayLaunchSound();

            var launcher = _mainWindow.GetLauncher();

            if (launcher == null ||
                launcher.SelectedIndex < 0 ||
                launcher.SelectedIndex >= launcher.Shortcuts.Length)
            {
                return;
            }

            _isClosing = false;

            _sessionCts?.Dispose();
            _sessionCts = new CancellationTokenSource();

            CancellationToken cancellationToken = _sessionCts.Token;

            _shortcut = launcher.Shortcuts[launcher.SelectedIndex];
            _mode = ParserMode.Games;

            ParserContentGrid.Height = double.NaN;

            HideParserSurface();

            _isBusy = false;
            _manualSearchFromNoMatches = false;

            _gameResults.Clear();
            _gridResults.Clear();
            _heroResults.Clear();

            _assetGame = null;
            _backgroundsLoaded = false;

            AssetTabsPanel.Visibility = Visibility.Collapsed;
            GamesListBox.Visibility = Visibility.Visible;
            CoversListBox.Visibility = Visibility.Collapsed;
            BackgroundsListBox.Visibility = Visibility.Collapsed;
            ManualSearchPanel.Visibility = Visibility.Collapsed;

            ShowStatus(Locals.GetString("GridDBSearch"));

            _inputHandler.SetMode(InputHandler.InputMode.Parser);

            _selectorCoordinator.ShowOverlay(
                SelectorCoordinator.Owner.Parser,
                null,
                null);

            Visibility = Visibility.Visible;

            ScheduleParserSelectorUpdate();

            var fadeIn = OverlayAnimations.CreateFadeIn(FadeDuration);

            BeginAnimation(OpacityProperty, fadeIn);

            ConfigureApiAuthorization();

            await SearchCurrentGameAsync(null, cancellationToken);
        }

        /// <summary>
        /// Closes the parser and cancels its active operations.
        /// </summary>
        /// <remarks>
        /// Cancels the current session and artwork loading,
        /// then hides the parser selector and status message.
        ///
        /// Windows keyboard event handlers are detached before
        /// hiding the keyboard and restoring the input language.
        ///
        /// The fade-out completion callback restores the main
        /// selector, input mode, and context hints.
        ///
        /// This order must remain unchanged.
        /// </remarks>
        public void Close()
        {
            if (Visibility != Visibility.Visible || _isClosing)
                return;

            _isClosing = true;

            _mainWindow.GetSound()?.PlayBackSound();

            CancelSession();
            CancelAssetLoading();
            HideStatus();

            _selectorCoordinator.Hide(
                SelectorCoordinator.Owner.Parser);

            if (_manualSearchInputView != null)
            {
                _manualSearchInputView.PrimaryViewHiding -=
                    ManualSearchInputView_Hiding;

                _manualSearchInputView.OcclusionsChanged -=
                    ManualSearchInputView_OcclusionsChanged;

                _manualSearchInputView = null;
            }

            ParserSurface.RenderTransform = null;

            SystemProvider.HideSystemKeyboard();
            RestoreInputLanguage();

            ManualSearchPanel.Visibility = Visibility.Collapsed;
            _manualSearchFromNoMatches = false;

            var fadeOut = OverlayAnimations.CreateFadeOut(
                Opacity,
                FadeDuration);

            fadeOut.Completed += (s, e) =>
            {
                Visibility = Visibility.Collapsed;

                _selectorCoordinator.ShowMain();

                _inputHandler.SetMode(InputHandler.InputMode.Main);
                _mainWindow.SetHintsMode(HintPanel.HintMode.Main);
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }

        /// <summary>
        /// Handles the Back action.
        /// </summary>
        /// <remarks>
        /// Returns from artwork selection to the game list.
        /// Closes the parser when already in game search mode.
        /// </remarks>
        public void Back()
        {
            if (_mode == ParserMode.Covers ||
                _mode == ParserMode.Backgrounds)
            {
                _mainWindow.GetSound()?.PlayBackSound();

                CancelAssetLoading();

                _mode = ParserMode.Games;
                ParserContentGrid.Height = double.NaN;

                AssetTabsPanel.Visibility = Visibility.Collapsed;
                CoversListBox.Visibility = Visibility.Collapsed;
                BackgroundsListBox.Visibility = Visibility.Collapsed;
                GamesListBox.Visibility = Visibility.Visible;

                _mainWindow.SetHintsMode(
                    HintPanel.HintMode.ParserGames);

                HideStatus();

                if (_gameResults.Count > 0 &&
                    GamesListBox.SelectedIndex < 0)
                {
                    GamesListBox.SelectedIndex = 0;
                }

                GamesListBox.Focus();

                ScheduleParserSelectorUpdate();

                return;
            }

            Close();
        }

        /// <summary>
        /// Confirms the currently selected game,
        /// cover, or background.
        /// </summary>
        public async void Confirm()
        {
            if (_isBusy || _sessionCts == null)
                return;

            if (_mode == ParserMode.Games)
            {
                var game =
                    GamesListBox.SelectedItem as ParserGameResult;

                if (game != null)
                {
                    _mainWindow.GetSound()?.PlayLaunchSound();

                    await LoadCoversAsync(
                        game,
                        _sessionCts.Token);
                }

                return;
            }

            if (_mode == ParserMode.Backgrounds)
            {
                var background =
                    BackgroundsListBox.SelectedItem as ParserGridResult;

                if (background != null)
                {
                    _mainWindow.GetSound()?.PlayLaunchSound();

                    await DownloadBackgroundAsync(
                        background,
                        _sessionCts.Token);
                }

                return;
            }

            var cover = CoversListBox.SelectedItem as ParserGridResult;

            if (cover != null)
            {
                _mainWindow.GetSound()?.PlayLaunchSound();

                await DownloadCoverAsync(
                    cover,
                    _sessionCts.Token);
            }
        }

        /// <summary>
        /// Displays a parser status message and starts
        /// the automatic hiding timer.
        /// </summary>
        private void ShowStatus(string text)
        {
            _statusTimer.Stop();

            StatusText.Text = text;

            ParserStatusSurface.Visibility = Visibility.Visible;
            StatusText.Visibility = Visibility.Visible;

            _statusTimer.Start();
        }

        /// <summary>
        /// Hides the parser status message.
        /// </summary>
        private void HideStatus()
        {
            _statusTimer.Stop();

            StatusText.Visibility = Visibility.Collapsed;
            ParserStatusSurface.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Displays an application-level parser error
        /// notification and closes the parser.
        /// </summary>
        private void ShowErrorNotification(string text)
        {
            _mainWindow.ShowParserErrorNotification(text);

            Close();
        }

        /// <summary>
        /// Cancels outstanding operations, removes event
        /// handlers, and releases the SteamGridDB client.
        /// </summary>
        /// <remarks>
        /// Prevents repeated disposal and detaches selection
        /// handlers from all three result lists.
        ///
        /// Requests session and artwork loading cancellation,
        /// stops the status timer, and removes Windows keyboard
        /// event subscriptions.
        ///
        /// Restores the input language before disposing
        /// SteamGridDbClient and the session token source.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            GamesListBox.SelectionChanged -= OnParserSelectionChanged;
            CoversListBox.SelectionChanged -= OnParserSelectionChanged;
            BackgroundsListBox.SelectionChanged -= OnParserSelectionChanged;

            CancelSession();
            CancelAssetLoading();

            _statusTimer.Stop();

            if (_manualSearchInputView != null)
            {
                _manualSearchInputView.PrimaryViewHiding -=
                    ManualSearchInputView_Hiding;

                _manualSearchInputView.OcclusionsChanged -=
                    ManualSearchInputView_OcclusionsChanged;

                _manualSearchInputView = null;
            }

            RestoreInputLanguage();

            _steamGridDbClient.Dispose();

            _sessionCts?.Dispose();
            _sessionCts = null;
        }
    }
}
