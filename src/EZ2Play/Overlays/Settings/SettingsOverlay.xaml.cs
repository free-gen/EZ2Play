
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EZ2Play.App
{
    /// <summary>
    /// Provides the application settings interface,
    /// including configuration changes and exit confirmation.
    /// </summary>
    /// <remarks>
    /// This file owns initialization, overlay visibility,
    /// configuration operations, and application commands.
    ///
    /// SettingsOverlay.Navigation.cs handles selection,
    /// navigation, and exit confirmation UI state.
    ///
    /// SelectorCoordinator manages selection highlighting.
    ///
    /// SystemProvider provides access to Windows operations
    /// through the HelperIntegration, FpsMonitorIntegration,
    /// and other platform components.
    ///
    /// Both partial files share the same state and XAML controls.
    /// </remarks>
    public partial class SettingsOverlay : UserControl
    {
        // Application services.
        private InputHandler _inputHandler;
        private MainWindow _mainWindow;
        private AppConfig _config;
        private SelectorCoordinator _selectorCoordinator;

        // Overlay animation duration in seconds.
        private double fadeDuration = 0.1;

        // Current overlay and navigation state.
        private bool _exitConfirmationMode = false;
        private bool _isClosing;
        private int _subOptionsSelectedIndex = 0;
        private bool _selectionHandlersAttached;

        // Prevent overlapping configuration operations.
        private bool _autorunActionPending;
        private bool _fpsMonitorActionPending;

        /// <summary>
        /// Initializes the settings overlay and connects
        /// application services and XAML controls.
        /// </summary>
        /// <remarks>
        /// Localization, available settings, toggle states,
        /// and selection initialization run during Loaded.
        ///
        /// Selection and autorun toggle handlers are registered
        /// only once, even if Loaded is raised again.
        ///
        /// The overlay starts hidden with zero opacity.
        /// </remarks>
        public SettingsOverlay(
            InputHandler inputHandler,
            MainWindow mainWindow,
            SelectorCoordinator selectorCoordinator)
        {
            InitializeComponent();

            _inputHandler = inputHandler;
            _mainWindow = mainWindow;
            _selectorCoordinator = selectorCoordinator;
            _config = _mainWindow.GetConfig();

            // Initialize localized content and selection state
            // when the overlay enters the visual tree.
            Loaded += (s, e) =>
            {
                Locals.ApplyLocalization(this);

                SetDescriptionWithIcon(
                    SettingsAutorunAppDesc,
                    "SettingsAutorunAppDesc",
                    "\uE390");

                RefreshDisplayList();

                if (!_autorunActionPending)
                    RefreshAutorunState();

                RefreshFpsMonitorVisibility();
                RefreshFpsMonitorState();

                if (SettingsListBox.Items.Count > 0)
                    SettingsListBox.SelectedIndex = 0;

                if (SubOptionsListBox.Items.Count > 0)
                    SubOptionsListBox.SelectedIndex = 0;

                if (ExitConfirmationListBox.Items.Count > 0)
                    ExitConfirmationListBox.SelectedIndex = 0;

                // Attach selection handlers only once.
                if (!_selectionHandlersAttached)
                {
                    SettingsListBox.SelectionChanged += OnSelectionChanged;
                    SubOptionsListBox.SelectionChanged += OnSelectionChanged;
                    ExitConfirmationListBox.SelectionChanged += OnSelectionChanged;

                    AutorunToggle.Checked +=
                        (sender, args) => ScheduleUpdateTreeHeaderDivider();

                    AutorunToggle.Unchecked +=
                        (sender, args) => ScheduleUpdateTreeHeaderDivider();

                    _selectionHandlersAttached = true;
                }

                UpdateOverlaySelector();

                UpdateSelectionState();
                ScheduleUpdateTreeHeaderDivider();
            };

            SettingsListBox.SelectedIndex = 0;
            SubOptionsListBox.SelectedIndex = 0;

            Opacity = 0;
            Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Schedules an update of the divider
        /// below the autorun settings item.
        /// </summary>
        private void ScheduleUpdateTreeHeaderDivider()
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(UpdateTreeHeaderDivider));
        }

        /// <summary>
        /// Updates the divider visibility according
        /// to the autorun toggle state.
        /// </summary>
        private void UpdateTreeHeaderDivider()
        {
            if (SettingsListBox.Items.Count == 0)
                return;

            var container =
                SettingsListBox.ItemContainerGenerator.ContainerFromIndex(0)
                    as ListBoxItem;

            if (container == null)
                return;

            var divider = FindVisualChild<Border>(
                container,
                "ItemDivider");

            if (divider != null)
            {
                divider.Visibility =
                    AutorunToggle.IsChecked.GetValueOrDefault(false)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
            }
        }

        /// <summary>
        /// Opens the settings overlay and refreshes its state.
        /// </summary>
        /// <remarks>
        /// Resets exit confirmation, activates Settings input
        /// mode, and transfers selector ownership.
        ///
        /// Refreshes display availability, autorun state,
        /// and FPS Monitor information.
        ///
        /// Selector positioning is deferred using
        /// DispatcherPriority.Loaded.
        ///
        /// Keyboard focus moves to the settings list
        /// after the fade-in animation completes.
        /// </remarks>
        public void Open()
        {
            if (Visibility == Visibility.Visible)
                return;

            _isClosing = false;

            _mainWindow.GetSound()?.PlayLaunchSound();

            HideExitDisplayConfirmation(false);

            _mainWindow.SetHintsMode(HintPanel.HintMode.Settings);

            _inputHandler.SetMode(InputHandler.InputMode.Settings);

            _selectorCoordinator.ShowOverlay(
                SelectorCoordinator.Owner.Settings,
                null,
                null);

            Visibility = Visibility.Visible;

            RefreshDisplayList();

            if (SettingsListBox.Items.Count > 0)
                SettingsListBox.SelectedIndex = 0;

            // Refresh selector after the layout is loaded.
            Dispatcher.BeginInvoke(
                new Action(UpdateOverlaySelector),
                DispatcherPriority.Loaded);

            if (!_autorunActionPending)
                RefreshAutorunState();

            RefreshFpsMonitorVisibility();
            RefreshFpsMonitorState();
            ScheduleUpdateTreeHeaderDivider();

            var fadeIn = OverlayAnimations.CreateFadeIn(fadeDuration);

            fadeIn.Completed += (s, e) => SettingsListBox.Focus();

            BeginAnimation(OpacityProperty, fadeIn);

            UpdateSelectionState();
        }

        /// <summary>
        /// Closes the settings overlay with a fade-out animation.
        /// </summary>
        /// <remarks>
        /// Prevents repeated closing and hides the settings
        /// selector before starting the animation.
        ///
        /// The completion callback hides the overlay, clears
        /// exit confirmation mode, and restores the main selector,
        /// input mode, and context hints.
        ///
        /// The callback order must remain unchanged.
        /// </remarks>
        public void Close()
        {
            if (Visibility != Visibility.Visible || _isClosing)
                return;

            _isClosing = true;

            _mainWindow.GetSound()?.PlayBackSound();

            _selectorCoordinator.Hide(
                SelectorCoordinator.Owner.Settings);

            var fadeOut = OverlayAnimations.CreateFadeOut(
                Opacity,
                fadeDuration);

            fadeOut.Completed += (s, e) =>
            {
                Visibility = Visibility.Collapsed;

                HideExitDisplayConfirmation(false);

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
        /// Cancels exit confirmation if active.
        /// Otherwise closes the settings overlay.
        /// </remarks>
        public void Back()
        {
            if (_exitConfirmationMode)
            {
                HideExitDisplayConfirmation();
                return;
            }

            Close();
        }

        /// <summary>
        /// Refreshes available display choices and
        /// visibility of the display switching setting.
        /// </summary>
        private void RefreshDisplayList()
        {
            var display = _mainWindow.GetDisplay();

            display.RefreshDisplayList();

            SettingsSourceDisplay.Visibility =
                !_mainWindow.IsHotSwapLaunch() &&
                display.HasMultipleDisplays
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        /// <summary>
        /// Shows the FPS Monitor setting only when
        /// the application is installed.
        /// </summary>
        private void RefreshFpsMonitorVisibility()
        {
            SettingsFpsMonitor.Visibility =
                SystemProvider.IsFpsMonitorInstalled()
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        /// <summary>
        /// Synchronizes the FPS Monitor toggle
        /// with the actual process state.
        /// </summary>
        private void RefreshFpsMonitorState()
        {
            FpsMonitorToggle.IsChecked =
                SystemProvider.IsFpsMonitorRunning();
        }

        /// <summary>
        /// Changes the FPS Monitor process state.
        /// </summary>
        /// <remarks>
        /// Startup uses the existing synchronous platform call,
        /// while stopping FPS Monitor runs inside Task.Run.
        ///
        /// The pending flag prevents overlapping operations.
        ///
        /// Failed operations restore the toggle from the
        /// actual process state. Exceptions are logged.
        /// </remarks>
        private async void SetFpsMonitorState(bool enabled)
        {
            if (_fpsMonitorActionPending)
                return;

            _fpsMonitorActionPending = true;

            try
            {
                bool success = enabled
                    ? SystemProvider.StartFpsMonitor()
                    : await System.Threading.Tasks.Task.Run(() =>
                        SystemProvider.StopFpsMonitor());

                if (success)
                    FpsMonitorToggle.IsChecked = enabled;
                else
                    RefreshFpsMonitorState();
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "FPS Monitor",
                    ex,
                    "Failed to change FPS Monitor state.");

                RefreshFpsMonitorState();
            }
            finally
            {
                _fpsMonitorActionPending = false;
            }
        }

        /// <summary>
        /// Synchronizes autorun settings with the
        /// Windows startup shortcut and Helper process.
        /// </summary>
        /// <remarks>
        /// If autorun is enabled but Helper is not running,
        /// attempts to start it using stored arguments.
        ///
        /// Updates the persisted configuration when
        /// the effective state differs from the saved value.
        /// </remarks>
        private void RefreshAutorunState()
        {
            bool autorunEnabled = SystemProvider.IsAutorunEnabled();
            bool helperRunning = SystemProvider.IsHelperProcessRunning();

            if (autorunEnabled && !helperRunning)
            {
                helperRunning = SystemProvider.StartHelperProcess(
                    SystemProvider.GetAutorunArguments());
            }

            bool effectiveState = autorunEnabled && helperRunning;
            bool configState = _config.AutorunEnabled;

            if (effectiveState != configState)
            {
                _config.AutorunEnabled = effectiveState;
                _config.Save();
            }

            AutorunToggle.IsChecked = effectiveState;

            UpdateTreeHeaderDivider();
            UpdateSubOptionsVisibility(effectiveState);
            LoadSubOptionsStates();
        }

        /// <summary>
        /// Shows or hides nested startup settings
        /// according to the autorun state.
        /// </summary>
        private void UpdateSubOptionsVisibility(bool enabled)
        {
            if (enabled)
            {
                TreeItemsContainer.Visibility = Visibility.Visible;
                NoSplash.Visibility = Visibility.Visible;
                NoMusic.Visibility = Visibility.Visible;
                HotSwap.Visibility = Visibility.Visible;
            }
            else
            {
                TreeItemsContainer.Visibility = Visibility.Collapsed;
                NoSplash.Visibility = Visibility.Collapsed;
                NoMusic.Visibility = Visibility.Collapsed;
                HotSwap.Visibility = Visibility.Collapsed;
            }

            UpdateSelectionState();
        }

        /// <summary>
        /// Executes the action associated with
        /// the currently selected settings item.
        /// </summary>
        /// <remarks>
        /// Handles autorun options, FPS Monitor, display
        /// switching, and application exit.
        ///
        /// Hidden settings cannot be confirmed.
        /// Autorun actions are ignored while another autorun
        /// operation is pending.
        ///
        /// When exiting from an external display, a separate
        /// confirmation list is used. The confirmation choices
        /// preserve their existing display switching behavior.
        ///
        /// Navigation and confirmation UI state are managed
        /// by SettingsOverlay.Navigation.cs.
        /// </remarks>
        public void Confirm()
        {
            // Handle the exit confirmation choices.
            if (_exitConfirmationMode)
            {
                if (ExitConfirmationListBox.SelectedItem == ConfirmYesItem)
                {
                    _mainWindow.GetDisplay()
                        .RunDisplaySwitch("/internal");

                    _mainWindow.ExitApplication();
                    Close();
                }
                else if (ExitConfirmationListBox.SelectedItem == ConfirmNoItem)
                {
                    _mainWindow.ExitApplication();
                    Close();
                }

                return;
            }

            if (_autorunActionPending &&
                (SettingsListBox.SelectedItem == TreeItemsContainer ||
                 SettingsListBox.SelectedItem == SettingsAutorunApp))
            {
                return;
            }

            // Ignore settings items hidden from the UI.
            if (SettingsListBox.SelectedItem is ListBoxItem selectedItem &&
                selectedItem.Visibility != Visibility.Visible)
            {
                return;
            }

            // Nested startup options.
            if (SettingsListBox.SelectedItem == TreeItemsContainer)
            {
                if (SubOptionsListBox.SelectedItem == NoSplash)
                {
                    bool newState =
                        !NoSplashToggle.IsChecked.GetValueOrDefault(false);

                    NoSplashToggle.IsChecked = newState;
                    UpdateAutorunArguments();
                }
                else if (SubOptionsListBox.SelectedItem == NoMusic)
                {
                    bool newState =
                        !NoMusicToggle.IsChecked.GetValueOrDefault(false);

                    NoMusicToggle.IsChecked = newState;
                    UpdateAutorunArguments();
                }
                else if (SubOptionsListBox.SelectedItem == HotSwap)
                {
                    bool newState =
                        !HotSwapToggle.IsChecked.GetValueOrDefault(false);

                    HotSwapToggle.IsChecked = newState;
                    UpdateAutorunArguments();
                }
            }
            // Main autorun toggle.
            else if (SettingsListBox.SelectedItem == SettingsAutorunApp)
            {
                bool newState =
                    !AutorunToggle.IsChecked.GetValueOrDefault(false);

                SetAutorunState(newState);
            }
            // FPS Monitor toggle.
            else if (SettingsListBox.SelectedItem == SettingsFpsMonitor)
            {
                bool newState =
                    !FpsMonitorToggle.IsChecked.GetValueOrDefault(false);

                SetFpsMonitorState(newState);
            }
            // Application exit.
            else if (SettingsListBox.SelectedItem == SettingsExitApp)
            {
                if (_mainWindow.GetDisplay().IsExternalDisplay)
                {
                    ShowExitDisplayConfirmation();
                    return;
                }

                _mainWindow.ExitApplication();
                Close();
            }
            // Switch the active display.
            else if (SettingsListBox.SelectedItem == SettingsSourceDisplay)
            {
                if (!_mainWindow.IsHotSwapLaunch())
                    _mainWindow.GetDisplay().SwitchDisplay(1);
            }
        }

        /// <summary>
        /// Enables or disables Windows startup for EZ2Play Helper.
        /// </summary>
        /// <remarks>
        /// Windows startup shortcut operations run inside Task.Run.
        /// The pending flag prevents overlapping autorun changes.
        ///
        /// A successful operation is verified against the actual
        /// autorun state before saving the application configuration.
        ///
        /// Failed operations or exceptions trigger a refresh
        /// from the current Windows startup and Helper state.
        /// </remarks>
        private async void SetAutorunState(bool enabled)
        {
            if (_autorunActionPending)
                return;

            _autorunActionPending = true;

            try
            {
                bool success =
                    await System.Threading.Tasks.Task.Run(() =>
                        enabled
                            ? SystemProvider.EnableAutorun()
                            : SystemProvider.DisableAutorun());

                if (!success ||
                    SystemProvider.IsAutorunEnabled() != enabled)
                {
                    RefreshAutorunState();
                    return;
                }

                UpdateSubOptionsVisibility(enabled);

                _config.AutorunEnabled = enabled;
                _config.Save();

                AutorunToggle.IsChecked = enabled;

                LoadSubOptionsStates();
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Autorun",
                    ex,
                    "Failed to change autorun state.");

                RefreshAutorunState();
            }
            finally
            {
                _autorunActionPending = false;
            }
        }

        /// <summary>
        /// Rebuilds the Helper startup arguments
        /// from the currently selected options.
        /// </summary>
        /// <remarks>
        /// The supported arguments are:
        /// --nosplash, --nomusic, and --hotswap.
        ///
        /// An unsuccessful update restores the toggle
        /// states from the existing shortcut arguments.
        /// </remarks>
        private async void UpdateAutorunArguments()
        {
            if (_autorunActionPending)
                return;

            _autorunActionPending = true;

            try
            {
                string args = " ";

                if (NoSplashToggle.IsChecked.GetValueOrDefault(false))
                    args += "--nosplash ";

                if (NoMusicToggle.IsChecked.GetValueOrDefault(false))
                    args += "--nomusic ";

                if (HotSwapToggle.IsChecked.GetValueOrDefault(false))
                    args += "--hotswap ";

                args = args.TrimEnd();

                bool success =
                    await System.Threading.Tasks.Task.Run(() =>
                        SystemProvider.SetAutorunArguments(args));

                if (!success)
                {
                    LoadSubOptionsStates();
                    return;
                }

                if (AutorunToggle.IsChecked.GetValueOrDefault(false))
                    UpdateSubOptionsVisibility(true);
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Autorun",
                    ex,
                    "Failed to update autorun arguments.");

                LoadSubOptionsStates();
            }
            finally
            {
                _autorunActionPending = false;
            }
        }

        /// <summary>
        /// Loads nested startup toggle states from
        /// the Helper shortcut arguments.
        /// </summary>
        private void LoadSubOptionsStates()
        {
            string args = SystemProvider.GetAutorunArguments();

            NoSplashToggle.IsChecked = args.Contains("--nosplash");
            NoMusicToggle.IsChecked = args.Contains("--nomusic");
            HotSwapToggle.IsChecked = args.Contains("--hotswap");
        }

        /// <summary>
        /// Inserts an icon into a localized description
        /// at the {ICON} placeholder.
        /// </summary>
        /// <remarks>
        /// Uses inline WPF elements to preserve the
        /// original text and icon layout.
        /// </remarks>
        private void SetDescriptionWithIcon(
            TextBlock tb,
            string key,
            string iconGlyph)
        {
            var text = Locals.GetString(key);

            var parts = text.Split(
                new[] { "{ICON}" },
                StringSplitOptions.None);

            tb.Inlines.Clear();

            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    var icon = new TextBlock
                    {
                        Text = iconGlyph,
                        FontFamily = (FontFamily)Application.Current.FindResource("IconFont"),
                        RenderTransform = new TranslateTransform(0, 4)
                    };

                    icon.SetResourceReference(
                        TextBlock.FontSizeProperty,
                        UiScaleKeys.SettingsOverlayDescFontSize);

                    tb.Inlines.Add(new InlineUIContainer(icon));
                }

                tb.Inlines.Add(new Run(parts[i]));
            }
        }

        /// <summary>
        /// Recursively finds an element in the WPF visual tree.
        /// </summary>
        /// <typeparam name="T">
        /// Required type of the visual child.
        /// </typeparam>
        /// <param name="parent">
        /// Root element for the recursive search.
        /// </param>
        /// <param name="childName">
        /// Optional element name. When empty, the first matching
        /// element of the requested type is returned.
        /// </param>
        /// <returns>
        /// Matching visual element, or null when not found.
        /// </returns>
        /// <remarks>
        /// Used to locate named elements inside WPF control
        /// templates, including the settings item divider.
        /// </remarks>
        private T FindVisualChild<T>(
            DependencyObject parent,
            string childName)
            where T : DependencyObject
        {
            if (parent == null)
                return null;

            int childrenCount =
                VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                {
                    if (string.IsNullOrEmpty(childName))
                        return typedChild;

                    if (child is FrameworkElement fe &&
                        fe.Name == childName)
                    {
                        return typedChild;
                    }
                }

                T result = FindVisualChild<T>(child, childName);

                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
