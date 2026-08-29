using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Documents;
using System.Windows.Threading;

namespace EZ2Play.App
{
    public partial class SettingsOverlay : UserControl
    {
        private InputHandler _inputHandler;
        private MainWindow _mainWindow;
        private AppConfig _config;
        private double fadeDuration = 0.1;
        private bool _exitConfirmationMode = false;
        private int _subOptionsSelectedIndex = 0;
        private bool _inputLocked = false;

        public SettingsOverlay(InputHandler inputHandler, MainWindow mainWindow)
        {
            InitializeComponent();

            _inputHandler = inputHandler;
            _mainWindow = mainWindow;
            _config = _mainWindow.GetConfig();

            Loaded += (s, e) =>
            {
                Locals.ApplyLocalization(this);
                SetDescriptionWithIcon(SettingsAutorunAppDesc, "SettingsAutorunAppDesc", "\uE3E3");

                RefreshDisplayList();
                RefreshAutorunState();
                RefreshFpsMonitorVisibility();
                RefreshFpsMonitorState();

                if (SettingsListBox.Items.Count > 0)
                    SettingsListBox.SelectedIndex = 0;

                if (SubOptionsListBox.Items.Count > 0)
                    SubOptionsListBox.SelectedIndex = 0;

                if (ExitConfirmationListBox.Items.Count > 0)
                    ExitConfirmationListBox.SelectedIndex = 0;

                SettingsListBox.SelectionChanged += OnSelectionChanged;
                SubOptionsListBox.SelectionChanged += OnSelectionChanged;
                ExitConfirmationListBox.SelectionChanged += OnSelectionChanged;

                AutorunToggle.Checked += (sender, args) => ScheduleUpdateTreeHeaderDivider();
                AutorunToggle.Unchecked += (sender, args) => ScheduleUpdateTreeHeaderDivider();

                UpdateSelectionState();
                ScheduleUpdateTreeHeaderDivider();
            };

            SettingsListBox.SelectedIndex = 0;
            SubOptionsListBox.SelectedIndex = 0;
            Opacity = 0;
            Visibility = Visibility.Collapsed;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectionState();
        }

        private void UpdateSelectionState()
        {
            bool isSubOptionsActive =
                !_exitConfirmationMode &&
                SettingsListBox.SelectedItem == TreeItemsContainer &&
                TreeItemsContainer.Visibility == Visibility.Visible;

            if (isSubOptionsActive)
            {
                if (SubOptionsListBox.SelectedIndex == -1 && SubOptionsListBox.Items.Count > 0)
                {
                    int index = Math.Max(0, Math.Min(_subOptionsSelectedIndex, SubOptionsListBox.Items.Count - 1));
                    SubOptionsListBox.SelectedIndex = index;
                }

                return;
            }

            if (SubOptionsListBox.SelectedIndex >= 0)
            {
                _subOptionsSelectedIndex = SubOptionsListBox.SelectedIndex;
                SubOptionsListBox.SelectedIndex = -1;
            }
        }

