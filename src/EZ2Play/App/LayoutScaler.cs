using System;
using System.Windows;

namespace EZ2Play.App
{
    public static class LayoutScaler
    {
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

            // Заставка
            target[UiScaleKeys.SplashLogoMaxHeight] = s(256); // высота логотипа

            // Сообщение NoShortcuts
            target[UiScaleKeys.NoShortcutsMargin] = new Thickness(0, 0, 0, s(100)); // отступ снизу
            target[UiScaleKeys.NoShortcutsFontSize] = fs(36); // размер шрифта

            // Сообщение при выходе
            target[UiScaleKeys.ExitMessageFontSize] = fs(42); // размер шрифта

            // Номер версии
            target[UiScaleKeys.AppInfoLabelMargin] = new Thickness( s(50) ); // отступы
            target[UiScaleKeys.AppInfoLabelFontSize] = fs(18); // размер шрифта

            // ===========================================================================

            // Верхняя панель
            target[UiScaleKeys.TopInfoPanelMargin] = new Thickness(s(0), s(25), s(0), 0); // общее смещение вниз
            target[UiScaleKeys.TopInfoTabsMargin] = new Thickness(s(70), s(0), s(0), 0);
            target[UiScaleKeys.TopInfoPrimalyFontSize] = fs(42);
            target[UiScaleKeys.TopInfoSecondaryFontSize] = fs(38);
            target[UiScaleKeys.UserAvatarSize] = s(64); // аватар

            // Уведомление
            double sysMsgHeight = 100;
            target[UiScaleKeys.SystemMessageHeight] = s(sysMsgHeight);
            target[UiScaleKeys.SystemMessageCornerRadius] = new CornerRadius(s(sysMsgHeight / 2)); 
            target[UiScaleKeys.SystemMessageMaxWidth] = s(1280); // макс ширина уведомления
            target[UiScaleKeys.SystemMessagePadding] = new Thickness(s(30), 0, s(50), 0); // внутр. оступы
            target[UiScaleKeys.SystemMessageOuterMargin] = new Thickness(s(0), s(0), s(70), 0); // внешний отступ
            target[UiScaleKeys.SystemMessageMargin] = new Thickness(s(0), 0, s(20), 0);
            target[UiScaleKeys.SystemMessageFontSize] = fs(22);
            target[UiScaleKeys.SystemMessageIconSize] = fs(42);

            // Витрина обложек
            target[UiScaleKeys.ItemCornerRadius] = s(23); // радиус обложек
            target[UiScaleKeys.SelectorThickness] = s(4); // толщина селектора
            target[UiScaleKeys.SelectorSpacing] = s(4); // оступ от обложки

            // Название игры
            target[UiScaleKeys.GameTitleMargin] = new Thickness(s(200), 0, s(0), 0);
            target[UiScaleKeys.SelectedGameTitleFontSize] = fs(72); // размер шрифта
            target[UiScaleKeys.LoadingProgressScale] = fs(55); // размер LoadingProgress

            // Карточка источника 
            double sourceCardHeight = 70;
            target[UiScaleKeys.SourceCardWidth] = s(320);
            target[UiScaleKeys.SourceCardHeight] = s(sourceCardHeight);
            target[UiScaleKeys.SourceCardCornerRadius] = new CornerRadius(s(sourceCardHeight / 2));
            target[UiScaleKeys.SourceCardPadding] = new Thickness(s(100), 0, s(100), 0); // внутр. отступы
            target[UiScaleKeys.BaseCardThickness] = new Thickness(s(2)); // радиусы 
            target[UiScaleKeys.SourceCardMargin] = new Thickness(0, s(50), 0, 0); // верхний отступ
            target[UiScaleKeys.SourceCardFontSize] = fs(28); // размер шрифта

            // Счетчик времени
            double counterCardHeight = 60;
            target[UiScaleKeys.CounterCardHeight] = s(counterCardHeight);
            target[UiScaleKeys.CounterCardCornerRadius] = new CornerRadius(s(counterCardHeight / 2));
            target[UiScaleKeys.CounterCardPadding] = new Thickness(s(30), 0, s(30), 0);
            target[UiScaleKeys.CounterCardThickness] = new Thickness(s(2));
            target[UiScaleKeys.CounterCardMargin] = new Thickness(s(50), 0, 0, 0);
            target[UiScaleKeys.CounterCardIconMargin] = new Thickness(0, 0, s(10), 0);
            target[UiScaleKeys.CounterCardFontSize] = fs(20);
            target[UiScaleKeys.CounterCardIconSize] = fs(24);

