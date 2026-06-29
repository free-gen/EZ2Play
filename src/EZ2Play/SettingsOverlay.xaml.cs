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
        private string _cachedArgs = " ";

        // Замороженные кисти для производительности
        private static readonly Brush SelectedBorderBrush;
        private static readonly Brush SelectedBackgroundBrush;
        private static readonly Brush TransparentBrush = Brushes.Transparent;

        static SettingsOverlay()
        {
            SelectedBorderBrush = new SolidColorBrush(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF));
            SelectedBorderBrush.Freeze();
            SelectedBackgroundBrush = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF));
            SelectedBackgroundBrush.Freeze();
        }

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
                LoadSubOptionsStates();

                if (_mainWindow.IsHotSwapLaunch())
                {
                    SettingsListBox.Items.Remove(SettingsSourceDisplay);
                }

                if (SettingsListBox.Items.Count > 0)
                    SettingsListBox.SelectedIndex = 0;

                if (SubOptionsListBox.Items.Count > 0)
                    SubOptionsListBox.SelectedIndex = 0;

                // Подписываемся на изменение выбора в обоих ListBox
                SettingsListBox.SelectionChanged += OnSelectionChanged;
                SubOptionsListBox.SelectionChanged += OnSelectionChanged;

                // Подписываемся на тумблер (ОДИН РАЗ)
                AutorunToggle.Checked += (sender, args) => ScheduleUpdateTreeHeaderDivider();
                AutorunToggle.Unchecked += (sender, args) => ScheduleUpdateTreeHeaderDivider();

                // Первоначальная отрисовка селектора
                ScheduleUpdateSelectionVisuals();
                
                // Планируем обновление разделителя ПОСЛЕ генерации контейнеров
                ScheduleUpdateTreeHeaderDivider();
            };

            SettingsListBox.SelectedIndex = 0;
            SubOptionsListBox.SelectedIndex = 0;
            Opacity = 0;
            Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Общий обработчик SelectionChanged для обоих ListBox.
        /// </summary>
        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ScheduleUpdateSelectionVisuals();
        }

        /// <summary>
        /// Откладывает обновление визуала до завершения генерации контейнеров.
        /// </summary>
        private void ScheduleUpdateSelectionVisuals()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateSelectionVisuals));
        }

        private void ScheduleUpdateTreeHeaderDivider()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateTreeHeaderDivider));
        }

        private void UpdateTreeHeaderDivider()
        {
            if (SettingsListBox.Items.Count == 0) return;

            // SettingsAutorunApp — первый элемент в списке (индекс 0)
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

        /// <summary>
        /// Полностью управляет видимостью рамок селектора в обоих ListBox.
        /// Определяет, какой ListBox сейчас активен, и рисует рамку только на одном элементе.
        /// </summary>
        private void UpdateSelectionVisuals()
        {
            // Активен SubOptionsListBox, только если выбран TreeItemsContainer
            bool isSubOptionsActive = (SettingsListBox.SelectedItem == TreeItemsContainer);

            // === Обрабатываем SettingsListBox ===
            for (int i = 0; i < SettingsListBox.Items.Count; i++)
            {
                var container = SettingsListBox.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (container == null) continue;

                // Рамка видна, только если SubOptions НЕ активен И этот контейнер выбран
                bool shouldBeSelected = !isSubOptionsActive && container.IsSelected;
                ApplySelectionVisual(container, shouldBeSelected);
            }

            // === Обрабатываем SubOptionsListBox ===
            for (int i = 0; i < SubOptionsListBox.Items.Count; i++)
            {
                var container = SubOptionsListBox.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (container == null) continue;

                // Рамка видна, только если SubOptions АКТИВЕН И этот контейнер выбран
                bool shouldBeSelected = isSubOptionsActive && container.IsSelected;
                ApplySelectionVisual(container, shouldBeSelected);
            }
        }

        /// <summary>
        /// Применяет или снимает визуал выделения с конкретного контейнера.
        /// </summary>
        private void ApplySelectionVisual(ListBoxItem container, bool isSelected)
        {
            var border = FindVisualChild<Border>(container, "SelectionBorder");
            var bgBorder = FindVisualChild<Border>(container, "SelectionBackground");

            if (border != null)
                border.BorderBrush = isSelected ? SelectedBorderBrush : TransparentBrush;

            if (bgBorder != null)
                bgBorder.Background = isSelected ? SelectedBackgroundBrush : TransparentBrush;
        }

        public void Open()
        {
            if (Visibility == Visibility.Visible) return;
            _mainWindow.SetHintsMode(HintPanel.HintMode.Settings);

            _inputHandler.SetSettingsOpen(true);
            Visibility = Visibility.Visible;

            if (SettingsListBox.Items.Count > 0)
                SettingsListBox.SelectedIndex = 0;

            RefreshAutorunState();
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

            ScheduleUpdateSelectionVisuals();
        }

        public void Close()
        {
            if (Visibility != Visibility.Visible) return;

            var fadeOut = new DoubleAnimation
            {
                From = Opacity,
                To = 0,
                Duration = TimeSpan.FromSeconds(fadeDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                Visibility = Visibility.Collapsed;
                _inputHandler.SetSettingsOpen(false);
                _mainWindow.SetHintsMode(HintPanel.HintMode.Main);
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }

        private void RefreshDisplayList()
        {
            var display = _mainWindow.GetDisplay();
            display.RefreshDisplayList();
        }

        private void RefreshAutorunState()
        {
            bool registryState = SystemProvider.IsAutorunEnabled();
            bool configState = _config.AutorunEnabled;

            if (registryState != configState)
            {
                _config.AutorunEnabled = registryState;
                _config.Save();
            }

            AutorunToggle.IsChecked = registryState;
            UpdateTreeHeaderDivider();
            UpdateSubOptionsVisibility(registryState);
            LoadSubOptionsStates();

            ScheduleUpdateSelectionVisuals();
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

            ScheduleUpdateSelectionVisuals();
        }

        public void Navigate(int direction, bool isHorizontal = true)
        {
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
                            // SelectionChanged сам вызовет ScheduleUpdateSelectionVisuals
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

                    // SelectionChanged сам вызовет ScheduleUpdateSelectionVisuals
                    return;
                }
                newIndexVert += direction;
            }
        }

        public void Confirm()
        {
            if (SettingsListBox.SelectedItem is ListBoxItem selectedItem && selectedItem.Visibility != Visibility.Visible)
            {
                return;
            }

            if (_exitConfirmationMode)
            {
                if (SettingsListBox.SelectedItem is ListBoxItem item)
                {
                    if ((string)item.Tag == "ConfirmYes")
                    {
                        _mainWindow.GetDisplay().RunDisplaySwitch("/internal");
                    }

                    _mainWindow.ExitApplication();
                    Close();

                    _exitConfirmationMode = false;
                }
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
                {
                    _mainWindow.GetDisplay().SwitchDisplay(1);
                }
            }
        }

        private void SetAutorunState(bool enabled)
        {
            try
            {
                if (enabled)
                {
                    SystemProvider.EnableAutorun();
                }
                else
                {
                    SystemProvider.DisableAutorun();
                }

                UpdateSubOptionsVisibility(enabled);
                _config.AutorunEnabled = enabled;
                _config.Save();
                AutorunToggle.IsChecked = enabled;

                LoadSubOptionsStates();
            }
            catch (Exception)
            {
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

            SystemProvider.SetAutorunArguments(args);

            if (AutorunToggle.IsChecked.GetValueOrDefault(false))
            {
                UpdateSubOptionsVisibility(true);
            }
        }

        private void LoadSubOptionsStates()
        {
            string args = SystemProvider.GetAutorunArguments();
            _cachedArgs = args;

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
            ExitConfirmText.Visibility = Visibility.Visible;
            SettingsListBox.Items.Clear();

            var yesItem = new ListBoxItem
            {
                Content = CreateConfirmItem(Locals.GetString("ConfirmYes")),
                Tag = "ConfirmYes"
            };

            var noItem = new ListBoxItem
            {
                Content = CreateConfirmItem(Locals.GetString("ConfirmNo")),
                Tag = "ConfirmNo"
            };

            SettingsListBox.Items.Add(yesItem);
            SettingsListBox.Items.Add(noItem);

            SettingsListBox.SelectedIndex = 0;

            ScheduleUpdateSelectionVisuals();
        }

        private Grid CreateConfirmItem(string text)
        {
            var grid = new Grid();
            var tb = new TextBlock
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            grid.Children.Add(tb);
            return grid;
        }

        /// <summary>
        /// Рекурсивно ищет дочерний элемент визуального дерева по имени.
        /// </summary>
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