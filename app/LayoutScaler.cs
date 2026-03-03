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

            // ===========================================================================

            // Верхняя панель
            target[UiScaleKeys.TopInfoPanelMargin] = new Thickness(s(0), s(20), s(0), 0); // смещение вниз
            target[UiScaleKeys.TopInfoFontSize] = fs(42); // размер шрифта

            // Витрина обложек
            target[UiScaleKeys.CarouselItemCornerRadius] = s(20); // радиус обложек
            target[UiScaleKeys.CarouselSelectorOuterMargin] = new Thickness(s(-7), s(-7), s(-7), s(-7)); // отступ до селектора
            target[UiScaleKeys.CarouselSelectorCornerRadius] = new CornerRadius(s(24)); // радиус селектора
            target[UiScaleKeys.CarouselSelectorBorderThickness] = new Thickness(s(3)); // толщина селектора

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
            target[UiScaleKeys.BottomPanelBorderThickness] = new Thickness(s(1), s(2), s(1), 0); // обводка
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

        // ======================== Верхняя панель ========================
        public const string TopInfoPanelMargin = "TopInfoPanelMargin";
        public const string TopInfoFontSize = "TopInfoFontSize";

        // ======================== Витрина обложек ========================
        public const string CarouselItemCornerRadius = "CarouselItemCornerRadius";
        public const string CarouselSelectorOuterMargin = "CarouselSelectorOuterMargin";
        public const string CarouselSelectorCornerRadius = "CarouselSelectorCornerRadius";
        public const string CarouselSelectorBorderThickness = "CarouselSelectorBorderThickness";

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
