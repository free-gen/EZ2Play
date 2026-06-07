using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EZ2Play.App
{
    public class Notifications
    {
        private readonly UIState _ui;
        private Border _NotificationPanel;
        private TextBlock _NotificationText;

        // очередь уведомлений
        private readonly Queue<Action> _queue = new Queue<Action>();
        private bool _running;

        public Notifications(UIState uiState)
        {
            _ui = uiState;
        }

        // Инициализация UI-элементов (вызывается из UIState после того как они найдены)
        public void Initialize(Border NotificationPanel, TextBlock NotificationText)
        {
            _NotificationPanel = NotificationPanel;
            _NotificationText = NotificationText;
        }

        private void Enqueue(Action action)
        {
            _queue.Enqueue(action);
            if (!_running)
                ProcessNext();
        }

        private void ProcessNext()
        {
            if (_queue.Count == 0)
            {
                _running = false;
                return;
            }

            _running = true;
            var action = _queue.Dequeue();
            action.Invoke();
        }

        private void Done()
        {
            ProcessNext();
        }

        // Основной метод показа уведомления с анимацией
        private void Show(string text, double delaySeconds, double displaySeconds, Action onComplete = null)
        {
            if (_NotificationPanel == null || _NotificationText == null)
            {
                onComplete?.Invoke();
                return;
            }

            _NotificationText.Text = text;
            _NotificationPanel.Visibility = Visibility.Visible;
            _NotificationPanel.Opacity = 0;

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
                    _NotificationPanel.Visibility = Visibility.Collapsed;
                    onComplete?.Invoke();
                };

                _NotificationPanel.BeginAnimation(UIElement.OpacityProperty, anim);
            }

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
                    var t = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(displaySeconds)
                    };

                    t.Tick += (s2, e2) =>
                    {
                        t.Stop();
                        FadeOut();
                    };

                    t.Start();
                };

                _NotificationPanel.BeginAnimation(UIElement.OpacityProperty, anim);
            }

            var delay = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(delaySeconds)
            };

            delay.Tick += (s, e) =>
            {
                delay.Stop();
                FadeIn();
            };

            delay.Start();
        }

        // ====== Публичные методы уведомлений ======
        
        public void Debug(double delaySeconds, double displaySeconds)
        {
            Enqueue(() => Show(Locals.GetString("MessageTest"), delaySeconds, displaySeconds, Done));
        }

        public void HotSwap(double delaySeconds, double displaySeconds)
        {
            Enqueue(() => Show(Locals.GetString("MessageHotSwap"), delaySeconds, displaySeconds, Done));
        }

        public void HotPlug(double delaySeconds, double displaySeconds, string deviceName)
        {
            Enqueue(() =>
            {
                string msg = $"{Locals.GetString("MessagePlugGamepad")}\n{deviceName}";
                Show(msg, delaySeconds, displaySeconds, Done);
            });
        }

        public void GameBar(double delaySeconds, double displaySeconds, bool gameBarInstalled)
        {
            Enqueue(() =>
            {
                string msg = Locals.GetString(gameBarInstalled ? "MessageGameBarDetected" : "MessageGameBarNotDetected");
                Show(msg, delaySeconds, displaySeconds, Done);
            });
        }
    }
}