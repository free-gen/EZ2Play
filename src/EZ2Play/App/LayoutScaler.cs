using System;
using System.Windows;

namespace EZ2Play.App
{
    public static class LayoutScaler
    {
        public const double ReferenceWidth = 2560.0;
        public const double ReferenceHeight = 1440.0;
        public static double MinScale { get; set; } = 0.5;
        public static double MaxScale { get; set; } = 2.0;
        public static double MinFontSize { get; set; } = 10.0;
        public static double MaxFontSize { get; set; } = 120.0;

        public static double GetScaleFactor(double actualWindowHeight)
        {
            if (actualWindowHeight <= 0) return 1.0;

            double raw = actualWindowHeight / ReferenceHeight;
            return Math.Max(MinScale, Math.Min(MaxScale, raw));
        }

        public static double Scale(double baseValue, double actualWindowHeight)
        {
            return baseValue * GetScaleFactor(actualWindowHeight);
        }

        public static double GetScaledFontSize(double baseFontSize, double actualWindowHeight)
        {
            double size = Scale(baseFontSize, actualWindowHeight);
            return Math.Max(MinFontSize, Math.Min(MaxFontSize, size));
        }

        // Populate the target ResourceDictionary with scaled UI values.
        public static void ApplyUiScaleToDictionary(ResourceDictionary target, double windowHeight)
        {
            if (target == null) return;

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
            target[UiScaleKeys.SettingsOverlayTreeItemPadding] = new Thickness(0, s(16), 0, s(16));
            target[UiScaleKeys.SettingsOverlayTreeItemMargin] = new Thickness(s(16));

            target[UiScaleKeys.SettingsOverlayDividerMargin] = new Thickness(0);
            target[UiScaleKeys.SettingsOverlayAppInfoMargin] = new Thickness(0, s(24), 0, s(24));

            target[UiScaleKeys.ToggleSwitchWidth] = s(64);
            target[UiScaleKeys.ToggleSwitchHeight] = s(32);
            target[UiScaleKeys.CheckBoxSize] = s(64);

            // Parser
            const double parserGameItemHeight = 128;
            const int parserVisibleGameRows = 5;

            target[UiScaleKeys.ParserGameItemHeight] = s(parserGameItemHeight);
            target[UiScaleKeys.ParserGamesViewportHeight] = s(parserGameItemHeight * parserVisibleGameRows);

            const double mediaRadius = 16;
            const double mediaCoverSize = 256;
            
            target[UiScaleKeys.ParserCoverSize] = s(mediaCoverSize);
            target[UiScaleKeys.ParserMediaRadius] = s(mediaRadius);

            target[UiScaleKeys.ParserBackgroundWidth] = s(587);
            target[UiScaleKeys.ParserBackgroundHeight] = s(190);

            target[UiScaleKeys.ParserTabsMargin] = new Thickness(0, s(32), 0, s(32));
            target[UiScaleKeys.ParserTabMargin] = new Thickness(s(24), 0, s(24), 0);

            target[UiScaleKeys.ParserProgressHeight] = s(4);

            target[UiScaleKeys.ParserInputHeight] = s(72);
            target[UiScaleKeys.ParserInputFontSize] = fs(40);
            target[UiScaleKeys.ParserInputMargin] = new Thickness(s(8));
            target[UiScaleKeys.ParserManualSearchHintMargin] = new Thickness(0, s(16), 0, s(20));

            target[UiScaleKeys.ParserAssetsMargin] = new Thickness(s(32));
            target[UiScaleKeys.ParserAssetsGap] = new Thickness(s(16));
            target[UiScaleKeys.ParserStatusMargin] = new Thickness(0, s(16), 0, s(16));

            target[UiScaleKeys.ParserStatusSurfaceMaxWidth] = s(800);
            target[UiScaleKeys.ParserStatusSurfaceMargin] = new Thickness(s(128));
            target[UiScaleKeys.ParserStatusSurfacePadding] = new Thickness(s(32), s(4), s(32), s(4));
            target[UiScaleKeys.ParserStatusSurfaceRadius] = new CornerRadius(s(4));
            target[UiScaleKeys.ParserStatusFontSize] = fs(22);

            // Main
            target[UiScaleKeys.GameCoverRadius] = s(mediaRadius);
            target[UiScaleKeys.SplashLogoMaxHeight] = s(256);

            target[UiScaleKeys.NoShortcutsMargin] = new Thickness(0, 0, 0, s(96));
            target[UiScaleKeys.NoShortcutsFontSize] = fs(30);

            target[UiScaleKeys.ExitMessageFontSize] = fs(36);

            target[UiScaleKeys.AppInfoLabelMargin] = new Thickness(s(64));
            target[UiScaleKeys.AppInfoLabelFontSize] = fs(16);

            target[UiScaleKeys.TopPanelMargin] = new Thickness(0, s(24), 0, 0);
            target[UiScaleKeys.TopInfoTabsMargin] = new Thickness(s(72), 0, 0, 0);
            target[UiScaleKeys.TopInfoPrimaryFontSize] = fs(42);
            target[UiScaleKeys.TopInfoSecondaryFontSize] = fs(38);
            target[UiScaleKeys.UserAvatarSize] = s(50);

            // Selector
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

            target[UiScaleKeys.SelectorOverlayItemStrokeInset] = s(8);
            target[UiScaleKeys.SelectorOverlayItemStrokeOutset] = s(0);
            target[UiScaleKeys.SelectorOverlayItemStrokeThickness] = s(strokeThickness);
            target[UiScaleKeys.SelectorOverlayItemStrokeRadius] = s(8);
            target[UiScaleKeys.SelectorOverlayItemBackgroundInset] = s(16);
            target[UiScaleKeys.SelectorOverlayItemBackgroundOutset] = s(0);
            target[UiScaleKeys.SelectorOverlayItemBackgroundRadius] = s(4);

            target[UiScaleKeys.GameTitleMargin] = new Thickness(s(224), 0, 0, 0);
            target[UiScaleKeys.GameTitleFontSize] = fs(72);
            target[UiScaleKeys.LoadingProgressSize] = fs(48);

            // Pills
            target[UiScaleKeys.SourcePillWidth] = s(320);
            target[UiScaleKeys.SourcePillHeight] = s(64);
            target[UiScaleKeys.SourcePillRadius] = new CornerRadius(s(32));
            target[UiScaleKeys.SourcePillMargin] = new Thickness(0, s(72), 0, 0);
            target[UiScaleKeys.SourcePillFontSize] = fs(28);

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

            // Hints
            target[UiScaleKeys.HintPanelHeight] = s(64);
            target[UiScaleKeys.HintPanelMargin] = new Thickness(0, 0, s(96), s(64));
            target[UiScaleKeys.HintPanelPadding] = new Thickness(s(32), 0, s(32), 0);

            target[UiScaleKeys.HintBlockMargin] = new Thickness(s(16), 0, s(16), 0);
            target[UiScaleKeys.HintTextMargin] = new Thickness(s(16), 0, 0, 0);
            target[UiScaleKeys.HintIconHeightGamepad] = s(28);
            target[UiScaleKeys.HintIconHeightKeyboard] = s(26);
            target[UiScaleKeys.HintTextFontSize] = fs(24);
        }
    }

