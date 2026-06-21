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

        //  Универсальное масштабирование: baseValue * factor. 
        public static double Scale(double baseValue, double actualWindowHeight)
        {
            return baseValue * GetScaleFactor(actualWindowHeight);
        }

        public static double GetScaledFontSize(double baseFontSize, double actualWindowHeight)
        {
            double size = Scale(baseFontSize, actualWindowHeight);
            return Math.Max(MinFontSize, Math.Min(MaxFontSize, size));
        }

        // Заполняет указанный ResourceDictionary масштабированными значениями для всех UiScaleKeys.
        public static void ApplyUiScaleToDictionary(ResourceDictionary target, double windowHeight)
        {
            if (target == null) return;
            double s(double baseVal) => Scale(baseVal, windowHeight);
            double fs(double baseVal) => GetScaledFontSize(baseVal, windowHeight);

            // Base
            target[UiScaleKeys.BaseCornerRadius] = new CornerRadius(s(8));
            
            // SettingsOverlay
            target[UiScaleKeys.SettingsOverlayWidth] = s(1280);
            target[UiScaleKeys.SettingsOverlayTreeItemWidth] = s(1280 / 3);
            target[UiScaleKeys.SettingsOverlayThickness] = new Thickness(s(2));
            target[UiScaleKeys.SettingsOverlayCornerRadius] = new CornerRadius(s(16));

            // SettingsOverlayItems
            target[UiScaleKeys.SettingsOverlayLabelFontSize] = fs(32);
            target[UiScaleKeys.SettingsOverlayLabelTreeFontSize] = fs(24);
            target[UiScaleKeys.SettingsOverlayLabelMargin] = new Thickness(0, 0, 0, s(8));
            target[UiScaleKeys.SettingsOverlayLabelTreeMargin] = new Thickness(0, 0, 0, s(4));
            target[UiScaleKeys.SettingsOverlayDescFontSize] = fs(20);
            target[UiScaleKeys.SettingsOverlayItemPadding] = new Thickness(0, s(32), 0, s(32));

            // SettingsOverlaySelection
            target[UiScaleKeys.SettingsOverlaySelectionBorderMargin] = new Thickness(s(6), s(8), s(6), s(10));
            target[UiScaleKeys.SettingsOverlaySelectionBorderThickness] = new Thickness(s(4));
            target[UiScaleKeys.SettingsOverlaySelectionBackgroundMargin] = new Thickness(s(6));
            target[UiScaleKeys.SettingsOverlaySelectionCornerRadius] = new CornerRadius(s(10));
            target[UiScaleKeys.SettingsOverlaySelectionBgCornerRadius] = new CornerRadius(s(6));

            // SettingsOverlayDivider
            target[UiScaleKeys.SettingsOverlayDividerMargin] = new Thickness(0);
            target[UiScaleKeys.SettingsOverlayDividerHeight] = s(2);

            // SettingsOverlayMisc
            target[UiScaleKeys.SettingsOverlayAppInfoMargin] = new Thickness(0, s(24), 0, s(24));
            target[UiScaleKeys.ToggleSwitchWidth] = s(64);
            target[UiScaleKeys.ToggleSwitchHeight] = s(32);
            
            
            // SplashScreen
            target[UiScaleKeys.SplashLogoMaxHeight] = s(256);

            // NoShortcutsMessage
            target[UiScaleKeys.NoShortcutsMargin] = new Thickness(0, 0, 0, s(96));
            target[UiScaleKeys.NoShortcutsFontSize] = fs(36);

            // ExitOverlay
            target[UiScaleKeys.ExitMessageFontSize] = fs(42);

            // AppInfoLabel
            target[UiScaleKeys.AppInfoLabelMargin] = new Thickness(s(64));
            target[UiScaleKeys.AppInfoLabelFontSize] = fs(16);

            // TopPanel
            target[UiScaleKeys.TopPanelMargin] = new Thickness(0, s(24), 0, 0);
            target[UiScaleKeys.TopInfoTabsMargin] = new Thickness(s(72), 0, 0, 0);
            target[UiScaleKeys.TopInfoPrimalyFontSize] = fs(42);
            target[UiScaleKeys.TopInfoSecondaryFontSize] = fs(38);
            target[UiScaleKeys.UserAvatarSize] = s(56);

            // NotificationPanel
            target[UiScaleKeys.NotificationPanelHeight] = s(96);
            target[UiScaleKeys.NotificationPanelCornerRadius] = new CornerRadius(s(16)); 
            target[UiScaleKeys.NotificationPanelMaxWidth] = s(1024);
            target[UiScaleKeys.NotificationPanelPadding] = new Thickness(s(32), 0, s(32), 0);
            target[UiScaleKeys.NotificationPanelOuterMargin] = new Thickness(0, 0, s(32), 0);
            target[UiScaleKeys.NotificationPanelMargin] = new Thickness(0, 0, s(24), 0);
            target[UiScaleKeys.NotificationPanelFontSize] = fs(22);
            target[UiScaleKeys.NotificationPanelIconSize] = fs(32);

            // ItemsListBox
            target[UiScaleKeys.ItemCornerRadius] = s(12);
            target[UiScaleKeys.SelectorThickness] = s(4);
            target[UiScaleKeys.SelectorSpacing] = s(4);

            // SelectedGameTitle
            target[UiScaleKeys.GameTitleMargin] = new Thickness(s(192), 0, 0, 0);
            target[UiScaleKeys.SelectedGameTitleFontSize] = fs(72);

            // GameSourceCard
            target[UiScaleKeys.SourceCardWidth] = s(320);
            target[UiScaleKeys.SourceCardHeight] = s(64);
            target[UiScaleKeys.SourceCardCornerRadius] = new CornerRadius(s(16));
            target[UiScaleKeys.SourceCardPadding] = new Thickness(s(96), 0, s(96), 0);
            target[UiScaleKeys.BaseCardThickness] = new Thickness(s(2));
            target[UiScaleKeys.SourceCardMargin] = new Thickness(0, s(72), 0, 0);
            target[UiScaleKeys.SourceCardFontSize] = fs(28);

            // GameCounterCard
            target[UiScaleKeys.CounterCardHeight] = s(64);
            target[UiScaleKeys.CounterCardCornerRadius] = new CornerRadius(s(16));
            target[UiScaleKeys.CounterCardPadding] = new Thickness(s(32), 0, s(32), 0);
            target[UiScaleKeys.CounterCardThickness] = new Thickness(s(2));
            target[UiScaleKeys.CounterCardMargin] = new Thickness(s(48), 0, 0, 0);
            target[UiScaleKeys.CounterCardIconMargin] = new Thickness(0, 0, s(8), 0);
            target[UiScaleKeys.CounterCardFontSize] = fs(20);
            target[UiScaleKeys.CounterCardIconSize] = fs(22);

            // LoadingProgress
            target[UiScaleKeys.LoadingProgressScale] = fs(42);

            // BottomPanel
            target[UiScaleKeys.BottomPanelHeight] = s(64);
            target[UiScaleKeys.BottomPanelCornerRadius] = new CornerRadius(s(16));
            target[UiScaleKeys.BottomPanelMargin] = new Thickness(0, 0, s(96), s(64));
            target[UiScaleKeys.BottomPanelPadding] = new Thickness(s(8), 0, s(8), 0);
            target[UiScaleKeys.BottomPanelBorderThickness] = new Thickness(s(2));
            target[UiScaleKeys.HintBlockMargin] = new Thickness(s(16), 0, s(16), 0);
            target[UiScaleKeys.HintTextMargin] = new Thickness(s(16), 0, 0, 0);
            target[UiScaleKeys.HintIconHeightGamepad] = s(28);
            target[UiScaleKeys.HintIconHeightKeyboard] = s(26);
            target[UiScaleKeys.HintTextFontSize] = fs(24);
        }
    }

    //  Ключи ресурсов для масштабируемых значений (DynamicResource). 
    public static class UiScaleKeys
    {
        public const string BaseCornerRadius = "BaseCornerRadius";

        public const string SettingsOverlayWidth = "SettingsOverlayWidth";
        public const string SettingsOverlayTreeItemWidth = "SettingsOverlayTreeItemWidth";
        public const string SettingsOverlayThickness = "SettingsOverlayThickness";
        public const string SettingsOverlayLabelFontSize = "SettingsOverlayLabelFontSize";
        public const string SettingsOverlayLabelTreeFontSize = "SettingsOverlayLabelTreeFontSize";
        public const string SettingsOverlayLabelMargin = "SettingsOverlayLabelMargin";
        public const string SettingsOverlayLabelTreeMargin = "SettingsOverlayLabelTreeMargin";
        public const string SettingsOverlayDescFontSize = "SettingsOverlayDescFontSize";
        public const string SettingsOverlayItemPadding = "SettingsOverlayItemPadding";
        public const string SettingsOverlaySelectionBorderMargin = "SettingsOverlaySelectionBorderMargin";
        public const string SettingsOverlaySelectionBorderThickness = "SettingsOverlaySelectionBorderThickness";
        public const string SettingsOverlaySelectionBackgroundMargin = "SettingsOverlaySelectionBackgroundMargin";
        public const string SettingsOverlayCornerRadius = "SettingsOverlayCornerRadius";
        public const string SettingsOverlaySelectionCornerRadius = "SettingsOverlaySelectionCornerRadius";
        public const string SettingsOverlaySelectionBgCornerRadius = "SettingsOverlaySelectionBgCornerRadius";
        public const string SettingsOverlayDividerMargin = "SettingsOverlayDividerMargin";
        public const string SettingsOverlayDividerHeight = "SettingsOverlayDividerHeight";
        public const string SettingsOverlayAppInfoMargin = "SettingsOverlayAppInfoMargin";

        public const string ToggleSwitchWidth = "ToggleSwitchWidth";
        public const string ToggleSwitchHeight = "ToggleSwitchHeight";

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

        public const string NotificationPanelHeight = "NotificationPanelHeight";
        public const string NotificationPanelMaxWidth = "NotificationPanelMaxWidth";
        public const string NotificationPanelCornerRadius = "NotificationPanelCornerRadius";
        public const string NotificationPanelPadding = "NotificationPanelPadding";
        public const string NotificationPanelOuterMargin = "NotificationPanelOuterMargin";
        public const string NotificationPanelMargin = "NotificationPanelMargin";
        public const string NotificationPanelFontSize = "NotificationPanelFontSize";
        public const string NotificationPanelIconSize = "NotificationPanelIconSize";

        public const string ItemCornerRadius = "ItemCornerRadius";
        public const string SelectorThickness = "SelectorThickness";
        public const string SelectorSpacing = "SelectorSpacing";

        public const string GameTitleMargin = "GameTitleMargin";
        public const string SelectedGameTitleFontSize = "SelectedGameTitleFontSize";
        public const string LoadingProgressScale = "LoadingProgressScale";

        public const string SourceCardWidth = "SourceCardWidth";
        public const string SourceCardHeight = "SourceCardHeight";
        public const string SourceCardPadding = "SourceCardPadding";
        public const string SourceCardCornerRadius = "SourceCardCornerRadius";
        public const string BaseCardThickness = "BaseCardThickness";
        public const string SourceCardMargin = "SourceCardMargin";
        public const string SourceCardFontSize = "SourceCardFontSize";

        public const string CounterCardHeight = "CounterCardHeight";
        public const string CounterCardPadding = "CounterCardPadding";
        public const string CounterCardCornerRadius = "CounterCardCornerRadius";
        public const string CounterCardThickness = "CounterCardThickness";
        public const string CounterCardMargin = "CounterCardMargin";
        public const string CounterCardIconMargin = "CounterCardIconMargin";
        public const string CounterCardFontSize = "CounterCardFontSize";
        public const string CounterCardIconSize = "CounterCardIconSize";

        public const string BottomPanelHeight = "BottomPanelHeight";
        public const string BottomPanelMargin = "BottomPanelMargin";
        public const string BottomPanelPadding = "BottomPanelPadding";
        public const string BottomPanelCornerRadius = "BottomPanelCornerRadius";
        public const string BottomPanelBorderThickness = "BottomPanelBorderThickness";
        public const string HintBlockMargin = "HintBlockMargin";
        public const string HintTextFontSize = "HintTextFontSize";
        public const string HintTextMargin = "HintTextMargin";
        public const string HintIconHeightGamepad = "HintIconHeightGamepad";
        public const string HintIconHeightKeyboard = "HintIconHeightKeyboard";
    }
}