        private void ScheduleUpdateTreeHeaderDivider()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateTreeHeaderDivider));
        }

        private void UpdateTreeHeaderDivider()
        {
            if (SettingsListBox.Items.Count == 0) return;

            var container = SettingsListBox.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;

            if (container == null) return;

            var divider = FindVisualChild<Border>(container, "ItemDivider");

            if (divider != null)
            {
                divider.Visibility = AutorunToggle.IsChecked.GetValueOrDefault(false)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        public async void Open()
        {
            if (Visibility == Visibility.Visible) return;

            _inputLocked = true;
            _mainWindow.GetSound()?.PlayLaunchSound();
            HideExitDisplayConfirmation(false);

            _mainWindow.SetHintsMode(HintPanel.HintMode.Settings);

            _inputHandler.SetSettingsOpen(true);
            Visibility = Visibility.Visible;

            RefreshDisplayList();

            if (SettingsListBox.Items.Count > 0)
                SettingsListBox.SelectedIndex = 0;

            RefreshAutorunState();
            RefreshFpsMonitorVisibility();
            RefreshFpsMonitorState();
            ScheduleUpdateTreeHeaderDivider();

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(fadeDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            fadeIn.Completed += (s, e) => SettingsListBox.Focus();
            BeginAnimation(OpacityProperty, fadeIn);

            UpdateSelectionState();

            await System.Threading.Tasks.Task.Delay(500);
            _inputLocked = false;
        }

        public void Close()
        {
            if (Visibility != Visibility.Visible) return;

            _inputLocked = false;
            _mainWindow.GetSound()?.PlayBackSound();

            var fadeOut = new DoubleAnimation
            {
                From = Opacity,
                To = 0,
                Duration = TimeSpan.FromSeconds(fadeDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                HideExitDisplayConfirmation(false);

                Visibility = Visibility.Collapsed;
                _inputHandler.SetSettingsOpen(false);
                _mainWindow.SetHintsMode(HintPanel.HintMode.Main);
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }

        public void Back()
        {
            if (_inputLocked) return;

            if (_exitConfirmationMode)
            {
                HideExitDisplayConfirmation();
                return;
            }

            Close();
        }

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

        private void RefreshFpsMonitorVisibility()
        {
            SettingsFpsMonitor.Visibility = SystemProvider.IsFpsMonitorInstalled()
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void RefreshFpsMonitorState()
        {
            FpsMonitorToggle.IsChecked = SystemProvider.IsFpsMonitorRunning();
        }

        private void SetFpsMonitorState(bool enabled)
        {
            bool success = enabled
                ? SystemProvider.StartFpsMonitor()
                : SystemProvider.StopFpsMonitor();

            if (success)
                FpsMonitorToggle.IsChecked = enabled;
            else
                RefreshFpsMonitorState();
        }

        private void RefreshAutorunState()
        {
            bool autorunEnabled = SystemProvider.IsAutorunEnabled();
            bool helperRunning = SystemProvider.IsHelperProcessRunning();

            if (autorunEnabled && !helperRunning)
                helperRunning = SystemProvider.StartHelperProcess(SystemProvider.GetAutorunArguments());

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

            UpdateSelectionState();
        }

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

        public void Navigate(int direction, bool isHorizontal = true)
        {
            if (_inputLocked) return;
            
            if (_exitConfirmationMode)
            {
                if (isHorizontal) return;

                int newIndex = ExitConfirmationListBox.SelectedIndex + direction;

                if (newIndex >= 0 && newIndex < ExitConfirmationListBox.Items.Count)
                {
                    ExitConfirmationListBox.SelectedIndex = newIndex;
                    ExitConfirmationListBox.ScrollIntoView(ExitConfirmationListBox.SelectedItem);
                    _mainWindow.GetSound()?.PlayMoveSound();
                }

                return;
            }

            if (isHorizontal)
            {
                if (SettingsListBox.SelectedItem == TreeItemsContainer && TreeItemsContainer.Visibility == Visibility.Visible)
                {
                    int newIndex = SubOptionsListBox.SelectedIndex + direction;

                    while (newIndex >= 0 && newIndex < SubOptionsListBox.Items.Count)
                    {
                        var targetItem = SubOptionsListBox.Items[newIndex] as ListBoxItem;

                        if (targetItem != null && targetItem.Visibility == Visibility.Visible)
                        {
                            SubOptionsListBox.SelectedIndex = newIndex;
                            _mainWindow.GetSound()?.PlayMoveSound();
                            return;
                        }

                        newIndex += direction;
                    }
                }

                return;
            }

            int newIndexVert = SettingsListBox.SelectedIndex + direction;

            while (newIndexVert >= 0 && newIndexVert < SettingsListBox.Items.Count)
            {
                var item = SettingsListBox.Items[newIndexVert] as ListBoxItem;

                if (item != null && item.Visibility == Visibility.Visible)
                {
                    SettingsListBox.SelectedIndex = newIndexVert;
                    SettingsListBox.ScrollIntoView(SettingsListBox.SelectedItem);
                    _mainWindow.GetSound()?.PlayMoveSound();

                    if (item == TreeItemsContainer)
                    {
                        if (SubOptionsListBox.SelectedIndex == -1)
                        {
                            for (int i = 0; i < SubOptionsListBox.Items.Count; i++)
                            {
                                if ((SubOptionsListBox.Items[i] as ListBoxItem)?.Visibility == Visibility.Visible)
                                {
                                    SubOptionsListBox.SelectedIndex = i;
                                    break;
                                }
                            }
                        }
                    }

                    return;
                }

                newIndexVert += direction;
            }
        }

        public void Confirm()
        {
            if (_inputLocked) return;

            if (_exitConfirmationMode)
            {
                if (ExitConfirmationListBox.SelectedItem == ConfirmYesItem)
                {
                    _mainWindow.GetDisplay().RunDisplaySwitch("/internal");
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

            if (SettingsListBox.SelectedItem is ListBoxItem selectedItem &&
                selectedItem.Visibility != Visibility.Visible)
            {
                return;
            }

            if (SettingsListBox.SelectedItem == TreeItemsContainer)
            {
                if (SubOptionsListBox.SelectedItem == NoSplash)
                {
                    bool newState = !NoSplashToggle.IsChecked.GetValueOrDefault(false);

                    NoSplashToggle.IsChecked = newState;
                    UpdateAutorunArguments();
                }

                else if (SubOptionsListBox.SelectedItem == NoMusic)
                {
                    bool newState = !NoMusicToggle.IsChecked.GetValueOrDefault(false);

                    NoMusicToggle.IsChecked = newState;
                    UpdateAutorunArguments();
                }

                else if (SubOptionsListBox.SelectedItem == HotSwap)
                {
                    bool newState = !HotSwapToggle.IsChecked.GetValueOrDefault(false);

                    HotSwapToggle.IsChecked = newState;
                    UpdateAutorunArguments();
                }
            }

            else if (SettingsListBox.SelectedItem == SettingsAutorunApp)
            {
                bool newState = !AutorunToggle.IsChecked.GetValueOrDefault(false);
                SetAutorunState(newState);
            }

            else if (SettingsListBox.SelectedItem == SettingsFpsMonitor)
            {
                bool newState = !FpsMonitorToggle.IsChecked.GetValueOrDefault(false);
                SetFpsMonitorState(newState);
            }

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

            else if (SettingsListBox.SelectedItem == SettingsSourceDisplay)
            {
                if (!_mainWindow.IsHotSwapLaunch())
                    _mainWindow.GetDisplay().SwitchDisplay(1);
            }
        }

        private void SetAutorunState(bool enabled)
        {
            try
            {
                bool success = enabled
                    ? SystemProvider.EnableAutorun()
                    : SystemProvider.DisableAutorun();

                if (!success || SystemProvider.IsAutorunEnabled() != enabled)
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
                DebugLog.Error("Autorun", ex, "Failed to change autorun state.");
                RefreshAutorunState();
            }
        }

        private void UpdateAutorunArguments()
        {
            string args = " ";

            if (NoSplashToggle.IsChecked.GetValueOrDefault(false))
                args += "--nosplash ";

            if (NoMusicToggle.IsChecked.GetValueOrDefault(false))
                args += "--nomusic ";

            if (HotSwapToggle.IsChecked.GetValueOrDefault(false))
                args += "--hotswap ";

            args = args.TrimEnd();

            bool success = SystemProvider.SetAutorunArguments(args);

            if (!success)
            {
                // Restore the UI to the arguments actually stored in the shortcut.
                LoadSubOptionsStates();
                return;
            }

            if (AutorunToggle.IsChecked.GetValueOrDefault(false))
                UpdateSubOptionsVisibility(true);
        }

        private void LoadSubOptionsStates()
        {
            string args = SystemProvider.GetAutorunArguments();

            NoSplashToggle.IsChecked = args.Contains("--nosplash");
            NoMusicToggle.IsChecked = args.Contains("--nomusic");
            HotSwapToggle.IsChecked = args.Contains("--hotswap");
        }

        private void SetDescriptionWithIcon(TextBlock tb, string key, string iconGlyph)
        {
            var text = Locals.GetString(key);
            var parts = text.Split(new[] { "{ICON}" }, StringSplitOptions.None);

            tb.Inlines.Clear();

            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    var icon = new TextBlock
                    {
                        Text = iconGlyph,
                        FontFamily = new FontFamily("Xbox Fluent"),
                        RenderTransform = new TranslateTransform(0, 4)
                    };

                    icon.SetResourceReference(TextBlock.FontSizeProperty, UiScaleKeys.SettingsOverlayDescFontSize);
                    tb.Inlines.Add(new InlineUIContainer(icon));
                }

                tb.Inlines.Add(new Run(parts[i]));
            }
        }

        private void ShowExitDisplayConfirmation()
        {
            _exitConfirmationMode = true;

            SettingsListBox.Visibility = Visibility.Collapsed;

            ExitConfirmText.Visibility = Visibility.Visible;
            ExitConfirmationListBox.Visibility = Visibility.Visible;
            ExitConfirmationListBox.SelectedIndex = 0;

            ExitConfirmationListBox.Focus();

            UpdateSelectionState();
        }

        private void HideExitDisplayConfirmation(bool focusSettings = true)
        {
            _exitConfirmationMode = false;

            ExitConfirmText.Visibility = Visibility.Collapsed;
            ExitConfirmationListBox.Visibility = Visibility.Collapsed;

            SettingsListBox.Visibility = Visibility.Visible;

            if (focusSettings && Visibility == Visibility.Visible)
                SettingsListBox.Focus();

            UpdateSelectionState();
        }

        // Recursively find a named child in the visual tree.
        private T FindVisualChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                {
                    if (string.IsNullOrEmpty(childName))
                        return typedChild;

                    if (child is FrameworkElement fe && fe.Name == childName)
                        return typedChild;
                }

                T result = FindVisualChild<T>(child, childName);

                if (result != null)
                    return result;
            }

            return null;
        }
    }
}