    // Resource keys used by DynamicResource for scaled UI values.
    public static class UiScaleKeys
    {
        // Shared
        public const string DividerThickness = "DividerThickness";
        public const string BackgroundTransitionSlide = "BackgroundTransitionSlide";

        // Overlay
        public const string OverlayWidth = "OverlayWidth";
        public const string OverlayRadius = "OverlayRadius";
        public const string OverlayPadding = "OverlayPadding";
        public const string OverlayPrimaryFontSize = "OverlayPrimaryFontSize";
        public const string OverlaySecondaryFontSize = "OverlaySecondaryFontSize";

        // Settings
        public const string SettingsOverlayLabelMargin = "SettingsOverlayLabelMargin";
        public const string SettingsOverlayLabelTreeMargin = "SettingsOverlayLabelTreeMargin";
        public const string SettingsOverlayDescFontSize = "SettingsOverlayDescFontSize";
        public const string SettingsOverlayItemPadding = "SettingsOverlayItemPadding";

        public const string SettingsOverlayTreeItemsContainerMargin = "SettingsOverlayTreeItemsContainerMargin";
        public const string SettingsOverlayTreeItemsContainerPadding = "SettingsOverlayTreeItemsContainerPadding";
        public const string SettingsOverlayTreeItemRadius = "SettingsOverlayTreeItemRadius";
        public const string SettingsOverlayTreeItemPadding = "SettingsOverlayTreeItemPadding";
        public const string SettingsOverlayTreeItemMargin = "SettingsOverlayTreeItemMargin";

        public const string SettingsOverlayDividerMargin = "SettingsOverlayDividerMargin";
        public const string SettingsOverlayAppInfoMargin = "SettingsOverlayAppInfoMargin";

        public const string ToggleSwitchWidth = "ToggleSwitchWidth";
        public const string ToggleSwitchHeight = "ToggleSwitchHeight";
        public const string CheckBoxSize = "CheckBoxSize";

        // Parser
        public const string ParserGameItemHeight = "ParserGameItemHeight";
        public const string ParserGamesViewportHeight = "ParserGamesViewportHeight";

        public const string ParserCoverSize = "ParserCoverSize";
        public const string ParserMediaRadius = "ParserMediaRadius";

        public const string ParserBackgroundWidth = "ParserBackgroundWidth";
        public const string ParserBackgroundHeight = "ParserBackgroundHeight";

        public const string ParserTabsMargin = "ParserTabsMargin";
        public const string ParserTabMargin = "ParserTabMargin";

        public const string ParserProgressHeight = "ParserProgressHeight";

        public const string ParserInputHeight = "ParserInputHeight";
        public const string ParserInputFontSize = "ParserInputFontSize";
        public const string ParserInputMargin = "ParserInputMargin";
        public const string ParserManualSearchHintMargin = "ParserManualSearchHintMargin";