            // Нижняя панель (подсказки)
            double bottomPanelHeight = 90;
            target[UiScaleKeys.BottomPanelHeight] = s(bottomPanelHeight);
            target[UiScaleKeys.BottomPanelCornerRadius] = new CornerRadius(s(bottomPanelHeight / 2));
            target[UiScaleKeys.BottomPanelPadding] = new Thickness(s(50), 0, s(50), 0); // внутр. отступы
            target[UiScaleKeys.BottomPanelBorderThickness] = new Thickness(s(2)); // обводка
            target[UiScaleKeys.HintBlockMargin] = new Thickness(s(30), 0, s(30), 0); // отступы между элементами
            target[UiScaleKeys.HintTextFontSize] = fs(24); // размер шрифта
            target[UiScaleKeys.HintTextMargin] = new Thickness(s(15), 0, 0, 0); // отступ от иконок
            target[UiScaleKeys.HintIconHeightGamepad] = s(30); // высота иконки Pad
            target[UiScaleKeys.HintIconHeightKeyboard] = s(32); // высота иконки Key
        }
    }

    //  Ключи ресурсов для масштабируемых значений (DynamicResource). 
    public static class UiScaleKeys
    {
        // ======================== Заставка ========================
        public const string SplashLogoMaxHeight = "SplashLogoMaxHeight";

        // ======================== Сообщение NoShortcuts ========================
        public const string NoShortcutsMargin = "NoShortcutsMargin";
        public const string NoShortcutsFontSize = "NoShortcutsFontSize";

        // ======================== Сообщение при выходе ========================
        public const string ExitMessageFontSize = "ExitMessageFontSize";

        // ======================== Номер версии ========================
        public const string AppInfoLabelMargin = "AppInfoLabelMargin";
        public const string AppInfoLabelFontSize = "AppInfoLabelFontSize";

        // ======================== Верхняя панель ========================
        public const string TopInfoPanelMargin = "TopInfoPanelMargin";
        public const string TopInfoTabsMargin = "TopInfoTabsMargin";
        public const string TopInfoPrimalyFontSize = "TopInfoPrimalyFontSize";
        public const string TopInfoSecondaryFontSize = "TopInfoSecondaryFontSize";
        public const string UserAvatarSize = "UserAvatarSize";

        // ======================== Системные уведомления ========================
        public const string SystemMessageHeight = "SystemMessageHeight";
        public const string SystemMessageMaxWidth = "SystemMessageMaxWidth";
        public const string SystemMessageCornerRadius = "SystemMessageCornerRadius";
        public const string SystemMessagePadding = "SystemMessagePadding";
        public const string SystemMessageOuterMargin = "SystemMessageOuterMargin";
        public const string SystemMessageMargin = "SystemMessageMargin";
        public const string SystemMessageFontSize = "SystemMessageFontSize";
        public const string SystemMessageIconSize = "SystemMessageIconSize";

        // ======================== Витрина обложек ========================
        public const string ItemCornerRadius = "ItemCornerRadius";
        public const string SelectorThickness = "SelectorThickness";
        public const string SelectorSpacing = "SelectorSpacing";

        // ======================== Название игры ========================
        public const string GameTitleMargin = "GameTitleMargin";
        public const string SelectedGameTitleFontSize = "SelectedGameTitleFontSize";
        public const string LoadingProgressScale = "LoadingProgressScale";

        // ======================== Карточка источника ========================
        public const string SourceCardWidth = "SourceCardWidth";
        public const string SourceCardHeight = "SourceCardHeight";
        public const string SourceCardPadding = "SourceCardPadding";
        public const string SourceCardCornerRadius = "SourceCardCornerRadius";
        public const string BaseCardThickness = "BaseCardThickness";
        public const string SourceCardMargin = "SourceCardMargin";
        public const string SourceCardFontSize = "SourceCardFontSize";

        // ======================== Счетчик времени ========================
        public const string CounterCardHeight = "CounterCardHeight";
        public const string CounterCardPadding = "CounterCardPadding";
        public const string CounterCardCornerRadius = "CounterCardCornerRadius";
        public const string CounterCardThickness = "CounterCardThickness";
        public const string CounterCardMargin = "CounterCardMargin";
        public const string CounterCardIconMargin = "CounterCardIconMargin";
        public const string CounterCardFontSize = "CounterCardFontSize";
        public const string CounterCardIconSize = "CounterCardIconSize";

        // ======================== Нижняя панель (подсказки) ========================
        public const string BottomPanelHeight = "BottomPanelHeight";
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
