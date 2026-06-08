using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace EZ2Play.App
{
    // --------------- Класс управления состоянием UI-элементов ---------------

    public class UIRegistry
    {
        // --------------- Свойства UI-элементов ---------------

        public Image SplashLogo { get; set; }
        public Grid SplashOverlay { get; set; }
        public TextBlock NoShortcutsMessage { get; set; }
        public TextBlock ExitMessageText { get; set; }
        public TextBlock AppInfoLabel { get; set; }
        
        // Верхняя панель
        public Grid TopPanel { get; set; }
        public TextBlock TabGamelistText { get; set; }
        public TextBlock TabLastPlayedText { get; set; }
        public Border NotificationPanel { get; set; }
        public TextBlock NotificationText { get; set; }
        public Notifications Notifications { get; private set; }
        public Image UserAvatar { get; set; }
        public TextBlock TimeLabel { get; set; } 

        // Список игр
        public Border GameSourceCard { get; set; }
        public Grid MainScreenGrid { get; set; }
        public TextBlock SelectedGameTitle { get; set; }
        public Border GameCounterCard { get; set; }
        public TextBlock GameCounterText { get; set; }
        public ListBox ItemsListBox { get; set; }
        public Grid CarouselWrapper { get; set; }
        public Image BackgroundImage { get; set; }        
        
        // Иконки подсказок
        public Border BottomPanel { get; set; }
        public FrameworkElement IconGamepadLaunch { get; set; }
        public FrameworkElement IconGamepadExit { get; set; }
        public FrameworkElement IconGamepadSwap { get; set; }
        public FrameworkElement IconGamepadSort { get; set; }
        public FrameworkElement IconKeyboardLaunch { get; set; }
        public FrameworkElement IconKeyboardExit { get; set; }
        public FrameworkElement IconKeyboardSwap { get; set; }
        public FrameworkElement IconKeyboardSort { get; set; }

        // --------------- Поля класса ---------------

        private bool UseImageBackground => BackgroundImage?.Source != null;
        private SplashScreen _splash;
        private ParticlesCanvas _particlesCanvas;
        private Wpf.Ui.Controls.ProgressRing _loadingRing;

        // --------------- Конструктор ---------------

        public UIRegistry() 
        {
            Notifications = new Notifications(this);
        }

        // --------------- Инициализация ---------------

        // Инициализация заставки
        public void InitializeSplash(Image logo, Grid overlay, Grid mainScreen)
        {
            _splash = new SplashScreen(logo, overlay, mainScreen);
        }

        // Инициализация уведомлений
        public void InitializeNotifications(Border NotificationPanel, TextBlock NotificationText)
        {
            Notifications.Initialize(NotificationPanel, NotificationText);
        }

        // Инициализация времени
        public void InitializeClock()
        {
            // Устанавливаем время один раз сразу
            UpdateClockDisplay();
            
            // Запускаем таймер через SystemProvider
            SystemProvider.StartClock((time) =>
            {
                if (TimeLabel != null)
                    TimeLabel.Text = time;
            });
        }

        // Обновляет отображение времени (ручной вызов при необходимости)
        public void UpdateClockDisplay()
        {
            if (TimeLabel != null)
                TimeLabel.Text = SystemProvider.GetCurrentTime();
        }

        // Загружает и отображает аватар пользователя
        public void LoadUserAvatar()
        {
            if (UserAvatar == null) return;

            var avatar = SystemProvider.GetUserAvatar();
            
            if (avatar == null)
            {
                UserAvatar.Visibility = Visibility.Collapsed;
                return;
            }

            UserAvatar.Source = avatar;
            UserAvatar.Visibility = Visibility.Visible;

            // Подписываемся на события для обрезки в круг
            UserAvatar.Loaded += (s, e) => ClipAvatarToCircle();
            UserAvatar.SizeChanged += (s, e) => ClipAvatarToCircle();
        }

        // Обрезает аватар в круг
        private void ClipAvatarToCircle()
        {
            var r = UserAvatar.ActualWidth / 2;
            if (r > 0)
                UserAvatar.Clip = new EllipseGeometry(new Point(r, r), r, r);
        }

        // Показывает или скрывает основной экран в зависимости от наличия ярлыков
        public void SetEmptyState(bool isEmpty)
        {
            if (MainScreenGrid != null)
                MainScreenGrid.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;

            if (NoShortcutsMessage != null)
                NoShortcutsMessage.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        // --------------- Экран выхода ---------------

        public void ShowExitOverlay()
        {
            if (MainScreenGrid != null)
                MainScreenGrid.Visibility = Visibility.Collapsed;

            if (NoShortcutsMessage != null)
                NoShortcutsMessage.Visibility = Visibility.Collapsed;

            if (AppInfoLabel != null)
                AppInfoLabel.Visibility = Visibility.Visible;

            if (ExitMessageText != null)
            {
                ExitMessageText.Visibility = Visibility.Visible;

                var animation = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(800),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                ExitMessageText.BeginAnimation(UIElement.OpacityProperty, animation);
            }
        }

        // --------------- Сплеш-экран ---------------

        public void RunSplashSequence(Action onComplete)
        {
            _splash?.RunSequence(onComplete);
        }

        public void ShowWithAnimation(bool skipSplash, Action onAfterSplash)
        {
            _splash?.ShowWithAnimation(skipSplash, onAfterSplash);
        }

        // --------------- Счетчик времени ---------------

        public void UpdatePlaytimeDisplay(string text, bool visible)
        {
            if (GameCounterText != null)
                GameCounterText.Text = text;
            
            if (GameCounterCard != null)
                GameCounterCard.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        // --------------- Индикатор загрузки ---------------

        public void InitializeLoadingRing(Wpf.Ui.Controls.ProgressRing ring)
        {
            _loadingRing = ring;
        }

        public void ShowLoading(bool show)
        {
            if (_loadingRing != null)
                _loadingRing.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
        
        // --------------- Иконки подсказок ---------------

        public void RefreshHintIcons(bool isGamepad)
        {
            var visGamepad = isGamepad ? Visibility.Visible : Visibility.Collapsed;
            var visKeyboard = isGamepad ? Visibility.Collapsed : Visibility.Visible;

            if (IconGamepadLaunch != null) IconGamepadLaunch.Visibility = visGamepad;
            if (IconKeyboardLaunch != null) IconKeyboardLaunch.Visibility = visKeyboard;
            if (IconGamepadExit != null) IconGamepadExit.Visibility = visGamepad;
            if (IconKeyboardExit != null) IconKeyboardExit.Visibility = visKeyboard;
            if (IconGamepadSwap != null) IconGamepadSwap.Visibility = visGamepad;
            if (IconKeyboardSwap != null) IconKeyboardSwap.Visibility = visKeyboard;
            if (IconGamepadSort != null) IconGamepadSort.Visibility = visGamepad;
            if (IconKeyboardSort != null) IconKeyboardSort.Visibility = visKeyboard;
        }

        // --------------- Загрузка ресурсов ---------------

        public bool LoadBackgroundImage()
        {
            if (BackgroundImage == null) return false;

            try
            {
                var bgFromPack = PackLoader.LoadFromPack("bg.jpg") ?? PackLoader.LoadFromPack("bg.png");
                if (bgFromPack != null)
                {
                    var decoder = BitmapDecoder.Create(
                        bgFromPack,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);

                    var frame = decoder.Frames[0];

                    int targetHeight = 1080;
                    double scale = (double)targetHeight / frame.PixelHeight;

                    var small = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
                    small.Freeze();

                    BackgroundImage.Source = small;
                    BackgroundImage.Visibility = Visibility.Collapsed;
                    BackgroundImage.Opacity = 0;

                    RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.HighQuality);

                    return true;
                }
            }
            catch { }

            BackgroundImage.Source = null;
            BackgroundImage.Visibility = Visibility.Collapsed;

            return false;
        }

        public void ShowBackground(bool visible)
        {
            if (UseImageBackground)
            {
                var bgAnim = new DoubleAnimation
                {
                    To = visible ? 0.7 : 0,
                    Duration = TimeSpan.FromSeconds(0.5),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                bgAnim.Completed += (s, e) =>
                {
                    if (!visible)
                        BackgroundImage.Visibility = Visibility.Collapsed;
                };

                BackgroundImage.Visibility = Visibility.Visible;
                BackgroundImage.BeginAnimation(UIElement.OpacityProperty, bgAnim);
            }
            else
            {
                _particlesCanvas?.SetParticlesVisible(visible, true, 0.5);
            }
        }

        public void SetParticlesCanvas(ParticlesCanvas canvas)
        {
            _particlesCanvas = canvas;
        }

        // --------------- Очистка ресурсов ---------------

        public void Dispose()
        {
            SystemProvider.StopClock();
        }
    }
}