        public const string ParserAssetsMargin = "ParserAssetsMargin";
        public const string ParserAssetsGap = "ParserAssetsGap";
        public const string ParserStatusMargin = "ParserStatusMargin";

        public const string ParserStatusSurfaceMaxWidth = "ParserStatusSurfaceMaxWidth";
        public const string ParserStatusSurfaceMargin = "ParserStatusSurfaceMargin";
        public const string ParserStatusSurfacePadding = "ParserStatusSurfacePadding";
        public const string ParserStatusSurfaceRadius = "ParserStatusSurfaceRadius";
        public const string ParserStatusFontSize = "ParserStatusFontSize";

        // Main
        public const string SplashLogoMaxHeight = "SplashLogoMaxHeight";

        public const string NoShortcutsMargin = "NoShortcutsMargin";
        public const string NoShortcutsFontSize = "NoShortcutsFontSize";

        public const string ExitMessageFontSize = "ExitMessageFontSize";

        public const string AppInfoLabelMargin = "AppInfoLabelMargin";
        public const string AppInfoLabelFontSize = "AppInfoLabelFontSize";

        public const string TopPanelMargin = "TopPanelMargin";
        public const string TopInfoTabsMargin = "TopInfoTabsMargin";
        public const string TopInfoPrimaryFontSize = "TopInfoPrimaryFontSize";
        public const string TopInfoSecondaryFontSize = "TopInfoSecondaryFontSize";
        public const string UserAvatarSize = "UserAvatarSize";

        public const string GameCoverRadius = "GameCoverRadius";

        public const string SelectorMediaStrokeInset = "SelectorMediaStrokeInset";
        public const string SelectorMediaStrokeOutset = "SelectorMediaStrokeOutset";
        public const string SelectorMediaStrokeThickness = "SelectorMediaStrokeThickness";
        public const string SelectorMediaStrokeRadius = "SelectorMediaStrokeRadius";
        public const string SelectorMediaBackgroundInset = "SelectorMediaBackgroundInset";
        public const string SelectorMediaBackgroundOutset = "SelectorMediaBackgroundOutset";
        public const string SelectorMediaBackgroundRadius = "SelectorMediaBackgroundRadius";

        public const string SelectorOverlayItemStrokeInset = "SelectorOverlayItemStrokeInset";
        public const string SelectorOverlayItemStrokeOutset = "SelectorOverlayItemStrokeOutset";
        public const string SelectorOverlayItemStrokeThickness = "SelectorOverlayItemStrokeThickness";
        public const string SelectorOverlayItemStrokeRadius = "SelectorOverlayItemStrokeRadius";
        public const string SelectorOverlayItemBackgroundInset = "SelectorOverlayItemBackgroundInset";
        public const string SelectorOverlayItemBackgroundOutset = "SelectorOverlayItemBackgroundOutset";
        public const string SelectorOverlayItemBackgroundRadius = "SelectorOverlayItemBackgroundRadius";

        public const string GameTitleMargin = "GameTitleMargin";
        public const string GameTitleFontSize = "GameTitleFontSize";
        public const string LoadingProgressSize = "LoadingProgressSize";

        // Cards
        public const string SourcePillWidth = "SourcePillWidth";
        public const string SourcePillHeight = "SourcePillHeight";
        public const string SourcePillRadius = "SourcePillRadius";
        public const string SourcePillMargin = "SourcePillMargin";
        public const string SourcePillFontSize = "SourcePillFontSize";

        public const string CounterPillHeight = "CounterPillHeight";
        public const string CounterPillPadding = "CounterPillPadding";
        public const string CounterPillMargin = "CounterPillMargin";
        public const string CounterPillIconMargin = "CounterPillIconMargin";
        public const string CounterPillFontSize = "CounterPillFontSize";
        public const string CounterPillIconSize = "CounterPillIconSize";

        // Notifications
        public const string NotificationPanelHeight = "NotificationPanelHeight";
        public const string NotificationPanelRadius = "NotificationPanelRadius";
        public const string NotificationPanelMaxWidth = "NotificationPanelMaxWidth";
        public const string NotificationPanelPadding = "NotificationPanelPadding";
        public const string NotificationPanelOuterMargin = "NotificationPanelOuterMargin";
        public const string NotificationPanelMargin = "NotificationPanelMargin";
        public const string NotificationPanelFontSize = "NotificationPanelFontSize";
        public const string NotificationPanelIconSize = "NotificationPanelIconSize";

        // Hints
        public const string HintPanelHeight = "HintPanelHeight";
        public const string HintPanelMargin = "HintPanelMargin";
        public const string HintPanelPadding = "HintPanelPadding";

        public const string HintBlockMargin = "HintBlockMargin";
        public const string HintTextMargin = "HintTextMargin";
        public const string HintIconHeightGamepad = "HintIconHeightGamepad";
        public const string HintIconHeightKeyboard = "HintIconHeightKeyboard";
        public const string HintTextFontSize = "HintTextFontSize";
    }
}