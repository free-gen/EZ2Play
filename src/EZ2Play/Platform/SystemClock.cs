
using System;
using System.Windows.Threading;

namespace EZ2Play.App
{
    /// <summary>
    /// Provides periodic clock updates and current time
    /// formatting for the application.
    /// </summary>
    /// <remarks>
    /// Uses the WPF DispatcherTimer to execute clock
    /// callbacks on the associated dispatcher thread.
    ///
    /// SystemProvider remains the public entry point
    /// for starting, stopping, and reading the clock.
    ///
    /// The original timer interval, callback behavior,
    /// and time formatting are preserved.
    /// </remarks>
    internal static class SystemClock
    {
        // Timer responsible for periodic clock updates.
        private static DispatcherTimer _clockTimer;

        /// <summary>
        /// Starts updating the application clock
        /// at one-second intervals.
        /// </summary>
        /// <param name="onTimeChanged">
        /// Callback receiving the current formatted time.
        /// </param>
        /// <remarks>
        /// Creates a new DispatcherTimer and stores
        /// its reference in the shared timer field.
        ///
        /// The callback is invoked on each timer tick.
        /// No immediate callback is performed on startup.
        /// </remarks>
        internal static void StartClock(Action<string> onTimeChanged)
        {
            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _clockTimer.Tick += (s, e) =>
                onTimeChanged?.Invoke(GetCurrentTime());

            _clockTimer.Start();
        }

        /// <summary>
        /// Stops the currently registered clock timer
        /// and clears its reference.
        /// </summary>
        internal static void StopClock()
        {
            _clockTimer?.Stop();
            _clockTimer = null;
        }

        /// <summary>
        /// Returns the current local time in 24-hour format.
        /// </summary>
        /// <returns>
        /// Current time formatted as HH:mm.
        /// </returns>
        internal static string GetCurrentTime()
        {
            return DateTime.Now.ToString("HH:mm");
        }
    }
}
