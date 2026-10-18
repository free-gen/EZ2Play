
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EZ2Play.App
{
    /// <summary>
    /// Displays application notifications with
    /// fade-in and fade-out animations.
    /// </summary>
    /// <remarks>
    /// Notifications are processed sequentially using
    /// an internal queue.
    ///
    /// Each notification completes its display sequence
    /// before the next queued notification begins.
    ///
    /// UI elements are provided through Initialize()
    /// after they become available in UIRegistry.
    /// </remarks>
    public class Notifications
    {
        // Default icon used when no specific icon is provided.
        private const string DefaultIcon = "\uE686";

        // Notification UI elements.
        private Border _NotificationPanel;
        private TextBlock _NotificationIcon;
        private TextBlock _NotificationText;

        // Sound manager used when a notification appears.
        private Sound _sound;

        // Queue of pending notification actions.
        private readonly Queue<Action> _queue = new Queue<Action>();

        // Indicates whether notification processing is active.
        private bool _running;

        /// <summary>
        /// Connects the notification manager to its
        /// visual elements and sound manager.
        /// </summary>
        public void Initialize(
            Border NotificationPanel,
            TextBlock NotificationIcon,
            TextBlock NotificationText,
            Sound sound)
        {
            _NotificationPanel = NotificationPanel;
            _NotificationIcon = NotificationIcon;
            _NotificationText = NotificationText;
            _sound = sound;
        }

        /// <summary>
        /// Adds a notification action to the queue
        /// and starts processing if the queue is idle.
        /// </summary>
        private void Enqueue(Action action)
        {
            _queue.Enqueue(action);

            if (!_running)
                ProcessNext();
        }

        /// <summary>
        /// Executes the next queued notification action.
        /// </summary>
        /// <remarks>
        /// The running flag remains active until the
        /// notification sequence explicitly completes.
        /// </remarks>
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

        /// <summary>
        /// Marks the current notification as completed
        /// and continues processing the queue.
        /// </summary>
        private void Done()
        {
            ProcessNext();
        }

        /// <summary>
        /// Displays a notification after an initial delay,
        /// keeps it visible for the specified duration,
        /// and then fades it out.
        /// </summary>
        /// <param name="text">
        /// Text displayed inside the notification.
        /// </param>
        /// <param name="delaySeconds">
        /// Delay before the fade-in animation starts.
        /// </param>
        /// <param name="displaySeconds">
        /// Time between completion of fade-in
        /// and the beginning of fade-out.
        /// </param>
        /// <param name="icon">
        /// Optional icon glyph. Uses DefaultIcon when omitted.
        /// </param>
        /// <param name="onComplete">
        /// Callback invoked after the notification disappears.
        /// </param>
        private void Show(
            string text,
            double delaySeconds,
            double displaySeconds,
            string icon = null,
            Action onComplete = null)
        {
            // Allow queue processing to continue if the
            // notification UI has not been initialized.
            if (_NotificationPanel == null ||
                _NotificationIcon == null ||
                _NotificationText == null)
            {
                onComplete?.Invoke();
                return;
            }

            // Prepare the notification in its hidden state.
            _NotificationIcon.Text = string.IsNullOrEmpty(icon)
                ? DefaultIcon
                : icon;

            _NotificationText.Text = text;
            _NotificationPanel.Visibility = Visibility.Visible;
            _NotificationPanel.Opacity = 0;

            // Fade out the notification and complete its queue entry.
            void FadeOut()
            {
                var anim = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(500),
                    EasingFunction = new CubicEase
                    {
                        EasingMode = EasingMode.EaseIn
                    }
                };

                anim.Completed += (s, e) =>
                {
                    _NotificationPanel.Visibility = Visibility.Collapsed;
                    onComplete?.Invoke();
                };

                _NotificationPanel.BeginAnimation(
                    UIElement.OpacityProperty,
                    anim);
            }

            // Play the notification sound and animate its appearance.
            void FadeIn()
            {
                _sound?.PlayEventSound();

                var anim = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(500),
                    EasingFunction = new CubicEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                };

                anim.Completed += (s, e) =>
                {
                    // Start the display timer only after
                    // the fade-in animation has completed.
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

                _NotificationPanel.BeginAnimation(
                    UIElement.OpacityProperty,
                    anim);
            }

            // Delay the initial appearance of the notification.
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

        /// <summary>
        /// Queues a notification indicating that the
        /// application is running a debug build.
        /// </summary>
        /// <remarks>
        /// Calls to this method are included only when
        /// the DEBUG compilation symbol is defined.
        /// </remarks>
        [System.Diagnostics.Conditional("DEBUG")]
        public void Debug(double delaySeconds, double displaySeconds)
        {
            Enqueue(() => Show(
                Locals.GetString("MessageDebugBuild"),
                delaySeconds,
                displaySeconds,
                "\uE7B2",
                Done));
        }

        /// <summary>
        /// Queues a notification about a hot-swap event.
        /// </summary>
        public void HotSwap(double delaySeconds, double displaySeconds)
        {
            Enqueue(() => Show(
                Locals.GetString("MessageHotSwap"),
                delaySeconds,
                displaySeconds,
                "\uE5A2",
                Done));
        }

        /// <summary>
        /// Queues a notification about a connected gamepad.
        /// Includes the device name on a separate line.
        /// </summary>
        public void HotPlug(
            double delaySeconds,
            double displaySeconds,
            string deviceName)
        {
            Enqueue(() =>
            {
                string msg =
                    $"{Locals.GetString("MessagePlugGamepad")}\n{deviceName}";

                Show(
                    msg,
                    delaySeconds,
                    displaySeconds,
                    "\uE314",
                    Done);
            });
        }

        /// <summary>
        /// Queues a warning about possible conflicts
        /// with an installed Xbox Game Bar.
        /// </summary>
        /// <remarks>
        /// The caller checks installation before requesting
        /// this notification. Guide handling remains active
        /// regardless of the detection result.
        /// </remarks>
        public void GameBarWarning(
            double delaySeconds,
            double displaySeconds)
        {
            Enqueue(() => Show(
                Locals.GetString("MessageGameBarWarning"),
                delaySeconds,
                displaySeconds,
                "\uE927",
                Done));
        }

        /// <summary>
        /// Queues an error notification containing
        /// the specified message text.
        /// </summary>
        public void Error(
            string text,
            double delaySeconds,
            double displaySeconds)
        {
            Enqueue(() => Show(
                text,
                delaySeconds,
                displaySeconds,
                "\uE91B",
                Done));
        }
    }
}
