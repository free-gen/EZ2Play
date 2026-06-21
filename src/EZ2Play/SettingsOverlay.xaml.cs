using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Documents;
using System.Windows.Media;

namespace EZ2Play.App
{
    public partial class SettingsOverlay : UserControl
    {
        private InputHandler _inputHandler;
        private MainWindow _mainWindow;
        private AppConfig _config;
        private double fadeDuration = 0.1;

        private bool _displayWasChangedInSession = false;
        private bool _exitConfirmationMode = false;
        private string _cachedArgs = "";
        
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
                
                // Если hotswap - удаляем пункт с дисплеем
                if (_mainWindow.IsHotSwapLaunch())
                {
                    SettingsListBox.Items.Remove(SettingsSourceDisplay);
                }
                
                // Устанавливаем первый видимый элемент
                if (SettingsListBox.Items.Count > 0)
                    SettingsListBox.SelectedIndex = 0;
            };
            SettingsListBox.SelectedIndex = 0;
            Opacity = 0;
            Visibility = Visibility.Collapsed;
        }
        
        public void Open()
        {
            if (Visibility == Visibility.Visible) return;
            _mainWindow.SetHintsMode(HintPanel.HintMode.Settings);
            
            _inputHandler.SetSettingsOpen(true);
            Visibility = Visibility.Visible;

            if (SettingsListBox.Items.Count > 0)
                SettingsListBox.SelectedIndex = 0;
            
            // Обновляем состояние автозапуска при открытии
            RefreshAutorunState();
            
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(fadeDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            fadeIn.Completed += (s, e) => SettingsListBox.Focus();
            BeginAnimation(OpacityProperty, fadeIn);
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

        // Обновить список дисплеев
        private void RefreshDisplayList()
        {
            var display = _mainWindow.GetDisplay();
            display.RefreshDisplayList();
        }

        // Обновить состояние автозапуска в UI
        private void RefreshAutorunState()
        {
            // Проверяем реальное состояние в реестре
            bool registryState = SystemProvider.IsAutorunEnabled();
            bool configState = _config.AutorunEnabled;
            
            // Синхронизируем конфиг с реестром, если они различаются
            if (registryState != configState)
            {
                _config.AutorunEnabled = registryState;
                _config.Save();
            }
            
            AutorunToggle.IsChecked = registryState;
            UpdateSubOptionsVisibility(registryState);
            LoadSubOptionsStates();
        }

        private void UpdateSubOptionsVisibility(bool enabled)
        {
            if (enabled)
            {
                NoSplash.Visibility = Visibility.Visible;
                NoMusic.Visibility = Visibility.Visible;
                HotSwap.Visibility = Visibility.Visible;
            }
            else
            {
                NoSplash.Visibility = Visibility.Collapsed;
                NoMusic.Visibility = Visibility.Collapsed;
                HotSwap.Visibility = Visibility.Collapsed;
            }
        }
        
        // Навигация
        public void Navigate(int direction, bool isHorizontal = true)
        {
            if (isHorizontal)
            {
                return;
            }
            
            int newIndex = SettingsListBox.SelectedIndex + direction;
            while (newIndex >= 0 && newIndex < SettingsListBox.Items.Count)
            {
                var item = SettingsListBox.Items[newIndex] as ListBoxItem;
                if (item != null && item.Visibility == Visibility.Visible)
                {
                    SettingsListBox.SelectedIndex = newIndex;
                    SettingsListBox.ScrollIntoView(SettingsListBox.SelectedItem);
                    return;
                }
                newIndex += direction;
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

                    _displayWasChangedInSession = false;
                    _exitConfirmationMode = false;
                }
                return;
            }

            if (SettingsListBox.SelectedItem == NoSplash)
            {
                bool newState = !NoSplashToggle.IsChecked.GetValueOrDefault(false);
                NoSplashToggle.IsChecked = newState;
                UpdateAutorunArguments();
            }
            else if (SettingsListBox.SelectedItem == NoMusic)
            {
                bool newState = !NoMusicToggle.IsChecked.GetValueOrDefault(false);
                NoMusicToggle.IsChecked = newState;
                UpdateAutorunArguments();
            }
            else if (SettingsListBox.SelectedItem == HotSwap)
            {
                bool newState = !HotSwapToggle.IsChecked.GetValueOrDefault(false);
                HotSwapToggle.IsChecked = newState;
                UpdateAutorunArguments();
            }
            else if (SettingsListBox.SelectedItem == SettingsAutorunApp)
            {
                bool newState = !AutorunToggle.IsChecked.GetValueOrDefault(false);
                SetAutorunState(newState);
            }
            else if (SettingsListBox.SelectedItem == SettingsExitApp)
            {
                if (_displayWasChangedInSession)
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
                    _displayWasChangedInSession = true;
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
                // System.Diagnostics.Debug.WriteLine($"Failed to set autorun state: {ex.Message}");
                RefreshAutorunState();
            }
        }

        private void UpdateAutorunArguments()
        {
            string args = "";
            if (NoSplashToggle.IsChecked.GetValueOrDefault(false))
                args += "--nosplash ";
            if (NoMusicToggle.IsChecked.GetValueOrDefault(false))
                args += "--nomusic ";
            if (HotSwapToggle.IsChecked.GetValueOrDefault(false))
                args += "--hotswap ";
            
            args = args.TrimEnd();
            
            // Обновляем ярлык
            SystemProvider.SetAutorunArguments(args);
            
            // Если автозапуск включен - обновляем видимость
            if (AutorunToggle.IsChecked.GetValueOrDefault(false))
            {
                UpdateSubOptionsVisibility(true);
            }
        }

        private void LoadSubOptionsStates()
        {
            string args = SystemProvider.GetAutorunArguments(); // новый метод
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
    }
}