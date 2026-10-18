
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EZ2Play.App
{
    /// <summary>
    /// Displays context-sensitive input hints for the
    /// current application screen and input device.
    /// </summary>
    /// <remarks>
    /// Hint content is rebuilt whenever the active screen
    /// or input device changes.
    ///
    /// Gamepad hints use glyphs from the Xbox icon font.
    /// Keyboard hints use existing WPF resources.
    /// </remarks>
    public class HintPanel : Border
    {
        /// <summary>
        /// Defines the application screen whose
        /// available actions should be displayed.
        /// </summary>
        public enum HintMode
        {
            Main,
            Settings,
            ParserGames,
            ParserAssets
        }

        /// <summary>
        /// Defines the input device used to display hints.
        /// </summary>
        public enum InputDevice
        {
            Keyboard,
            Gamepad
        }

        // Container holding the currently displayed hint blocks.
        private StackPanel _container;

        // Maps logical button names to Xbox glyphs,
        // keyboard resource keys, and icon colors.
        private Dictionary<string, (string glyph, string pathKey, Brush color)> _buttonIcons;

        /// <summary>
        /// Identifies the current hint display mode.
        /// Changes automatically rebuild the hint panel.
        /// </summary>
        public static readonly DependencyProperty ModeProperty =
            DependencyProperty.Register(
                nameof(Mode),
                typeof(HintMode),
                typeof(HintPanel),
                new PropertyMetadata(
                    HintMode.Main,
                    OnModeChanged));

        /// <summary>
        /// Identifies the active input device.
        /// Changes automatically rebuild the hint panel.
        /// </summary>
        public static readonly DependencyProperty DeviceProperty =
            DependencyProperty.Register(
                nameof(Device),
                typeof(InputDevice),
                typeof(HintPanel),
                new PropertyMetadata(
                    InputDevice.Keyboard,
                    OnDeviceChanged));

        public HintMode Mode
        {
            get => (HintMode)GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        public InputDevice Device
        {
            get => (InputDevice)GetValue(DeviceProperty);
            set => SetValue(DeviceProperty, value);
        }

        public HintPanel()
        {
            // Button definitions:
            // glyph   - character from the Xbox icon font;
            // pathKey - WPF resource representing a keyboard key;
            // color   - foreground color of the gamepad glyph.
            _buttonIcons = new Dictionary<string, (string, string, Brush)>
            {
                ["BtnA"] = (
                    "\uE3CE",
                    "KeyEnter",
                    new SolidColorBrush(Color.FromRgb(0x30, 0xC8, 0x30))),

                ["BtnB"] = (
                    "\uE3CD",
                    "KeyEscape",
                    new SolidColorBrush(Color.FromRgb(0xEB, 0x1E, 0x00))),

                ["BtnX"] = (
                    "\uE3CB",
                    "KeyX",
                    new SolidColorBrush(Color.FromRgb(0x00, 0xA0, 0xE0))),

                ["BtnY"] = (
                    "\uE3CC",
                    "KeyY",
                    new SolidColorBrush(Color.FromRgb(0xFF, 0xB9, 0x00))),

                ["BtnLb"] = (
                    "\uE3ED",
                    null,
                    new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xD3))),

                ["BtnRb"] = (
                    "\uE3EB",
                    null,
                    new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xD3))),

                ["BtnStart"] = (
                    "\uE3EC",
                    "KeyEscape",
                    new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xD3))),

                ["BtnXbox"] = (
                    "\uE636",
                    null,
                    new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xD3))),

                ["BtnXboxFill"] = (
                    "\uE3E3",
                    null,
                    new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xD3)))
            };

            HorizontalAlignment = HorizontalAlignment.Right;
            VerticalAlignment = VerticalAlignment.Bottom;

            Render();
        }

        /// <summary>
        /// Rebuilds the hint panel according to the
        /// currently selected HintMode.
        /// </summary>
        /// <remarks>
        /// The previous StackPanel is removed before the
        /// new collection of hint blocks is created.
        ///
        /// Display text is retrieved through Locals so
        /// hints follow the application's localization.
        /// </remarks>
        private void Render()
        {
            if (_container != null)
                Child = null;

            _container = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            Child = _container;

            // Main screen: launch game, manage artwork, settings.
            if (Mode == HintMode.Main)
            {
                _container.Children.Add(
                    CreateHintBlock(
                        "BtnA",
                        Locals.GetString("Launch")));

                _container.Children.Add(
                    CreateHintBlock(
                        "BtnX",
                        Locals.GetString("ParserOverlay")));

                _container.Children.Add(
                    CreateHintBlock(
                        "BtnStart",
                        Locals.GetString("SettingsOverlay")));
            }
            // Parser game search: select, search, and go back.
            else if (Mode == HintMode.ParserGames)
            {
                _container.Children.Add(
                    CreateHintBlock(
                        "BtnA",
                        Locals.GetString("Select")));

                _container.Children.Add(
                    CreateHintBlock(
                        "BtnY",
                        Locals.GetString("Search")));

                _container.Children.Add(
                    CreateHintBlock(
                        "BtnB",
                        Locals.GetString("Back")));
            }
            // Parser artwork: select an asset or go back.
            else if (Mode == HintMode.ParserAssets)
            {
                _container.Children.Add(
                    CreateHintBlock(
                        "BtnA",
                        Locals.GetString("Select")));

                _container.Children.Add(
                    CreateHintBlock(
                        "BtnB",
                        Locals.GetString("Back")));
            }
            // Settings and other modes: select or go back.
            else
            {
                _container.Children.Add(
                    CreateHintBlock(
                        "BtnA",
                        Locals.GetString("Select")));

                _container.Children.Add(
                    CreateHintBlock(
                        "BtnB",
                        Locals.GetString("Back")));
            }
        }

        /// <summary>
        /// Creates a horizontal hint block containing
        /// an optional input icon and localized text.
        /// </summary>
        private FrameworkElement CreateHintBlock(
            string buttonKey,
            string text)
        {
            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Use dynamically scaled spacing between hint blocks.
            stack.SetResourceReference(
                StackPanel.MarginProperty,
                UiScaleKeys.HintBlockMargin);

            var firstIcon = CreateHintIcon(buttonKey);

            if (firstIcon != null)
                stack.Children.Add(firstIcon);

            var textBlock = new TextBlock
            {
                Text = text,
                Style = Application.Current.FindResource("HintTextStyle") as Style,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Keep margins and font sizes linked to UI scaling.
            textBlock.SetResourceReference(
                TextBlock.MarginProperty,
                UiScaleKeys.HintTextMargin);

            textBlock.SetResourceReference(
                TextBlock.FontSizeProperty,
                UiScaleKeys.HintTextFontSize);

            stack.Children.Add(textBlock);

            return stack;
        }

        /// <summary>
        /// Creates the visual representation of a button
        /// for the currently active input device.
        /// </summary>
        /// <returns>
        /// A gamepad or keyboard icon when available;
        /// otherwise null.
        /// </returns>
        private FrameworkElement CreateHintIcon(string buttonKey)
        {
            var icon = _buttonIcons[buttonKey];

            // Gamepad icons are rendered as Xbox font glyphs.
            if (Device == InputDevice.Gamepad && icon.glyph != null)
            {
                var viewbox = new Viewbox();

                viewbox.SetResourceReference(
                    Viewbox.HeightProperty,
                    UiScaleKeys.HintIconHeightGamepad);

                viewbox.Stretch = Stretch.Uniform;
                viewbox.VerticalAlignment = VerticalAlignment.Center;

                viewbox.Child = new TextBlock
                {
                    Text = icon.glyph,
                    FontFamily = (FontFamily)Application.Current.Resources["XboxFont"],
                    Foreground = icon.color
                };

                return viewbox;
            }

            // Keyboard icons are loaded from existing WPF resources.
            if (Device == InputDevice.Keyboard && icon.pathKey != null)
            {
                var viewbox = new Viewbox();

                viewbox.SetResourceReference(
                    Viewbox.HeightProperty,
                    UiScaleKeys.HintIconHeightKeyboard);

                viewbox.Stretch = Stretch.Uniform;
                viewbox.VerticalAlignment = VerticalAlignment.Center;

                viewbox.Child = new ContentPresenter
                {
                    Content = Application.Current.FindResource(icon.pathKey)
                };

                return viewbox;
            }

            return null;
        }

        /// <summary>
        /// Rebuilds hint content when the active screen changes.
        /// </summary>
        private static void OnModeChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            ((HintPanel)d).Render();
        }

        /// <summary>
        /// Rebuilds hint icons when the active input device changes.
        /// </summary>
        private static void OnDeviceChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            ((HintPanel)d).Render();
        }
    }
}
