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
        // Вызывается из MainWindow (this.Resources) и из SplashScreen (Application.Current.Resources).
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
            target[UiScaleKeys.VersionLabelMargin] = new Thickness( s(50) ); // отступы
            target[UiScaleKeys.VersionLabelFontSize] = fs(16); // размер шрифта

            // ===========================================================================

            // Верхняя панель
            target[UiScaleKeys.TopInfoPanelMargin] = new Thickness(s(0), s(25), s(0), 0); // общее смещение вниз
            target[UiScaleKeys.TopInfoFontSize] = fs(42); // размер шрифта
            target[UiScaleKeys.UserAvatarSize] = s(50); // аватар

            // Уведомление
            target[UiScaleKeys.SystemMessageHeight] = s(100); // высота уведомления
            target[UiScaleKeys.SystemMessageMaxWidth] = s(1280); // макс ширина уведомления
            target[UiScaleKeys.SystemMessageCornerRadius] = new CornerRadius(s(20)); // макс ширина уведомления
            target[UiScaleKeys.SystemMessagePadding] = new Thickness(s(25), 0, s(25), 0); // внутр. оступы
            target[UiScaleKeys.SystemMessageOuterMargin] = new Thickness(s(0), s(20), s(400), 0); // внешний отступ
            target[UiScaleKeys.SystemMessageMargin] = new Thickness(s(0), 0, s(25), 0);
            target[UiScaleKeys.SystemMessageFontSize] = fs(22);
            target[UiScaleKeys.SystemMessageIconSize] = fs(50);

            // Витрина обложек
            target[UiScaleKeys.ItemCornerRadius] = s(20); // радиус обложек
            target[UiScaleKeys.SelectorThickness] = s(4); // толщина селектора
            target[UiScaleKeys.SelectorSpacing] = s(4); // оступ от обложки

            // Название игры
            target[UiScaleKeys.SelectedGameTitleFontSize] = fs(72); // размер шрифта
            target[UiScaleKeys.LoadingProgressScale] = fs(55); // размер LoadingProgress

            // Карточка источника 
            target[UiScaleKeys.SourceCardHeight] = s(56); // высота
            target[UiScaleKeys.SourceCardPadding] = new Thickness(s(50), 0, s(50), 0); // внутр. отступы
            target[UiScaleKeys.SourceCardCornerRadius] = new CornerRadius(s(28)); // радиусы 
            target[UiScaleKeys.SourceCardMargin] = new Thickness(0, s(50), 0, 0); // верхний отступ
            target[UiScaleKeys.SourceCardFontSize] = fs(24); // размер шрифта
            target[UiScaleKeys.SourceCardLineHeight] = s(28); // выравнивание по вертикали

            // Нижняя панель (подсказки)
            target[UiScaleKeys.BottomPanelHeight] = s(90); // высота
            target[UiScaleKeys.BottomPanelPadding] = new Thickness(s(50), 0, s(50), 0); // внутр. отступы
            target[UiScaleKeys.BottomPanelCornerRadius] = new CornerRadius(s(45)); // радиусы
            target[UiScaleKeys.BottomPanelBorderThickness] = new Thickness(s(2), s(2), s(2), 2); // обводка
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
        public const string VersionLabelMargin = "VersionLabelMargin";
        public const string VersionLabelFontSize = "VersionLabelFontSize";

        // ======================== Верхняя панель ========================
        public const string TopInfoPanelMargin = "TopInfoPanelMargin";
        public const string TopInfoFontSize = "TopInfoFontSize";
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
        public const string SelectedGameTitleFontSize = "SelectedGameTitleFontSize";
        public const string LoadingProgressScale = "LoadingProgressScale";

        // ======================== Карточка источника ========================
        public const string SourceCardHeight = "SourceCardHeight";
        public const string SourceCardPadding = "SourceCardPadding";
        public const string SourceCardCornerRadius = "SourceCardCornerRadius";
        public const string SourceCardMargin = "SourceCardMargin";
        public const string SourceCardFontSize = "SourceCardFontSize";
        public const string SourceCardLineHeight = "SourceCardLineHeight";

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
