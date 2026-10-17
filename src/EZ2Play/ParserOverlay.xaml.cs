using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Rectangle = System.Windows.Shapes.Rectangle;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using DrawingImaging = System.Drawing.Imaging;

using Windows.UI.ViewManagement.Core;

namespace EZ2Play.App
{
    public sealed class ParserAssetsPanel : Panel
    {
        private double _layoutCellHeight;
        private double _layoutVerticalGap;
        private double _layoutHorizontalGap;

        public ParserAssetsPanel()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
        }

        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(nameof(Columns), typeof(int), typeof(ParserAssetsPanel),
                new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty GapProperty =
            DependencyProperty.Register(nameof(Gap), typeof(Thickness), typeof(ParserAssetsPanel),
                new FrameworkPropertyMetadata(new Thickness(0), FrameworkPropertyMetadataOptions.AffectsMeasure));

        public int Columns
        {
            get => (int)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public Thickness Gap
        {
            get => (Thickness)GetValue(GapProperty);
            set => SetValue(GapProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            int columns = Math.Max(1, Columns);
            int rows = (InternalChildren.Count + columns - 1) / columns;

            if (InternalChildren.Count == 0)
            {
                return new Size();
            }

            double horizontalGap = Gap.Left + Gap.Right;
            double verticalGap = Gap.Top + Gap.Bottom;

            double maxWidth = 0;
            double maxHeight = 0;

            bool infiniteWidth = double.IsInfinity(availableSize.Width);

            // Natural width is needed only when the parent provides no width limit.
            if (infiniteWidth)
            {
                foreach (UIElement child in InternalChildren)
                {
                    child.Measure(new Size(
                        double.PositiveInfinity,
                        double.PositiveInfinity));

                    maxWidth = Math.Max(maxWidth, child.DesiredSize.Width);
                }
            }

            double cellWidth = infiniteWidth
                ? maxWidth
                : Math.Max(0,
                    (availableSize.Width - horizontalGap * (columns - 1)) / columns);

            // Measure each item using its actual column width.
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(cellWidth, double.PositiveInfinity));

                maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
            }

            _layoutCellHeight = Math.Ceiling(maxHeight);
            _layoutVerticalGap = Math.Ceiling(verticalGap);
            _layoutHorizontalGap = Math.Ceiling(horizontalGap);

            double desiredWidth = double.IsInfinity(availableSize.Width)
                ? cellWidth * columns + horizontalGap * (columns - 1) : availableSize.Width;

            double desiredHeight = _layoutCellHeight * rows + _layoutVerticalGap * Math.Max(0, rows - 1);

            return new Size(desiredWidth, desiredHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int columns = Math.Max(1, Columns);

            double horizontalGap = _layoutHorizontalGap;
            double verticalGap = _layoutVerticalGap;

            double cellWidth =
                Math.Max(0, (finalSize.Width - horizontalGap * (columns - 1)) / columns);

            double cellHeight = _layoutCellHeight;

            for (int index = 0; index < InternalChildren.Count; index++)
            {
                int row = index / columns;
                int column = index % columns;

                double x = Math.Round(column * (cellWidth + horizontalGap));
                double y = row * (cellHeight + verticalGap);
                double right = Math.Round((column + 1) * (cellWidth + horizontalGap) - horizontalGap);

                InternalChildren[index].Arrange(new Rect(x, y, right - x, cellHeight));
            }

            return finalSize;
        }
    }

    public partial class ParserOverlay : UserControl, IDisposable
    {
        private const int GridColumns = 4;
        private const int BackgroundColumns = 2;
        private const int CoversVisibleRows = 2;
        private const int BackgroundsVisibleRows = 3;
        private const int MaxGames = 15;
        private const int MaxCovers = 30;
        private const int MaxBackgrounds = 30;
        private const int MaxSourceImageDimension = 16384;
        private const long MaxSourceImagePixels = 40_000_000L;
        private const int MaxThumbnailDownloadBytes = 12 * 1024 * 1024;
        private const double FadeDuration = 0.1;
        private const int GameSearchTimeoutSeconds = 3;
        private const double ManualSearchKeyboardGap = 32;

        private readonly InputHandler _inputHandler;
        private readonly MainWindow _mainWindow;
        private readonly AppConfig _config;
        private readonly SelectorCoordinator _selectorCoordinator;
        private const double StatusAutoHideSeconds = 5;
        private readonly DispatcherTimer _statusTimer;

        private readonly SteamGridDbClient _steamGridDbClient;

        private CancellationTokenSource _sessionCts;
        private bool _disposed;

        private CultureInfo _inputLanguageBeforeManualSearch;
        private bool _inputLanguageCaptured;

        private readonly ObservableCollection<ParserGameResult> _gameResults = new ObservableCollection<ParserGameResult>();
        private readonly ObservableCollection<ParserGridResult> _gridResults = new ObservableCollection<ParserGridResult>();
        private readonly ObservableCollection<ParserGridResult> _heroResults = new ObservableCollection<ParserGridResult>();

        private ShortcutInfo _shortcut;
        private ParserGameResult _assetGame;
        private ParserMode _mode = ParserMode.Games;
        private bool _backgroundsLoaded;
        private bool _isBusy;

        private CancellationTokenSource _assetLoadCts;
        private CoreInputView _manualSearchInputView;
        private bool _manualSearchFromNoMatches;
        private bool _parserSelectorUpdateScheduled;
        private bool _isClosing;

        private enum ParserMode
        {
            Games,
            Covers,
            Backgrounds
        }

        public ParserOverlay(InputHandler inputHandler, MainWindow mainWindow, SelectorCoordinator selectorCoordinator)
        {
            InitializeComponent();
            Locals.ApplyLocalization(this);

            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(StatusAutoHideSeconds)
            };

            _statusTimer.Tick += (sender, args) =>
            {
                if (!_isBusy && ParserSurface.Visibility == Visibility.Collapsed)
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

        private void ConfigureApiAuthorization()
        {
            string apiKey = _config?.SteamGridDbApiKey?.Trim();
            _steamGridDbClient.ConfigureFallbackAuthorization(apiKey);
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

        private void CancelAssetLoading()
        {
            _assetLoadCts?.Cancel();

            AssetsProgressBar.IsIndeterminate = false;
            AssetsProgressBar.Visibility = Visibility.Collapsed;
            AssetsProgressBar.Value = 0;

            CoversListBox.Opacity = 1.0;
            BackgroundsListBox.Opacity = 1.0;
        }

        private void ShowParserSurface()
        {
            ParserSurface.Visibility = Visibility.Visible;
            ParserContentGrid.Visibility = Visibility.Visible;
        }

        private void HideParserSurface()
        {
            ParserSurface.Visibility = Visibility.Collapsed;
            ParserContentGrid.Visibility = Visibility.Collapsed;
        }

        private void OnParserSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, sender)) return;
            ScheduleParserSelectorUpdate();
        }

        private void ScheduleParserSelectorUpdate()
        {
            if (_disposed || _parserSelectorUpdateScheduled) return;

            _parserSelectorUpdateScheduled = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _parserSelectorUpdateScheduled = false;
                if (!_disposed) UpdateParserSelector();
            }), DispatcherPriority.Loaded);
        }

        public void RefreshSelector()
        {
            if (!_disposed && !_isClosing && Visibility == Visibility.Visible)
                UpdateParserSelector();
        }

        private void UpdateParserSelector()
        {
            if (_selectorCoordinator == null)
                return;

            if (Visibility != Visibility.Visible || _isClosing)
            {
                _selectorCoordinator.Hide(SelectorCoordinator.Owner.Parser);
                return;
            }

            ListBox activeListBox = null;

            if (_mode == ParserMode.Games && GamesListBox.Visibility == Visibility.Visible)
                activeListBox = GamesListBox;
            else if (_mode == ParserMode.Covers && CoversListBox.Visibility == Visibility.Visible)
                activeListBox = CoversListBox;
            else if (_mode == ParserMode.Backgrounds && BackgroundsListBox.Visibility == Visibility.Visible)
                activeListBox = BackgroundsListBox;

            if (activeListBox == null || activeListBox.SelectedIndex < 0)
            {
                _selectorCoordinator.ShowOverlay(SelectorCoordinator.Owner.Parser, null, null);
                return;
            }

            var selectedItem = activeListBox.ItemContainerGenerator.ContainerFromIndex(activeListBox.SelectedIndex) as ListBoxItem;

            if (selectedItem == null)
            {
                _selectorCoordinator.ShowOverlay(SelectorCoordinator.Owner.Parser, null, null);
                return;
            }

            bool isMedia = _mode == ParserMode.Covers ||
                _mode == ParserMode.Backgrounds;

            FrameworkElement target = isMedia
                ? (FrameworkElement)FindVisualChild<Rectangle>(selectedItem)
                : selectedItem;

            Selector.SelectorProfile profile = isMedia
                ? _selectorCoordinator.CreateMediaProfile()
                : _selectorCoordinator.CreateOverlayItemProfile();

            if (target == null)
            {
                _selectorCoordinator.ShowOverlay(SelectorCoordinator.Owner.Parser, null, null);
                return;
            }

            _selectorCoordinator.ShowOverlay(SelectorCoordinator.Owner.Parser, target, profile, 
                _mode == ParserMode.Games ? (FrameworkElement)activeListBox : ParserContentGrid);
        }

        private static T FindVisualChild<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T result)
                    return result;

                T nestedResult = FindVisualChild<T>(child);

                if (nestedResult != null)
                    return nestedResult;
            }

            return null;
        }

        private void UpdateAssetViewportHeight(ListBox listBox, int visibleRows, string mediaHeightKey)
        {
            double itemHeight = Math.Ceiling((double)FindResource(mediaHeightKey));

            Thickness gap = (Thickness)FindResource(UiScaleKeys.ParserAssetsGap);
            double verticalGap = Math.Ceiling(gap.Top + gap.Bottom);

            double contentHeight = Math.Ceiling(
                itemHeight * visibleRows +
                verticalGap * Math.Max(0, visibleRows - 1));

            double outerMargin = listBox.Margin.Top + listBox.Margin.Bottom;
            double targetHeight = Math.Ceiling(contentHeight + outerMargin);

            if (double.IsNaN(ParserContentGrid.Height) ||
                Math.Abs(ParserContentGrid.Height - targetHeight) > 0.01)
            {
                ParserContentGrid.Height = targetHeight;
            }
        }

        public async void Open()
        {
            if (_disposed || Visibility == Visibility.Visible) return;

            _mainWindow.GetSound()?.PlayLaunchSound();

            var launcher = _mainWindow.GetLauncher();

            if (launcher == null || launcher.SelectedIndex < 0 || launcher.SelectedIndex >= launcher.Shortcuts.Length)
                return;

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

            _selectorCoordinator.ShowOverlay(SelectorCoordinator.Owner.Parser, null, null);
            Visibility = Visibility.Visible;

            ScheduleParserSelectorUpdate();

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(FadeDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            BeginAnimation(OpacityProperty, fadeIn);

            ConfigureApiAuthorization();

            await SearchCurrentGameAsync(null, cancellationToken);
        }

        public void Close()
        {
            if (Visibility != Visibility.Visible || _isClosing) return;

            _isClosing = true;
            _mainWindow.GetSound()?.PlayBackSound();
            CancelSession();
            CancelAssetLoading();
            HideStatus();
            _selectorCoordinator.Hide(SelectorCoordinator.Owner.Parser);

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

                _selectorCoordinator.ShowMain();

                _inputHandler.SetMode(InputHandler.InputMode.Main);
                _mainWindow.SetHintsMode(HintPanel.HintMode.Main);
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }

        public void Back()
        {
            if (_mode == ParserMode.Covers || _mode == ParserMode.Backgrounds)
            {
                _mainWindow.GetSound()?.PlayBackSound();
                CancelAssetLoading();

                _mode = ParserMode.Games;
                ParserContentGrid.Height = double.NaN;

                AssetTabsPanel.Visibility = Visibility.Collapsed;
                CoversListBox.Visibility = Visibility.Collapsed;
                BackgroundsListBox.Visibility = Visibility.Collapsed;
                GamesListBox.Visibility = Visibility.Visible;

                _mainWindow.SetHintsMode(HintPanel.HintMode.ParserGames);

                HideStatus();

                if (_gameResults.Count > 0 && GamesListBox.SelectedIndex < 0)
                    GamesListBox.SelectedIndex = 0;

                GamesListBox.Focus();

                ScheduleParserSelectorUpdate();

                return;
            }

            Close();
        }

        public void NavigateHorizontal(int direction)
        {
            if (_isBusy || _mode == ParserMode.Games) return;

            ListBox listBox = _mode == ParserMode.Covers ? CoversListBox : BackgroundsListBox;
            int columns = _mode == ParserMode.Covers ? GridColumns : BackgroundColumns;

            if (listBox.Items.Count == 0) return;

            int index = Math.Max(0, listBox.SelectedIndex);
            int column = index % columns;

            if (direction < 0 && column == 0) return;
            if (direction > 0 && column == columns - 1) return;

            MoveSelection(listBox, index + Math.Sign(direction));
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

            ListBox listBox = _mode == ParserMode.Covers ? CoversListBox : BackgroundsListBox;
            int columns = _mode == ParserMode.Covers ? GridColumns : BackgroundColumns;

            if (listBox.Items.Count == 0) return;

            int indexAsset = Math.Max(0, listBox.SelectedIndex);
            MoveSelection(listBox, indexAsset + Math.Sign(direction) * columns);
        }

        private async Task SetActiveParserAssetTab(ParserMode mode)
        {
            _mode = mode;
            UpdateAssetTabs();

            if (mode == ParserMode.Covers)
            {
                ShowCoversTab();
                return;
            }

            if (mode == ParserMode.Backgrounds)
            {
                if (!_backgroundsLoaded)
                    await LoadBackgroundsAsync(_assetGame, _sessionCts.Token);
                else
                    ShowBackgroundsTab();
            }
        }

        public async void SwitchAssetTab(int direction)
        {
            if (_isBusy || _mode == ParserMode.Games || _assetGame == null || _sessionCts == null)
                return;

            if (direction < 0)
            {
                if (_mode == ParserMode.Covers)
                    return;

                _mainWindow.GetSound()?.PlayTabSound();
                await SetActiveParserAssetTab(ParserMode.Covers);
                return;
            }

            if (direction > 0)
            {
                if (_mode == ParserMode.Backgrounds)
                    return;

                _mainWindow.GetSound()?.PlayTabSound();
                await SetActiveParserAssetTab(ParserMode.Backgrounds);
            }
        }

        private void UpdateAssetTabs()
        {
            AssetTabsPanel.Visibility = _mode == ParserMode.Games ? Visibility.Collapsed : Visibility.Visible;

            ParserCoversTabText.Opacity = _mode == ParserMode.Covers ? 1.0 : 0.5;
            ParserBackgroundsTabText.Opacity = _mode == ParserMode.Backgrounds ? 1.0 : 0.5;
        }

        private void ShowCoversTab()
        {
            BackgroundsListBox.Visibility = Visibility.Collapsed;
            UpdateAssetViewportHeight(CoversListBox, CoversVisibleRows, UiScaleKeys.ParserCoverSize);

            if (_gridResults.Count == 0)
            {
                CoversListBox.Visibility = Visibility.Collapsed;
                ShowStatus(Locals.GetString("NoResultsFound"));
                ScheduleParserSelectorUpdate();
                return;
            }

            HideStatus();

            CoversListBox.Visibility = Visibility.Visible;

            if (CoversListBox.SelectedIndex < 0)
                CoversListBox.SelectedIndex = 0;

            CoversListBox.ScrollIntoView(CoversListBox.SelectedItem);
            CoversListBox.Focus();
            ScheduleParserSelectorUpdate();
        }

        private void ShowBackgroundsTab()
        {
            CoversListBox.Visibility = Visibility.Collapsed;
            UpdateAssetViewportHeight(BackgroundsListBox, BackgroundsVisibleRows, UiScaleKeys.ParserBackgroundHeight);

            if (_heroResults.Count == 0)
            {
                BackgroundsListBox.Visibility = Visibility.Collapsed;
                ShowStatus(Locals.GetString("NoResultsFound"));
                ScheduleParserSelectorUpdate();
                return;
            }

            HideStatus();

            BackgroundsListBox.Visibility = Visibility.Visible;

            if (BackgroundsListBox.SelectedIndex < 0)
                BackgroundsListBox.SelectedIndex = 0;

            BackgroundsListBox.ScrollIntoView(BackgroundsListBox.SelectedItem);
            BackgroundsListBox.Focus();
            ScheduleParserSelectorUpdate();
        }

        public void Search()
        {
            if (_isBusy) return;
            if (_mode != ParserMode.Games) return;
            if (GamesListBox.Visibility != Visibility.Visible) return;
            if (_sessionCts == null) return;

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

            if (_mode == ParserMode.Backgrounds)
            {
                var background = BackgroundsListBox.SelectedItem as ParserGridResult;

                if (background != null)
                {
                    _mainWindow.GetSound()?.PlayLaunchSound();
                    await DownloadBackgroundAsync(background, _sessionCts.Token);
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
            UpdateParserSelector();
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

            _selectorCoordinator.ShowOverlay(SelectorCoordinator.Owner.Parser, null, null);

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

            ShowParserSurface();
            ManualSearchPanel.Visibility = Visibility.Visible;
            ParserSurface.RenderTransform = null;

            // Manual search uses the settings-style hint layout.
            _mainWindow.SetHintsMode(HintPanel.HintMode.Settings);

            // Switch input language to English before focusing the search field.
            CaptureInputLanguage();

            SearchInputBox.UpdateLayout();

            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

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
            ScheduleParserSelectorUpdate();
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

                List<ParserGameResult> results;

                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(GameSearchTimeoutSeconds));

                    try
                    {
                        results = await _steamGridDbClient.SearchGamesAsync(
                            query, MaxGames, timeoutCts.Token);
                    }
                    catch (OperationCanceledException) when (
                        !cancellationToken.IsCancellationRequested &&
                        timeoutCts.IsCancellationRequested)
                    {
                        if (IsSessionActive(cancellationToken))
                            ShowErrorNotification(Locals.GetString("GridDBError"));

                        return;
                    }
                }

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

                ShowParserSurface();
                ManualSearchPanel.Visibility = Visibility.Collapsed;
                GamesListBox.Visibility = Visibility.Visible;
                _mainWindow.SetHintsMode(HintPanel.HintMode.ParserGames);
                GamesListBox.SelectedIndex = 0;
                GamesListBox.ScrollIntoView(GamesListBox.SelectedItem);
                GamesListBox.Focus();

                ScheduleParserSelectorUpdate();
            }

            catch (OperationCanceledException)
            {
                // Пользователь закрыл парсер или отменил операцию.
            }

            catch (SteamGridDbApiKeyMissingException ex)
            {
                DebugLog.Error("Parser", ex, "SteamGridDB API key is not configured.");

                if (IsSessionActive(cancellationToken))
                    ShowErrorNotification(Locals.GetString("GridDbApiMiss"));
            }

            catch (SteamGridDbAuthException ex)
            {
                DebugLog.Error("Parser", ex, "SteamGridDB authorization failed.");

                if (IsSessionActive(cancellationToken))
                    ShowErrorNotification(Locals.GetString("GridDbInvalidApi"));
            }

            catch (Exception ex)
            {
                DebugLog.Error("Parser", ex, "SteamGridDB game search failed.");

                if (IsSessionActive(cancellationToken))
                    ShowErrorNotification(Locals.GetString("GridDBError"));
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
            HideParserSurface();

            ShowStatus(Locals.GetString("GridDBSearch"));

            if (_sessionCts == null) return;

            _mainWindow.GetSound()?.PlayLaunchSound();
            await SearchCurrentGameAsync(query, _sessionCts.Token);
        }

        private async Task LoadCoversAsync(ParserGameResult game, CancellationToken sessionToken)
        {
            if (!IsSessionActive(sessionToken)) return;

            CancelAssetLoading();

            var cts = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
            _assetLoadCts = cts;

            var cancellationToken = cts.Token;

            _isBusy = true;
            _mode = ParserMode.Covers;

            UpdateAssetViewportHeight(CoversListBox, CoversVisibleRows, UiScaleKeys.ParserCoverSize);

            _assetGame = game;
            _backgroundsLoaded = false;
            _heroResults.Clear();

            AssetTabsPanel.Visibility = Visibility.Visible;
            BackgroundsListBox.Visibility = Visibility.Collapsed;
            UpdateAssetTabs();

            _mainWindow.SetHintsMode(HintPanel.HintMode.ParserAssets);

            GamesListBox.Visibility = Visibility.Collapsed;
            CoversListBox.Visibility = Visibility.Collapsed;
            CoversListBox.Opacity = 0.45;
            ScheduleParserSelectorUpdate();

            AssetsProgressBar.Value = 0;
            AssetsProgressBar.IsIndeterminate = true;
            AssetsProgressBar.Visibility = Visibility.Visible;

            try
            {
                var results = await _steamGridDbClient.GetSquareGridsAsync(game.Id, MaxCovers, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(sessionToken)) return;

                _gridResults.Clear();

                foreach (var result in results)
                    _gridResults.Add(result);

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

                UpdateAssetViewportHeight(CoversListBox, CoversVisibleRows, UiScaleKeys.ParserCoverSize);

                CoversListBox.SelectedIndex = 0;
                CoversListBox.ScrollIntoView(CoversListBox.SelectedItem);
                CoversListBox.Focus();

                ScheduleParserSelectorUpdate();

                AssetsProgressBar.IsIndeterminate = false;
                AssetsProgressBar.Minimum = 0;
                AssetsProgressBar.Maximum = _gridResults.Count;
                AssetsProgressBar.Value = 0;

                int loadedCount = 0;
                int totalThumbnails = _gridResults.Count;

                using (var thumbnailSemaphore = new SemaphoreSlim(6, 6))
                {
                    var tasks = _gridResults.Select(async grid =>
                    {
                        await thumbnailSemaphore.WaitAsync(cancellationToken);

                        try
                        {
                            await LoadThumbnailAsync(grid, cancellationToken, 512, 512);
                            cancellationToken.ThrowIfCancellationRequested();

                            int completed = Interlocked.Increment(ref loadedCount);

                            if (completed % 4 == 0 || completed == totalThumbnails)
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

                if (!IsSessionActive(sessionToken)) return;

                CoversListBox.Opacity = 1.0;
                AssetsProgressBar.Visibility = Visibility.Collapsed;
            }

            catch (OperationCanceledException)
            {
                // Expected when the user goes back or closes the parser.
            }

            catch (SteamGridDbApiKeyMissingException ex)
            {
                DebugLog.Error("Parser", ex, "SteamGridDB API key is not configured.");

                if (IsSessionActive(sessionToken) &&
                    ReferenceEquals(_assetLoadCts, cts) &&
                    !cancellationToken.IsCancellationRequested)
                {
                    ShowErrorNotification(Locals.GetString("GridDbApiMiss"));
                }
            }

            catch (SteamGridDbAuthException ex)
            {
                DebugLog.Error("Parser", ex, "SteamGridDB authorization failed while loading covers.");

                if (IsSessionActive(sessionToken))
                {
                    CoversListBox.Opacity = 1.0;
                    AssetsProgressBar.IsIndeterminate = false;
                    AssetsProgressBar.Visibility = Visibility.Collapsed;

                    HideParserSurface();
                    ShowErrorNotification(Locals.GetString("GridDbInvalidApi"));
                }
            }

            catch (Exception ex)
            {
                DebugLog.Error("Parser", ex, "SteamGridDB cover search failed.");

                if (!IsSessionActive(sessionToken)) return;

                CoversListBox.Opacity = 1.0;
                AssetsProgressBar.IsIndeterminate = false;
                AssetsProgressBar.Visibility = Visibility.Collapsed;

                HideParserSurface();
                ShowErrorNotification(Locals.GetString("GridDBError"));
            }

            finally
            {
                bool isCurrentLoad = ReferenceEquals(_assetLoadCts, cts);

                if (isCurrentLoad)
                    _assetLoadCts = null;

                cts.Dispose();

                if (isCurrentLoad && IsCurrentSession(sessionToken))
                    _isBusy = false;
            }
        }

        private async Task LoadBackgroundsAsync(ParserGameResult game, CancellationToken sessionToken)
        {
            if (!IsSessionActive(sessionToken)) return;

            CancelAssetLoading();

            var cts = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
            _assetLoadCts = cts;

            var cancellationToken = cts.Token;

            _isBusy = true;
            _mode = ParserMode.Backgrounds;

            UpdateAssetViewportHeight(BackgroundsListBox, BackgroundsVisibleRows, UiScaleKeys.ParserBackgroundHeight);

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
                var results = await _steamGridDbClient.GetHeroesAsync(game.Id, MaxBackgrounds, 3840, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(sessionToken)) return;

                _heroResults.Clear();

                foreach (var result in results)
                    _heroResults.Add(result);

                _backgroundsLoaded = true;

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

                UpdateAssetViewportHeight(BackgroundsListBox, BackgroundsVisibleRows, UiScaleKeys.ParserBackgroundHeight);

                BackgroundsListBox.SelectedIndex = 0;
                BackgroundsListBox.ScrollIntoView(BackgroundsListBox.SelectedItem);
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
                        await thumbnailSemaphore.WaitAsync(cancellationToken);

                        try
                        {
                            await LoadThumbnailAsync(hero, cancellationToken, 768);
                            cancellationToken.ThrowIfCancellationRequested();

                            int completed = Interlocked.Increment(ref loadedCount);

                            if (completed % 4 == 0 || completed == totalThumbnails)
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

                if (!IsSessionActive(sessionToken)) return;

                BackgroundsListBox.Opacity = 1.0;
                AssetsProgressBar.Visibility = Visibility.Collapsed;
            }

            catch (OperationCanceledException)
            {
                // Expected when the user goes back or closes the parser.
            }

            catch (SteamGridDbApiKeyMissingException ex)
            {
                DebugLog.Error("Parser", ex, "SteamGridDB API key is not configured.");

                if (IsSessionActive(sessionToken) &&
                    ReferenceEquals(_assetLoadCts, cts) &&
                    !cancellationToken.IsCancellationRequested)
                {
                    ShowErrorNotification(Locals.GetString("GridDbApiMiss"));
                }
            }

            catch (SteamGridDbAuthException ex)
            {
                DebugLog.Error("Parser", ex, "SteamGridDB authorization failed while loading backgrounds.");

                if (IsSessionActive(sessionToken))
                {
                    BackgroundsListBox.Opacity = 1.0;
                    AssetsProgressBar.IsIndeterminate = false;
                    AssetsProgressBar.Visibility = Visibility.Collapsed;

                    HideParserSurface();
                    ShowErrorNotification(Locals.GetString("GridDbInvalidApi"));
                }
            }

            catch (Exception ex)
            {
                DebugLog.Error("Parser", ex, "SteamGridDB background search failed.");

                if (!IsSessionActive(sessionToken)) return;

                BackgroundsListBox.Opacity = 1.0;
                AssetsProgressBar.IsIndeterminate = false;
                AssetsProgressBar.Visibility = Visibility.Collapsed;

                HideParserSurface();
                ShowErrorNotification(Locals.GetString("GridDBError"));
            }

            finally
            {
                bool isCurrentLoad = ReferenceEquals(_assetLoadCts, cts);

                if (isCurrentLoad)
                    _assetLoadCts = null;

                cts.Dispose();

                if (isCurrentLoad && IsCurrentSession(sessionToken))
                    _isBusy = false;
            }
        }

        private async Task LoadThumbnailAsync(ParserGridResult result, CancellationToken cancellationToken,
            int decodePixelWidth, int decodePixelHeight = 0)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte[] bytes = await _steamGridDbClient.DownloadImageAsync(
                    result.Thumb, cancellationToken, MaxThumbnailDownloadBytes);

                cancellationToken.ThrowIfCancellationRequested();

                BitmapImage bitmap = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ValidateSourceImageHeader(bytes);

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

                // Возвращаемся на UI-поток для обновления привязки.
                result.ImageSource = bitmap;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Ошибка одной миниатюры не отменяет остальные.
            }
        }

        private static void ValidateSourceImageHeader(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 24)
                throw new InvalidDataException("Invalid image header.");

            long width = 0;
            long height = 0;

            // PNG: signature + IHDR
            if (bytes[0] == 137 && bytes[1] == 80 &&
                bytes[2] == 78 && bytes[3] == 71 &&
                bytes[4] == 13 && bytes[5] == 10 &&
                bytes[6] == 26 && bytes[7] == 10)
            {
                if (bytes[8] != 0 || bytes[9] != 0 ||
                    bytes[10] != 0 || bytes[11] != 13 ||
                    bytes[12] != 73 || bytes[13] != 72 ||
                    bytes[14] != 68 || bytes[15] != 82)
                {
                    throw new InvalidDataException("Invalid PNG header.");
                }

                width = ((long)bytes[16] << 24) |
                        ((long)bytes[17] << 16) |
                        ((long)bytes[18] << 8) |
                        bytes[19];

                height = ((long)bytes[20] << 24) |
                        ((long)bytes[21] << 16) |
                        ((long)bytes[22] << 8) |
                        bytes[23];
            }
            // JPEG: find Start Of Frame marker
            else if (bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                int offset = 2;
                bool found = false;

                while (offset < bytes.Length)
                {
                    if (bytes[offset++] != 0xFF)
                        break;

                    while (offset < bytes.Length && bytes[offset] == 0xFF)
                        offset++;

                    if (offset >= bytes.Length)
                        break;

                    int marker = bytes[offset++];

                    if (marker == 0xD9 || marker == 0xDA)
                        break;

                    if (marker == 0x01 || marker == 0xD8 ||
                        (marker >= 0xD0 && marker <= 0xD7))
                        continue;

                    if (offset > bytes.Length - 2)
                        break;

                    int length = (bytes[offset] << 8) | bytes[offset + 1];

                    if (length < 2 || length > bytes.Length - offset)
                        break;

                    bool isSof = marker >= 0xC0 && marker <= 0xCF &&
                                marker != 0xC4 && marker != 0xC8 &&
                                marker != 0xCC;

                    if (isSof)
                    {
                        if (length < 7)
                            break;

                        height = (bytes[offset + 3] << 8) | bytes[offset + 4];
                        width = (bytes[offset + 5] << 8) | bytes[offset + 6];

                        found = true;
                        break;
                    }

                    offset += length;
                }

                if (!found)
                    throw new InvalidDataException("Invalid JPEG header.");
            }
            else
            {
                throw new InvalidDataException("Unsupported image format.");
            }

            if (width <= 0 || height <= 0 ||
                width > MaxSourceImageDimension ||
                height > MaxSourceImageDimension ||
                width * height > MaxSourceImagePixels)
            {
                throw new InvalidDataException(
                    $"Image dimensions exceed supported limits: {width}x{height}.");
            }
        }

        private static void ValidateSourceImageDimensions(Drawing.Image image)
        {
            int width = image.Width;
            int height = image.Height;

            if (width <= 0 || height <= 0 ||
                width > MaxSourceImageDimension ||
                height > MaxSourceImageDimension ||
                (long)width * height > MaxSourceImagePixels)
            {
                throw new InvalidDataException(
                    $"Image dimensions exceed supported limits: {width}x{height}.");
            }
        }

        private async Task DownloadCoverAsync(ParserGridResult cover, CancellationToken cancellationToken)
        {
            _isBusy = true;
            string coverTempPath = null;

            AssetsProgressBar.Value = 0;
            AssetsProgressBar.IsIndeterminate = true;
            AssetsProgressBar.Visibility = Visibility.Visible;

            try
            {
                byte[] bytes = await _steamGridDbClient.DownloadImageAsync(cover.Url, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(cancellationToken)) return;

                string shortcutName = _shortcut.Name;

                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ValidateSourceImageHeader(bytes);

                    string coversDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shortcuts", "covers");

                Directory.CreateDirectory(coversDirectory);

                // Use the actual shortcut name instead of DisplayName.
                string coverPath = Path.Combine(coversDirectory, shortcutName + ".png");
                coverTempPath = Path.Combine(coversDirectory, shortcutName + "." + Guid.NewGuid().ToString("N") + ".tmp");

                using (var input = new MemoryStream(bytes))
                using (var sourceImage = Drawing.Image.FromStream(input, true, true))
                using (var resizedImage = new Drawing.Bitmap(512, 512, DrawingImaging.PixelFormat.Format32bppArgb))
                {
                    ValidateSourceImageDimensions(sourceImage);

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

                        graphics.DrawImage(sourceImage, destRect, sourceRect, Drawing.GraphicsUnit.Pixel);
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    using (var output = new FileStream(coverTempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        resizedImage.Save(output, DrawingImaging.ImageFormat.Png);
                    }

                    using (var verifyStream = new FileStream(coverTempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var verifyImage = Drawing.Image.FromStream(verifyStream, true, true))
                    {
                        if (verifyImage.Width != 512 || verifyImage.Height != 512)
                            throw new InvalidDataException("Saved cover has invalid dimensions.");
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    if (File.Exists(coverPath))
                        File.Replace(coverTempPath, coverPath, null);
                    else
                        File.Move(coverTempPath, coverPath);

                    coverTempPath = null;
                    }
                    }, cancellationToken);

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
                    ShowStatus(Locals.GetString("ParserOperationError") + ": " + ex.Message);
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

        private async Task DownloadBackgroundAsync(ParserGridResult background, CancellationToken cancellationToken)
        {
            _isBusy = true;

            AssetsProgressBar.Value = 0;
            AssetsProgressBar.IsIndeterminate = true;
            AssetsProgressBar.Visibility = Visibility.Visible;

            string tempPath = null;

            try
            {
                byte[] bytes = await _steamGridDbClient.DownloadImageAsync(background.Url, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSessionActive(cancellationToken)) return;

                string shortcutName = _shortcut.Name;

                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ValidateSourceImageHeader(bytes);

                    string extension;

                using (var input = new MemoryStream(bytes))
                using (var sourceImage = Drawing.Image.FromStream(input, true, true))
                {
                    ValidateSourceImageDimensions(sourceImage);

                    if (sourceImage.Width < 3840)
                        throw new InvalidDataException($"Background width is only {sourceImage.Width}px. Minimum required width is 3840px.");

                    extension = sourceImage.RawFormat.Guid == DrawingImaging.ImageFormat.Png.Guid ? ".png" : ".jpg";
                }

                string backgroundsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shortcuts", "backgrounds");

                Directory.CreateDirectory(backgroundsDirectory);

                string backgroundPath = Path.Combine(backgroundsDirectory, shortcutName + extension);
                tempPath = Path.Combine(backgroundsDirectory, shortcutName + "." + Guid.NewGuid().ToString("N") + ".tmp");

                using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    output.Write(bytes, 0, bytes.Length);
                }

                using (var verifyStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))

                using (var verifyImage = Drawing.Image.FromStream(verifyStream, true, true))
                {
                    ValidateSourceImageDimensions(verifyImage);

                    if (verifyImage.Width < 3840)
                        throw new InvalidDataException("Saved background has invalid dimensions.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(backgroundPath))
                    File.Replace(tempPath, backgroundPath, null);
                else
                    File.Move(tempPath, backgroundPath);

                tempPath = null;

                string[] extensions = { ".png", ".jpg", ".jpeg" };

                foreach (string oldExtension in extensions)
                {
                    string oldPath = Path.Combine(backgroundsDirectory, shortcutName + oldExtension);

                    if (!string.Equals(oldPath, backgroundPath, StringComparison.OrdinalIgnoreCase) && File.Exists(oldPath))
                    {
                        try
                        {
                            File.Delete(oldPath);
                        }
                        catch (Exception ex)
                        {
                            DebugLog.Error("Parser", ex, $"Failed to remove old background '{oldPath}'.");
                        }
                    }
                }
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

                                if (!IsSessionActive(cancellationToken)) return;

                                _mainWindow.InvalidateBackgroundCache(_shortcut.FullPath);
                _mainWindow.RefreshSelectedBackground();

                Close();
            }

            catch (OperationCanceledException)
            {
                // Expected when the parser is closed during download.
            }

            catch (Exception ex)
            {
                DebugLog.Error("Parser", ex, "Failed to download or save background.");

                if (IsSessionActive(cancellationToken))
                {
                    BackgroundsListBox.Visibility = Visibility.Visible;
                    ShowStatus(Locals.GetString("ParserOperationError") + ": " + ex.Message);
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

        private void ShowStatus(string text)
        {
            _statusTimer.Stop();

            StatusText.Text = text;
            ParserStatusSurface.Visibility = Visibility.Visible;
            StatusText.Visibility = Visibility.Visible;

            _statusTimer.Start();
        }

        private void HideStatus()
        {
            _statusTimer.Stop();

            StatusText.Visibility = Visibility.Collapsed;
            ParserStatusSurface.Visibility = Visibility.Collapsed;
        }

        private void ShowErrorNotification(string text)
        {
            _mainWindow.ShowParserErrorNotification(text);
            Close();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            GamesListBox.SelectionChanged -= OnParserSelectionChanged;
            CoversListBox.SelectionChanged -= OnParserSelectionChanged;
            BackgroundsListBox.SelectionChanged -= OnParserSelectionChanged;

            CancelSession();
            CancelAssetLoading();
            _statusTimer.Stop();

            if (_manualSearchInputView != null)
            {
                _manualSearchInputView.PrimaryViewHiding -= ManualSearchInputView_Hiding;
                _manualSearchInputView.OcclusionsChanged -= ManualSearchInputView_OcclusionsChanged;
                _manualSearchInputView = null;
            }

            RestoreInputLanguage();

            _steamGridDbClient.Dispose();

            _sessionCts?.Dispose();
            _sessionCts = null;
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
