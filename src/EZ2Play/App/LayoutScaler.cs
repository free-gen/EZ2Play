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
            target[UiScaleKeys.BaseCornerRadius] = new CornerRadius(s(8));

            // Overlay
            target[UiScaleKeys.OverlayWidth] = s(1280);
            target[UiScaleKeys.OverlayBorderThickness] = new Thickness(s(2));
            target[UiScaleKeys.OverlayCornerRadius] = new CornerRadius(s(16));
            target[UiScaleKeys.OverlayPadding] = new Thickness(s(16));
            target[UiScaleKeys.OverlayPrimaryFontSize] = fs(32);
            target[UiScaleKeys.OverlaySecondaryFontSize] = fs(24);

            target[UiScaleKeys.OverlaySelectionBorderMargin] = new Thickness(s(8));
            target[UiScaleKeys.OverlaySelectionBorderThickness] = new Thickness(s(4));
            target[UiScaleKeys.OverlaySelectionBackgroundMargin] = new Thickness(s(6));
            target[UiScaleKeys.OverlaySelectionCornerRadius] = new CornerRadius(s(10));
            target[UiScaleKeys.OverlaySelectionBackgroundCornerRadius] = new CornerRadius(s(6));

            // Settings
            target[UiScaleKeys.SettingsOverlayLabelMargin] = new Thickness(0, 0, 0, s(8));
            target[UiScaleKeys.SettingsOverlayLabelTreeMargin] = new Thickness(0, 0, 0, s(4));
            target[UiScaleKeys.SettingsOverlayDescFontSize] = fs(22);
            target[UiScaleKeys.SettingsOverlayItemPadding] = new Thickness(0, s(32), 0, s(32));

            target[UiScaleKeys.SettingsOverlayTreeItemsContainerMargin] = new Thickness(0, s(-8), 0, 0);
            target[UiScaleKeys.SettingsOverlayTreeItemsContainerPadding] = new Thickness(s(16), 0, s(16), s(8));
            target[UiScaleKeys.SettingsOverlayTreeItemPadding] = new Thickness(0, s(8), 0, s(8));
            target[UiScaleKeys.SettingsOverlayTreeItemMargin] = new Thickness(s(16));

            target[UiScaleKeys.SettingsOverlayDividerMargin] = new Thickness(0);
            target[UiScaleKeys.SettingsOverlayDividerHeight] = s(2);
            target[UiScaleKeys.SettingsOverlayAppInfoMargin] = new Thickness(0, s(24), 0, s(24));

            target[UiScaleKeys.ToggleSwitchWidth] = s(64);
            target[UiScaleKeys.ToggleSwitchHeight] = s(32);
            target[UiScaleKeys.CheckBox] = s(64);

            // Parser
            const double parserGameItemHeight = 128;
            const double parserCoverSize = 256;
            const double parserCoverMargin = 4;
            const double parserCoverBorder = 4;
            const double parserBackgroundWidth = 584;
            const double parserBackgroundHeight = 189;

            const int parserVisibleGameRows = 5;
            const int parserVisibleCoverRows = 2;
            const int parserVisibleBackgroundRows = 3;

            double parserCoverCellSize = parserCoverSize + parserCoverMargin * 4 + parserCoverBorder * 2;
            double parserBackgroundCellHeight = parserBackgroundHeight + parserCoverMargin * 4 + parserCoverBorder * 2;

            target[UiScaleKeys.ParserGameItemHeight] = s(parserGameItemHeight);
            target[UiScaleKeys.ParserGamesViewportHeight] = s(parserGameItemHeight * parserVisibleGameRows);

            target[UiScaleKeys.ParserCoverSize] = s(parserCoverSize);
            target[UiScaleKeys.ParserCoversViewportHeight] = s(parserCoverCellSize * parserVisibleCoverRows);
            target[UiScaleKeys.ParserCoverRadius] = s(12);
            target[UiScaleKeys.ParserCoverBorderRadius] = new CornerRadius(s(16));
            target[UiScaleKeys.ParserCoverMargin] = new Thickness(s(parserCoverMargin));

            target[UiScaleKeys.ParserBackgroundWidth] = s(parserBackgroundWidth);
            target[UiScaleKeys.ParserBackgroundHeight] = s(parserBackgroundHeight);
            target[UiScaleKeys.ParserBackgroundsViewportHeight] = s(parserBackgroundCellHeight * parserVisibleBackgroundRows);

            target[UiScaleKeys.ParserTabsMargin] = new Thickness(0, 0, 0, s(16));
            target[UiScaleKeys.ParserTabMargin] = new Thickness(s(24), 0, s(24), 0);

            target[UiScaleKeys.ParserSelectionMargin] = new Thickness(s(0));

            target[UiScaleKeys.ParserProgressWidth] = s(2560);
            target[UiScaleKeys.ParserProgressHeight] = s(8);

            target[UiScaleKeys.ParserInputHeight] = s(72);
            target[UiScaleKeys.ParserInputFontSize] = fs(40);
            target[UiScaleKeys.ParserManualSearchHintMargin] = new Thickness(0, s(8), 0, s(24));

            // Main
            target[UiScaleKeys.SplashLogoMaxHeight] = s(256);

            target[UiScaleKeys.NoShortcutsMargin] = new Thickness(0, 0, 0, s(96));
            target[UiScaleKeys.NoShortcutsFontSize] = fs(36);

            target[UiScaleKeys.ExitMessageFontSize] = fs(42);

            target[UiScaleKeys.AppInfoLabelMargin] = new Thickness(s(64));
            target[UiScaleKeys.AppInfoLabelFontSize] = fs(16);

            target[UiScaleKeys.TopPanelMargin] = new Thickness(0, s(24), 0, 0);
            target[UiScaleKeys.TopInfoTabsMargin] = new Thickness(s(72), 0, 0, 0);
            target[UiScaleKeys.TopInfoPrimalyFontSize] = fs(42);
            target[UiScaleKeys.TopInfoSecondaryFontSize] = fs(38);
            target[UiScaleKeys.UserAvatarSize] = s(56);

            target[UiScaleKeys.ItemCornerRadius] = s(12);
            target[UiScaleKeys.SelectorThickness] = s(4);
            target[UiScaleKeys.SelectorSpacing] = s(4);

            target[UiScaleKeys.GameTitleMargin] = new Thickness(s(224), 0, 0, 0);
            target[UiScaleKeys.SelectedGameTitleFontSize] = fs(72);
            target[UiScaleKeys.LoadingProgressScale] = fs(42);

            // Cards
            target[UiScaleKeys.SourceCardWidth] = s(320);
            target[UiScaleKeys.SourceCardHeight] = s(64);
            target[UiScaleKeys.BaseCardThickness] = new Thickness(s(2));
            target[UiScaleKeys.SourceCardMargin] = new Thickness(0, s(72), 0, 0);
            target[UiScaleKeys.SourceCardFontSize] = fs(28);

            target[UiScaleKeys.CounterCardHeight] = s(64);
            target[UiScaleKeys.CounterCardPadding] = new Thickness(s(32), 0, s(32), 0);
            target[UiScaleKeys.CounterCardThickness] = new Thickness(s(2));
            target[UiScaleKeys.CounterCardMargin] = new Thickness(s(48), 0, 0, 0);
            target[UiScaleKeys.CounterCardIconMargin] = new Thickness(0, 0, s(8), 0);
            target[UiScaleKeys.CounterCardFontSize] = fs(20);
            target[UiScaleKeys.CounterCardIconSize] = fs(22);

            // Notifications
            target[UiScaleKeys.NotificationPanelHeight] = s(96);
            target[UiScaleKeys.NotificationPanelMaxWidth] = s(1024);
            target[UiScaleKeys.NotificationPanelPadding] = new Thickness(s(32), 0, s(32), 0);
            target[UiScaleKeys.NotificationPanelOuterMargin] = new Thickness(0, 0, s(32), 0);
            target[UiScaleKeys.NotificationPanelMargin] = new Thickness(0, 0, s(24), 0);
            target[UiScaleKeys.NotificationPanelFontSize] = fs(22);
            target[UiScaleKeys.NotificationPanelIconSize] = fs(32);

            // Hints
            target[UiScaleKeys.HintPanelHeight] = s(64);
            target[UiScaleKeys.HintPanelMargin] = new Thickness(0, 0, s(96), s(64));
            target[UiScaleKeys.HintPanelPadding] = new Thickness(s(8), 0, s(8), 0);

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

        public const string BaseCornerRadius = "BaseCornerRadius";

        // Overlay
        public const string OverlayWidth = "OverlayWidth";
        public const string OverlayBorderThickness = "OverlayBorderThickness";
        public const string OverlayCornerRadius = "OverlayCornerRadius";
        public const string OverlayPadding = "OverlayPadding";
        public const string OverlayPrimaryFontSize = "OverlayPrimaryFontSize";
        public const string OverlaySecondaryFontSize = "OverlaySecondaryFontSize";

        public const string OverlaySelectionBorderMargin = "OverlaySelectionBorderMargin";
        public const string OverlaySelectionBorderThickness = "OverlaySelectionBorderThickness";
        public const string OverlaySelectionBackgroundMargin = "OverlaySelectionBackgroundMargin";
        public const string OverlaySelectionCornerRadius = "OverlaySelectionCornerRadius";
        public const string OverlaySelectionBackgroundCornerRadius = "OverlaySelectionBackgroundCornerRadius";

        // Settings
        public const string SettingsOverlayLabelMargin = "SettingsOverlayLabelMargin";
        public const string SettingsOverlayLabelTreeMargin = "SettingsOverlayLabelTreeMargin";
        public const string SettingsOverlayDescFontSize = "SettingsOverlayDescFontSize";
        public const string SettingsOverlayItemPadding = "SettingsOverlayItemPadding";

        public const string SettingsOverlayTreeItemsContainerMargin = "SettingsOverlayTreeItemsContainerMargin";
        public const string SettingsOverlayTreeItemsContainerPadding = "SettingsOverlayTreeItemsContainerPadding";
        public const string SettingsOverlayTreeItemPadding = "SettingsOverlayTreeItemPadding";
        public const string SettingsOverlayTreeItemMargin = "SettingsOverlayTreeItemMargin";

        public const string SettingsOverlayDividerMargin = "SettingsOverlayDividerMargin";
        public const string SettingsOverlayDividerHeight = "SettingsOverlayDividerHeight";
        public const string SettingsOverlayAppInfoMargin = "SettingsOverlayAppInfoMargin";

        public const string ToggleSwitchWidth = "ToggleSwitchWidth";
        public const string ToggleSwitchHeight = "ToggleSwitchHeight";
        public const string CheckBox = "CheckBox";

        // Parser
        public const string ParserGameItemHeight = "ParserGameItemHeight";
        public const string ParserGamesViewportHeight = "ParserGamesViewportHeight";

        public const string ParserCoverSize = "ParserCoverSize";
        public const string ParserCoversViewportHeight = "ParserCoversViewportHeight";
        public const string ParserCoverRadius = "ParserCoverRadius";
        public const string ParserCoverBorderRadius = "ParserCoverBorderRadius";
        public const string ParserCoverMargin = "ParserCoverMargin";

        public const string ParserBackgroundWidth = "ParserBackgroundWidth";
        public const string ParserBackgroundHeight = "ParserBackgroundHeight";
        public const string ParserBackgroundsViewportHeight = "ParserBackgroundsViewportHeight";

        public const string ParserTabsMargin = "ParserTabsMargin";
        public const string ParserTabMargin = "ParserTabMargin";

        public const string ParserSelectionMargin = "ParserSelectionMargin";

        public const string ParserProgressWidth = "ParserProgressWidth";
        public const string ParserProgressHeight = "ParserProgressHeight";

        public const string ParserInputHeight = "ParserInputHeight";
        public const string ParserInputFontSize = "ParserInputFontSize";
        public const string ParserManualSearchHintMargin = "ParserManualSearchHintMargin";

        // Main
        public const string SplashLogoMaxHeight = "SplashLogoMaxHeight";

        public const string NoShortcutsMargin = "NoShortcutsMargin";
        public const string NoShortcutsFontSize = "NoShortcutsFontSize";

        public const string ExitMessageFontSize = "ExitMessageFontSize";

        public const string AppInfoLabelMargin = "AppInfoLabelMargin";
        public const string AppInfoLabelFontSize = "AppInfoLabelFontSize";

        public const string TopPanelMargin = "TopPanelMargin";
        public const string TopInfoTabsMargin = "TopInfoTabsMargin";
        public const string TopInfoPrimalyFontSize = "TopInfoPrimalyFontSize";
        public const string TopInfoSecondaryFontSize = "TopInfoSecondaryFontSize";
        public const string UserAvatarSize = "UserAvatarSize";

        public const string ItemCornerRadius = "ItemCornerRadius";
        public const string SelectorThickness = "SelectorThickness";
        public const string SelectorSpacing = "SelectorSpacing";

        public const string GameTitleMargin = "GameTitleMargin";
        public const string SelectedGameTitleFontSize = "SelectedGameTitleFontSize";
        public const string LoadingProgressScale = "LoadingProgressScale";

        // Cards
        public const string SourceCardWidth = "SourceCardWidth";
        public const string SourceCardHeight = "SourceCardHeight";
        public const string BaseCardThickness = "BaseCardThickness";
        public const string SourceCardMargin = "SourceCardMargin";
        public const string SourceCardFontSize = "SourceCardFontSize";

        public const string CounterCardHeight = "CounterCardHeight";
        public const string CounterCardPadding = "CounterCardPadding";
        public const string CounterCardThickness = "CounterCardThickness";
        public const string CounterCardMargin = "CounterCardMargin";
        public const string CounterCardIconMargin = "CounterCardIconMargin";
        public const string CounterCardFontSize = "CounterCardFontSize";
        public const string CounterCardIconSize = "CounterCardIconSize";

        // Notifications
        public const string NotificationPanelHeight = "NotificationPanelHeight";
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