
using System;
using System.Windows;

namespace EZ2Play.App
{
    /// <summary>
    /// Calculates UI scaling factors and populates WPF resources
    /// with dimensions adjusted to the current window size.
    /// </summary>
    /// <remarks>
    /// The reference resolution is 2560x1440.
    /// General UI scaling uses the window height, while
    /// individual components may use separate layout calculations.
    ///
    /// Resource names are defined in UiScaleKeys.
    /// </remarks>
    public static class LayoutScaler
    {
        // Reference resolution used for UI scaling.
        public const double ReferenceWidth = 2560.0;
        public const double ReferenceHeight = 1440.0;

        // Allowed scale factor range.
        public static double MinScale { get; set; } = 0.5;
        public static double MaxScale { get; set; } = 2.0;

        // Allowed font size range.
        public static double MinFontSize { get; set; } = 10.0;
        public static double MaxFontSize { get; set; } = 120.0;

        /// <summary>
        /// Calculates the scale factor relative to the reference
        /// window height, respecting the configured limits.
        /// </summary>
        public static double GetScaleFactor(double actualWindowHeight)
        {
            if (actualWindowHeight <= 0) return 1.0;

            double raw = actualWindowHeight / ReferenceHeight;

            return Math.Max(MinScale, Math.Min(MaxScale, raw));
        }

        /// <summary>
        /// Scales a base dimension using the current window height.
        /// </summary>
        public static double Scale(double baseValue, double actualWindowHeight)
        {
            return baseValue * GetScaleFactor(actualWindowHeight);
        }

        /// <summary>
        /// Scales a font size and restricts it to the configured
        /// minimum and maximum values.
        /// </summary>
        public static double GetScaledFontSize(double baseFontSize, double actualWindowHeight)
        {
            double size = Scale(baseFontSize, actualWindowHeight);

            return Math.Max(MinFontSize, Math.Min(MaxFontSize, size));
        }

