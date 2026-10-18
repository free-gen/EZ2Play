
using System;
using System.Windows.Threading;
using EZ2Play.App;

namespace EZ2Play
{
    /// <summary>
    /// Handles changes in application activity and window activation.
    /// </summary>
    /// <remarks>
    /// Uses the state and dependencies owned by MainWindow.xaml.cs.
    /// The activity checks and the order of music, cursor, background,
    /// and playtime operations are preserved from the original class.
    /// </remarks>
    public partial class MainWindow
    {
        /// <summary>
        /// Applies an activity change only when the main screen is active,
        /// the application is not exiting, and the state has changed.
        /// </summary>
        private void UpdateAppActivity(bool isActive)
        {
            if (_isExiting || !_isMainScreenActive || isActive == _wasActive)
                return;

            DebugLog.Write(
                "Activity",
                $"State: {_wasActive} -> {isActive}, " +
                $"foreground={SystemProvider.IsForeground()}");

            _wasActive = isActive;

            if (isActive)
                OnBecameActive();
            else
                OnBecameInactive();
        }

        /// <summary>
        /// Restores audio, cursor, background, and playtime state when
        /// the application becomes active.
        /// </summary>
        private void OnBecameActive()
        {
            _sound.PlayBackgroundMusic(Sound.FadeDurationMs * 3);
            SystemProvider.HideCursor();
            _backgroundController.Show(true);

            _metadata.Stop();
            UpdatePlaytimeUI();

            if (_tabsController.IsLastPlayed)
            {
                _launcher.SortByLastPlayed();
            }
        }

        /// <summary>
        /// Suspends background music and hides the main-screen background
        /// when the application becomes inactive.
        /// </summary>
        private void OnBecameInactive()
        {
            _sound.StopBackgroundMusicSafe(Sound.FadeDurationMs);
            _uiRegistry.ShowLoading(false);
            SystemProvider.ShowCursor();
            _backgroundController.Show(false);
        }

        /// <summary>
        /// Refreshes scale-dependent resources and the selector after
        /// the window is activated.
        /// </summary>
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            DebugLog.Write(
                "Activity",
                $"OnActivated: foreground={SystemProvider.IsForeground()}, " +
                $"active={_wasActive}");

            if (ActualHeight > 0)
            {
                UpdateUiScaleResources(ActualHeight);
                Dispatcher.BeginInvoke(new Action(RefreshActiveSelector), DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// Resets the pending foreground activation candidate.
        /// Inactivity is confirmed by foreground polling before
        /// background visuals and music are suspended.
        /// </summary>
        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);

            DebugLog.Write(
                "Activity",
                $"OnDeactivated: foreground={SystemProvider.IsForeground()}, " +
                $"active={_wasActive}");

            _foregroundCandidateSince = DateTime.MinValue;
        }
    }
}
