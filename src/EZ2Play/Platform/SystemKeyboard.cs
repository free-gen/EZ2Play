
using System;
using System.Globalization;
using System.Windows.Input;
using Windows.UI.ViewManagement.Core;

namespace EZ2Play.App
{
    /// <summary>
    /// Provides Windows input language and system keyboard operations.
    /// </summary>
    /// <remarks>
    /// SystemProvider remains the public entry point for these operations.
    /// This class preserves the original CoreInputView calls, fallback
    /// behavior, and exception handling.
    /// </remarks>
    internal static class SystemKeyboard
    {
        /// <summary>
        /// Switches to an available English input language and returns
        /// the language that was active before the switch.
        /// </summary>
        /// <returns>
        /// The previous input language, or null if an exception occurs.
        /// </returns>
        internal static CultureInfo ForceEnglishInputLanguage()
        {
            try
            {
                var manager = InputLanguageManager.Current;

                var previousLanguage =
                    manager.CurrentInputLanguage;

                if (previousLanguage != null &&
                    string.Equals(
                        previousLanguage.TwoLetterISOLanguageName,
                        "en",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return previousLanguage;
                }

                foreach (CultureInfo language in
                    manager.AvailableInputLanguages)
                {
                    if (string.Equals(
                        language.TwoLetterISOLanguageName,
                        "en",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        manager.CurrentInputLanguage = language;

                        return previousLanguage;
                    }
                }

                return previousLanguage;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Restores the previously active input language, if supplied.
        /// </summary>
        internal static void RestoreInputLanguage(CultureInfo language)
        {
            if (language == null)
                return;

            try
            {
                InputLanguageManager.Current.CurrentInputLanguage =
                    language;
            }
            catch
            {
            }
        }

        /// <summary>
        /// Performs the original system keyboard warm-up operation.
        /// </summary>
        /// <remarks>
        /// The existing operation calls TryHide; it does not show
        /// or pre-create a keyboard window.
        /// </remarks>
        internal static void WarmUpSystemKeyboard()
        {
            try
            {
                CoreInputView.GetForCurrentView()?.TryHide();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Attempts to show the gamepad keyboard, falling back to
        /// the standard keyboard when the first request fails.
        /// </summary>
        internal static void ShowGamepadKeyboard()
        {
            try
            {
                var inputView = CoreInputView.GetForCurrentView();

                if (inputView == null)
                    return;

                try
                {
                    if (inputView.TryShow(CoreInputViewKind.Gamepad))
                        return;
                }
                catch (Exception ex)
                {
                    DebugLog.Error(
                        "Keyboard",
                        ex,
                        "Gamepad keyboard failed.");
                }

                inputView.TryShow(CoreInputViewKind.Keyboard);
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "Keyboard",
                    ex,
                    "Failed to show system keyboard.");
            }
        }

        /// <summary>
        /// Requests that the current Windows system keyboard be hidden.
        /// </summary>
        internal static void HideSystemKeyboard()
        {
            try
            {
                CoreInputView.GetForCurrentView()?.TryHide();
            }
            catch
            {
            }
        }
    }
}
