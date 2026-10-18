
using System;
using System.Windows.Media.Animation;

namespace EZ2Play.App
{
    /// <summary>
    /// Creates opacity animations used by application overlays.
    /// </summary>
    /// <remarks>
    /// Provides shared fade-in and fade-out animation
    /// configuration without controlling overlay lifecycle.
    ///
    /// The calling overlay remains responsible for starting
    /// the animation and handling its completion.
    ///
    /// Animation durations are supplied by the caller
    /// to preserve each overlay's existing configuration.
    /// </remarks>
    internal static class OverlayAnimations
    {
        /// <summary>
        /// Creates an animation that fades an overlay
        /// from fully transparent to fully opaque.
        /// </summary>
        /// <param name="durationSeconds">
        /// Animation duration in seconds.
        /// </param>
        /// <returns>
        /// A new opacity animation using cubic ease-out.
        /// </returns>
        public static DoubleAnimation CreateFadeIn(
            double durationSeconds)
        {
            return new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };
        }

        /// <summary>
        /// Creates an animation that fades an overlay
        /// from its current opacity to fully transparent.
        /// </summary>
        /// <param name="fromOpacity">
        /// Opacity at the beginning of the animation.
        /// </param>
        /// <param name="durationSeconds">
        /// Animation duration in seconds.
        /// </param>
        /// <returns>
        /// A new opacity animation using cubic ease-in.
        /// </returns>
        public static DoubleAnimation CreateFadeOut(
            double fromOpacity,
            double durationSeconds)
        {
            return new DoubleAnimation
            {
                From = fromOpacity,
                To = 0,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseIn
                }
            };
        }
    }
}