        /// <summary>
        /// Populates the target ResourceDictionary with scaled
        /// dimensions, margins, padding, and font sizes.
        /// </summary>
        /// <remarks>
        /// These resources are consumed by WPF elements through
        /// their existing resource references.
        ///
        /// The order, values, and resource types must remain
        /// compatible with the original interface.
        /// </remarks>
        public static void ApplyUiScaleToDictionary(
            ResourceDictionary target,
            double windowHeight)
        {
            if (target == null) return;

            // Local helpers preserve the original scale calculations.
            double s(double baseVal) => Scale(baseVal, windowHeight);
            double fs(double baseVal) => GetScaledFontSize(baseVal, windowHeight);

            // Shared
            target[UiScaleKeys.DividerThickness] = s(2);
            target[UiScaleKeys.BackgroundTransitionSlide] = s(16);

            // Overlays
            target[UiScaleKeys.OverlayWidth] = s(1280);
            target[UiScaleKeys.OverlayRadius] = new CornerRadius(s(16));
            target[UiScaleKeys.OverlayPadding] = new Thickness(s(8));
            target[UiScaleKeys.OverlayPrimaryFontSize] = fs(32);
            target[UiScaleKeys.OverlaySecondaryFontSize] = fs(24);

            // Settings
            target[UiScaleKeys.SettingsOverlayLabelMargin] = new Thickness(0, 0, 0, s(8));
            target[UiScaleKeys.SettingsOverlayLabelTreeMargin] = new Thickness(0, 0, 0, s(4));
            target[UiScaleKeys.SettingsOverlayDescFontSize] = fs(22);
            target[UiScaleKeys.SettingsOverlayItemPadding] = new Thickness(0, s(32), 0, s(32));

            target[UiScaleKeys.SettingsOverlayTreeItemsContainerMargin] = new Thickness(0, 0, 0, 0);
            target[UiScaleKeys.SettingsOverlayTreeItemsContainerPadding] = new Thickness(s(16), 0, s(16), s(16));
            target[UiScaleKeys.SettingsOverlayTreeItemRadius] = new CornerRadius(s(4));
            target[UiScaleKeys.SettingsOverlayTreeItemPadding] = new Thickness(0, s(12), 0, s(12));
            target[UiScaleKeys.SettingsOverlayTreeItemMargin] = new Thickness(s(16));

            target[UiScaleKeys.SettingsOverlayDividerMargin] = new Thickness(0);
            target[UiScaleKeys.SettingsOverlayAppInfoMargin] = new Thickness(0, s(24), 0, s(24));

            target[UiScaleKeys.ToggleSwitchWidth] = s(64);
            target[UiScaleKeys.ToggleSwitchHeight] = s(32);
            target[UiScaleKeys.CheckBoxSize] = s(64);

            // Parser: game list
            const double parserGameItemHeight = 128;
            const int parserVisibleGameRows = 5;

            target[UiScaleKeys.ParserGameItemHeight] = s(parserGameItemHeight);
            target[UiScaleKeys.ParserGamesViewportHeight] = s(parserGameItemHeight * parserVisibleGameRows);
            target[UiScaleKeys.ParserGameItemMargin] = new Thickness(s(50), 0, s(50), 0);

            // Parser: media
            const double mediaRadius = 16;
            const double mediaCoverSize = 256;

            target[UiScaleKeys.ParserCoverSize] = s(mediaCoverSize);
            target[UiScaleKeys.ParserMediaRadius] = s(mediaRadius);

            target[UiScaleKeys.ParserBackgroundWidth] = s(587);
            target[UiScaleKeys.ParserBackgroundHeight] = s(190);

            // Parser: tabs
            target[UiScaleKeys.ParserTabsMargin] = new Thickness(s(40), s(32), 0, s(32));
            target[UiScaleKeys.ParserTabMargin] = new Thickness(s(24), 0, s(24), 0);

            // Parser: progress indicator
            target[UiScaleKeys.ParserProgressHeight] = s(4);

            // Parser: manual search
            target[UiScaleKeys.ParserInputHeight] = s(72);
            target[UiScaleKeys.ParserInputFontSize] = fs(40);
            target[UiScaleKeys.ParserInputMargin] = new Thickness(s(16));
            target[UiScaleKeys.ParserManualSearchHintMargin] = new Thickness(0, s(16), 0, s(4));

            // Parser: assets and status
            target[UiScaleKeys.ParserAssetsMargin] = new Thickness(s(32));
            target[UiScaleKeys.ParserAssetsGap] = new Thickness(s(16));
            target[UiScaleKeys.ParserStatusMargin] = new Thickness(0, s(16), 0, s(16));

            target[UiScaleKeys.ParserStatusSurfaceMaxWidth] = s(800);
            target[UiScaleKeys.ParserStatusSurfaceMargin] = new Thickness(s(128));
            target[UiScaleKeys.ParserStatusSurfacePadding] = new Thickness(s(32), s(4), s(32), s(4));
            target[UiScaleKeys.ParserStatusSurfaceRadius] = new CornerRadius(s(4));
            target[UiScaleKeys.ParserStatusFontSize] = fs(22);

            // Main: game covers and splash screen
            target[UiScaleKeys.GameCoverRadius] = s(mediaRadius);
            target[UiScaleKeys.SplashLogoMaxHeight] = s(256);

            // Main: empty library
            target[UiScaleKeys.NoShortcutsMargin] = new Thickness(0, 0, 0, s(96));
            target[UiScaleKeys.NoShortcutsFontSize] = fs(30);

            // Main: exit message
            target[UiScaleKeys.ExitMessageFontSize] = fs(36);

            // Main: top information panel
            target[UiScaleKeys.TopPanelMargin] = new Thickness(0, s(24), 0, 0);
            target[UiScaleKeys.TopInfoTabsMargin] = new Thickness(s(72), 0, 0, 0);
            target[UiScaleKeys.TopInfoPrimaryFontSize] = fs(42);
            target[UiScaleKeys.TopInfoSecondaryFontSize] = fs(38);
            target[UiScaleKeys.UserAvatarSize] = s(50);

            // Selector: media items
            const double strokeInset = 0;
            const double strokeOutset = 8;
            const double strokeThickness = 4;

            target[UiScaleKeys.SelectorMediaStrokeInset] = s(strokeInset);
            target[UiScaleKeys.SelectorMediaStrokeOutset] = s(strokeOutset);
            target[UiScaleKeys.SelectorMediaStrokeThickness] = s(strokeThickness);
            target[UiScaleKeys.SelectorMediaStrokeRadius] = s(mediaRadius + strokeOutset - strokeInset - strokeThickness / 2);
            target[UiScaleKeys.SelectorMediaBackgroundInset] = s(0);
            target[UiScaleKeys.SelectorMediaBackgroundOutset] = s(0);
            target[UiScaleKeys.SelectorMediaBackgroundRadius] = s(mediaRadius);

            // Selector: overlay items
            target[UiScaleKeys.SelectorOverlayItemStrokeInset] = s(8);
            target[UiScaleKeys.SelectorOverlayItemStrokeOutset] = s(0);
            target[UiScaleKeys.SelectorOverlayItemStrokeThickness] = s(strokeThickness);
            target[UiScaleKeys.SelectorOverlayItemStrokeRadius] = s(8);
            target[UiScaleKeys.SelectorOverlayItemBackgroundInset] = s(16);
            target[UiScaleKeys.SelectorOverlayItemBackgroundOutset] = s(0);
            target[UiScaleKeys.SelectorOverlayItemBackgroundRadius] = s(4);

            // Main: selected game title and loading indicator
            target[UiScaleKeys.GameTitleMargin] = new Thickness(s(224), 0, 0, 0);
            target[UiScaleKeys.GameTitleFontSize] = fs(72);
            target[UiScaleKeys.LoadingProgressSize] = fs(48);

            // Source pill
            target[UiScaleKeys.SourcePillWidth] = s(320);
            target[UiScaleKeys.SourcePillHeight] = s(64);
            target[UiScaleKeys.SourcePillRadius] = new CornerRadius(s(32));
            target[UiScaleKeys.SourcePillMargin] = new Thickness(0, s(72), 0, 0);
            target[UiScaleKeys.SourcePillFontSize] = fs(28);

            // Game counter pill
            target[UiScaleKeys.CounterPillHeight] = s(64);
            target[UiScaleKeys.CounterPillPadding] = new Thickness(s(32), 0, s(32), 0);
            target[UiScaleKeys.CounterPillMargin] = new Thickness(s(48), 0, 0, 0);
            target[UiScaleKeys.CounterPillIconMargin] = new Thickness(0, 0, s(8), 0);
            target[UiScaleKeys.CounterPillFontSize] = fs(22);
            target[UiScaleKeys.CounterPillIconSize] = fs(22);

            // Notifications
            target[UiScaleKeys.NotificationPanelHeight] = s(96);
            target[UiScaleKeys.NotificationPanelRadius] = new CornerRadius(s(16));
            target[UiScaleKeys.NotificationPanelMaxWidth] = s(1024);
            target[UiScaleKeys.NotificationPanelPadding] = new Thickness(s(42), 0, s(64), 0);
            target[UiScaleKeys.NotificationPanelOuterMargin] = new Thickness(0, 0, s(48), 0);
            target[UiScaleKeys.NotificationPanelMargin] = new Thickness(0, 0, s(24), 0);
            target[UiScaleKeys.NotificationPanelFontSize] = fs(22);
            target[UiScaleKeys.NotificationPanelIconSize] = fs(28);

            // Input hints: panel
            target[UiScaleKeys.HintPanelHeight] = s(64);
            target[UiScaleKeys.HintPanelMargin] = new Thickness(0, 0, s(96), s(64));
            target[UiScaleKeys.HintPanelPadding] = new Thickness(s(32), 0, s(32), 0);

            // Input hints: content
            target[UiScaleKeys.HintBlockMargin] = new Thickness(s(16), 0, s(16), 0);
            target[UiScaleKeys.HintTextMargin] = new Thickness(s(16), 0, 0, 0);
            target[UiScaleKeys.HintIconHeightGamepad] = s(28);
            target[UiScaleKeys.HintIconHeightKeyboard] = s(26);
            target[UiScaleKeys.HintTextFontSize] = fs(24);
        }
    }
}
