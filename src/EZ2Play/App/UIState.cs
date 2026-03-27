using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Linq;
using Microsoft.Win32;
using System.Security.Principal;
using System.Windows.Media.Effects;

namespace EZ2Play.App
{
    // --------------- Класс управления состоянием UI-элементов ---------------

    public class UIState
    {
        // --------------- Свойства UI-элементов ---------------

        // Верхняя панель информации
        public TextBlock TopLeftAppName { get; set; }
        public TextBlock TopRightTime { get; set; }
        public Notifications Notification { get; private set; }
        public Grid TopInfoPanel { get; set; }

        // Элементы пользователя
        public Image UserAvatar { get; set; }

        // Нижняя панель
        public Border BottomPanel { get; set; }

        // Сообщения и заголовки
        public TextBlock NoShortcutsMessage { get; set; }
        public TextBlock SelectedGameTitle { get; set; }
        public TextBlock VersionLabel { get; set; }
        public TextBlock ExitMessageText { get; set; }

        // Карточки и контейнеры
        public Border GameSourceCard { get; set; }
        public Grid MainScreenGrid { get; set; }
        public Grid SplashOverlay { get; set; }
        public Image SplashLogo { get; set; }

        // Системные уведомления
        public Border SystemMessage { get; set; }
        public TextBlock SystemMessageText { get; set; }

        // Текст компании
        public System.Windows.Documents.Run CompanyNameRun { get; set; }

        // Иконки подсказок
        public FrameworkElement LaunchIconXinput { get; set; }
        public FrameworkElement LaunchIconKeyboard { get; set; }
        public FrameworkElement ExitIconGamepad { get; set; }
        public FrameworkElement ExitIconKeyboard { get; set; }
        public FrameworkElement ScreenSwapIconGamepad { get; set; }
        public FrameworkElement ScreenSwapIconKeyboard { get; set; }

        // ListBox для выхода
        public ListBox ItemsListBox { get; set; }

        // Фон
        public Image BackgroundImage { get; set; }

        // --------------- Поля класса ---------------

        private DispatcherTimer _clockTimer;
        private string _appDisplayName;
        private bool UseImageBackground => BackgroundImage?.Source != null;
        private ParticlesCanvas _particlesCanvas;

        // --------------- Конструктор ---------------

        public UIState() 
        {
            Notification = new Notifications(this);
        }

        // --------------- Инициализация ---------------

        // Инициализирует информацию в правом верхнем углу (время, название).
        public void InitializeToInfoPanel()
        {
            _appDisplayName = AppInfo.Name;
            UpdateTopInfoPanel();

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => UpdateTopInfoPanel();
            _clockTimer.Start();
        }

        // --------------- Обновление UI ---------------

        // Обновляет панель информации (время и название приложения).
        public void UpdateTopInfoPanel()
        {
            if (TopLeftAppName != null && TopRightTime != null)
            {
                TopLeftAppName.Text = $"{AppInfo.Name} Launcher";
                TopRightTime.Text = DateTime.Now.ToString("HH:mm");
            }
        }

        // Загружает и отображает изображение пользователя.
        public void UserImage()
        {
            if (UserAvatar == null) return;

            try
            {
                string path = null;
                var sid = WindowsIdentity.GetCurrent()?.User?.Value;

                if (!string.IsNullOrEmpty(sid))
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(
                        $@"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\{sid}"))
                    {
                        path = key?.GetValue("Image192") as string;
                    }
                }

                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    path = System.IO.Path.Combine(@"C:\ProgramData\Microsoft\User Account Pictures", "user-192.png");

                if (!File.Exists(path))
                {
                    UserAvatar.Visibility = Visibility.Collapsed;
                    return;
                }

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                UserAvatar.Source = bmp;
                UserAvatar.Visibility = Visibility.Visible;

                // Обрезка аватара в круг
                UserAvatar.Loaded += (s, e) =>
                {
                    var r = UserAvatar.ActualWidth / 2;
                    if (r > 0) UserAvatar.Clip = new EllipseGeometry(new Point(r, r), r, r);
                };

