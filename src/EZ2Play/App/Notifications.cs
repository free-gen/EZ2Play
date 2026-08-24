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
        private readonly UIRegistry _ui;

        private const string DefaultIcon = "\uE686";

        private Border _NotificationPanel;
        private TextBlock _NotificationIcon;
        private TextBlock _NotificationText;

        private readonly Queue<Action> _queue = new Queue<Action>();
        private bool _running;

        public Notifications(UIRegistry uiRegistry)
        {
            _ui = uiRegistry;
        }

        // Initialize UI elements after they are resolved by UIRegistry.
        public void Initialize(Border NotificationPanel, TextBlock NotificationIcon, TextBlock NotificationText)
        {
            _NotificationPanel = NotificationPanel;
            _NotificationIcon = NotificationIcon;
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

        // Show a notification with delayed fade-in and fade-out animation.
        private void Show(string text, double delaySeconds, double displaySeconds, string icon = null, Action onComplete = null)
        {
            if (_NotificationPanel == null || _NotificationIcon == null || _NotificationText == null)
            {
                onComplete?.Invoke();
                return;
            }

            _NotificationIcon.Text = string.IsNullOrEmpty(icon) ? DefaultIcon : icon;
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

        [System.Diagnostics.Conditional("DEBUG")]
        public void Debug(double delaySeconds, double displaySeconds)
        {
            Enqueue(() => Show(Locals.GetString("MessageDebugBuild"), delaySeconds, displaySeconds, "\uE91A", Done));
        }

        public void HotSwap(double delaySeconds, double displaySeconds)
        {
            Enqueue(() => Show(Locals.GetString("MessageHotSwap"), delaySeconds, displaySeconds, "\uE5A2", Done));
        }

        public void HotPlug(double delaySeconds, double displaySeconds, string deviceName)
        {
            Enqueue(() =>
            {
                string msg = $"{Locals.GetString("MessagePlugGamepad")}\n{deviceName}";
                Show(msg, delaySeconds, displaySeconds, "\uE314", Done);
            });
        }

        public void GameBar(double delaySeconds, double displaySeconds, bool gameBarInstalled)
        {
            Enqueue(() =>
            {
                string msg = Locals.GetString(gameBarInstalled ? "MessageGameBarDetected" : "MessageGameBarNotDetected");
                Show(msg, delaySeconds, displaySeconds, "\uE927", Done);
            });
        }
    }
}