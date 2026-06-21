using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EZ2Play.App
{
    public class HintPanel : Border
    {
        public enum HintMode { Main, Settings }
        public enum InputDevice { Keyboard, Gamepad }

        private StackPanel _container;
        
        private Dictionary<string, (string glyph, string pathKey, Brush color)> _buttonIcons;

        public static readonly DependencyProperty ModeProperty =
            DependencyProperty.Register(nameof(Mode), typeof(HintMode), typeof(HintPanel),
                new PropertyMetadata(HintMode.Main, OnModeChanged));

        public static readonly DependencyProperty DeviceProperty =
            DependencyProperty.Register(nameof(Device), typeof(InputDevice), typeof(HintPanel),
                new PropertyMetadata(InputDevice.Keyboard, OnDeviceChanged));

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

        // Словарь иконок
        public HintPanel()
        {
            _buttonIcons = new Dictionary<string, (string, string, Brush)>
            {
                ["BtnA"]     = ("\uE3CE", "KeyEnter", new SolidColorBrush(Color.FromRgb(0x30, 0xC8, 0x30))),
                ["BtnB"]     = ("\uE3CD", "KeyEscape", new SolidColorBrush(Color.FromRgb(0xEB, 0x1E, 0x00))),
                ["BtnX"]     = ("\uE3CB", null, new SolidColorBrush(Color.FromRgb(0x00, 0xA0, 0xE0))),
                ["BtnY"]     = ("\uE3CC", null, new SolidColorBrush(Color.FromRgb(0xFF, 0xB9, 0x00))),
                ["BtnLb"] = ("\uE3ED", null, new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xD3))),
                ["BtnRb"] = ("\uE3EB", null, new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xD3))),
                ["BtnStart"] = ("\uE3EC", "KeyEscape", new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xD3))),
                ["BtnXbox"] = ("\uE636", null, new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xD3))),
                ["BtnXboxFill"] = ("\uE3E3", null, new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xD3))),
            };

            Style = Application.Current.FindResource("HintCardStyle") as Style;
            HorizontalAlignment = HorizontalAlignment.Right;
            VerticalAlignment = VerticalAlignment.Bottom;
            
            Render();
        }

        private void Render()
        {
            if (_container != null)
                Child = null;

            _container = new StackPanel { Orientation = Orientation.Horizontal };
            Child = _container;

            // Разметка основного экрана
            if (Mode == HintMode.Main)
            {
                _container.Children.Add(CreateHintBlock("BtnA", Locals.GetString("Launch")));
                _container.Children.Add(CreateHintBlock("BtnStart", Locals.GetString("SettingsOverlay")));
                // _container.Children.Add(CreateHintBlock("BtnLb", Locals.GetString("SwitchTabs")));
            }
            // Разметка в настройках
            else
            {
                _container.Children.Add(CreateHintBlock("BtnA", Locals.GetString("Select")));
                _container.Children.Add(CreateHintBlock("BtnB", Locals.GetString("Back")));
            }
        }

        private FrameworkElement CreateHintBlock(string buttonKey, string text)
        {
            var stack = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.SetResourceReference(StackPanel.MarginProperty, UiScaleKeys.HintBlockMargin);

            var icon = _buttonIcons[buttonKey];

            if (Device == InputDevice.Gamepad && icon.glyph != null)
            {
                var viewbox = new Viewbox();
                viewbox.SetResourceReference(Viewbox.HeightProperty, UiScaleKeys.HintIconHeightGamepad);
                viewbox.Stretch = Stretch.Uniform;
                viewbox.VerticalAlignment = VerticalAlignment.Center;
                
                viewbox.Child = new TextBlock
                {
                    Text = icon.glyph,
                    FontFamily = new FontFamily("Xbox Fluent"),
                    // FontWeight = FontWeights.SemiBold,
                    Foreground = icon.color
                };
                
                stack.Children.Add(viewbox);
            }
            else if (Device == InputDevice.Keyboard && icon.pathKey != null)
            {
                var viewbox = new Viewbox();
                viewbox.SetResourceReference(Viewbox.HeightProperty, UiScaleKeys.HintIconHeightKeyboard);
                viewbox.Stretch = Stretch.Uniform;
                viewbox.VerticalAlignment = VerticalAlignment.Center;
                
                viewbox.Child = new ContentPresenter
                {
                    Content = Application.Current.FindResource(icon.pathKey)
                };
                
                stack.Children.Add(viewbox);
            }

            var textBlock = new TextBlock
            {
                Text = text,
                Style = Application.Current.FindResource("HintTextStyle") as Style,
                VerticalAlignment = VerticalAlignment.Center
            };
            textBlock.SetResourceReference(TextBlock.MarginProperty, UiScaleKeys.HintTextMargin);
            textBlock.SetResourceReference(TextBlock.FontSizeProperty, UiScaleKeys.HintTextFontSize);
            
            stack.Children.Add(textBlock);

            return stack;
        }

        private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((HintPanel)d).Render();
        }

        private static void OnDeviceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((HintPanel)d).Render();
        }
    }
}