                UserAvatar.SizeChanged += (s, e) =>
                {
                    var r = UserAvatar.ActualWidth / 2;
                    if (r > 0) UserAvatar.Clip = new EllipseGeometry(new Point(r, r), r, r);
                };
            }
            catch
            {
                UserAvatar.Visibility = Visibility.Collapsed;
            }
        }

        public void SetEmptyState(bool isEmpty)
        {
            // Скрываем или показываем весь основной экран
            if (MainScreenGrid != null)
                MainScreenGrid.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;

            // Сообщение об отсутствии ярлыков показываем отдельно
            if (NoShortcutsMessage != null)
                NoShortcutsMessage.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        // --------------- Экран выхода ---------------

        public void ShowExitOverlay()
        {
            // Скрываем основной экран целиком
            if (MainScreenGrid != null)
                MainScreenGrid.Visibility = Visibility.Collapsed;

            // Скрываем сообщение об отсутствии ярлыков, если оно было видно
            if (NoShortcutsMessage != null)
                NoShortcutsMessage.Visibility = Visibility.Collapsed;

            // Устанавливаем имя компании
            if (CompanyNameRun != null)
                CompanyNameRun.Text = AppInfo.Company;

            // Номер версии
            if (VersionLabel != null)
                VersionLabel.Visibility = Visibility.Visible;

            // Показываем сообщение о выходе с анимацией
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

        // Запускает последовательность сплеш-экрана: лого → вызов onComplete.
        public void RunSplashSequence(Action onComplete)
        {
            if (SplashLogo == null)
            {
                onComplete?.Invoke();
                return;
            }

            TryLoadLogoInto(SplashLogo);

            void AnimateIn(UIElement el, int durationMs, Action after)
            {
                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                anim.Completed += (s, e) => after?.Invoke();
                el.BeginAnimation(UIElement.OpacityProperty, anim);
            }

            void AnimateOut(UIElement el, int durationMs, Action after)
            {
                var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(durationMs))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                anim.Completed += (s, e) => after?.Invoke();
                el.BeginAnimation(UIElement.OpacityProperty, anim);
            }

            void Delay(int ms, Action after)
            {
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
                t.Tick += (s, e) => { t.Stop(); after?.Invoke(); };
                t.Start();
            }

            Delay(500, () =>
                AnimateIn(SplashLogo, 1000, () =>
                    Delay(500, () =>
                        AnimateOut(SplashLogo, 500, () =>
                            Delay(1000, () => onComplete?.Invoke())))));
        }

        // Показывает окно с анимацией, опционально пропуская сплеш.
        public void ShowWithAnimation(bool skipSplash, Action onAfterSplash)
        {
            // Сразу спрятать базовый UI и сплеш
            if (MainScreenGrid != null) MainScreenGrid.Visibility = Visibility.Collapsed;
            if (SplashOverlay != null) SplashOverlay.Visibility = Visibility.Collapsed;

            // Показываем окно
            var window = Application.Current?.MainWindow;
            if (window != null)
                window.Visibility = Visibility.Visible;

            if (skipSplash)
            {
                if (window != null)
                    window.Opacity = 1.0;
                onAfterSplash?.Invoke();
                return;
            }

            if (window != null)
                window.Opacity = 0.0;

            if (SplashOverlay != null)
                SplashOverlay.Visibility = Visibility.Visible;

            var animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(800),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, e) =>
            {
                RunSplashSequence(() =>
                {
                    if (SplashOverlay != null)
                        SplashOverlay.Visibility = Visibility.Hidden;
                    onAfterSplash?.Invoke();
                });
            };

            if (window != null)
                window.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        // --------------- Системные уведомления ---------------

        // Показывает уведомление с общей логикой появления/исчезновения
        private void ShowNotification(string text, double delaySeconds, double displaySeconds)
        {
            if (SystemMessage == null || SystemMessageText == null)
                return;

            SystemMessageText.Text = text;

            SystemMessage.Visibility = Visibility.Visible;
            SystemMessage.Opacity = 0;

            void FadeIn()
            {
                var anim = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(500),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                anim.Completed += (s, e) =>
                {
                    var displayTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(displaySeconds)
                    };

                    displayTimer.Tick += (s2, e2) =>
                    {
                        displayTimer.Stop();
                        FadeOut();
                    };

                    displayTimer.Start();
                };

                SystemMessage.BeginAnimation(UIElement.OpacityProperty, anim);
            }

            void FadeOut()
            {
                var anim = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(500),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                anim.Completed += (s, e) =>
                {
                    SystemMessage.Visibility = Visibility.Collapsed;
                };

                SystemMessage.BeginAnimation(UIElement.OpacityProperty, anim);
            }

            var delayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(delaySeconds)
            };

            delayTimer.Tick += (s, e) =>
            {
                delayTimer.Stop();
                FadeIn();
            };

            delayTimer.Start();
        }

        // --------------- Вложенный класс для уведомлений ---------------

        public class Notifications
        {
            private readonly UIState _uiState;

            public Notifications(UIState uiState)
            {
                _uiState = uiState;
            }

            // Отладочное уведомление
            public void Debug(double delaySeconds, double displaySeconds)
            {
                _uiState.ShowNotification(Locals.GetString("MessageTest"), delaySeconds, displaySeconds);
            }

            // Уведомление о HotSwap режиме
            public void HotSwap(double delaySeconds, double displaySeconds)
            {
                _uiState.ShowNotification(Locals.GetString("MessageHotSwap"), delaySeconds, displaySeconds);
            }

            // Уведомление о подключении устройства ввода
            public void HotPlug(double delaySeconds, double displaySeconds, string deviceName)
            {
                string mainMessage = Locals.GetString("MessagePlugGamepad");
                string fullMessage = $"{mainMessage}\n{deviceName}";
                _uiState.ShowNotification(fullMessage, delaySeconds, displaySeconds);
            }
        }
        
        // --------------- Иконки подсказок ---------------

        // Переключает иконки подсказок: геймпад или клавиатура.
        public void RefreshHintIcons(bool isGamepad)
        {
            var visGamepad = isGamepad ? Visibility.Visible : Visibility.Collapsed;
            var visKeyboard = isGamepad ? Visibility.Collapsed : Visibility.Visible;

            if (LaunchIconXinput != null) LaunchIconXinput.Visibility = visGamepad;
            if (LaunchIconKeyboard != null) LaunchIconKeyboard.Visibility = visKeyboard;
            if (ExitIconGamepad != null) ExitIconGamepad.Visibility = visGamepad;
            if (ExitIconKeyboard != null) ExitIconKeyboard.Visibility = visKeyboard;
            if (ScreenSwapIconGamepad != null) ScreenSwapIconGamepad.Visibility = visGamepad;
            if (ScreenSwapIconKeyboard != null) ScreenSwapIconKeyboard.Visibility = visKeyboard;
        }

        // --------------- Загрузка ресурсов ---------------

        // Пытается загрузить логотип в указанный Image элемент.
        private static bool TryLoadLogoInto(Image image)
        {
            if (image == null) return false;

            try
            {
                // Попытка загрузить из ui.pack
                var logoFromPack = PackLoader.LoadFromPack("logo.png");
                if (logoFromPack != null)
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = logoFromPack;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    image.Source = bitmap;
                    RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                    return true;
                }
            }
            catch { }

            try
            {
                // Фолбек на ресурс WPF
                var uri = new Uri("pack://application:,,,/Assets/logo.png", UriKind.Absolute);
                var bitmap = new BitmapImage(uri);
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                image.Source = bitmap;
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Загрузка фонового изображения вместо частиц
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

                    // BackgroundImage.Effect = new BlurEffect
                    // {
                    //     Radius = 0,
                    //     RenderingBias = RenderingBias.Performance
                    // };

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

        // Показать фон с анимацией
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

        // Освобождает ресурсы (останавливает таймеры).
        public void Dispose()
        {
            try
            {
                _clockTimer?.Stop();
            }
            catch { }
        }
    }
}