
using System;

namespace EZ2Play.App
{
    /// <summary>
    /// Routes keyboard and gamepad input events to the
    /// currently active application screen.
    /// </summary>
    /// <remarks>
    /// Raw input is provided by Input.
    /// This class translates input events into commands
    /// for the main screen, settings overlay, and parser overlay.
    /// </remarks>
    public class InputHandler
    {
        /// <summary>
        /// Defines the currently active input context.
        /// </summary>
        public enum InputMode
        {
            Main,
            Settings,
            Parser,
            Empty
        }

        private readonly Input _input;

        private InputMode _mode = InputMode.Main;
        private DateTime _lastStartBackTime;

        // Main screen navigation.
        public event Action<int> OnMoveSelection;

        // Settings navigation.
        public event Action<int> OnSettingsNavigate;
        public event Action<int> OnSettingsNavigateVertical;

        // Main screen actions.
        public event Action OnLaunchSelected;
        public event Action OnOpenSettings;
        public event Action OnSwitchToGamelist;
        public event Action OnSwitchToLastPlayed;

        // Settings actions.
        public event Action OnSettingsConfirm;
        public event Action OnSettingsBack;

        // Parser actions and navigation.
        public event Action OnOpenParser;
        public event Action OnParserSearch;
        public event Action OnParserConfirm;
        public event Action OnParserBack;
        public event Action<int> OnParserNavigateHorizontal;
        public event Action<int> OnParserNavigateVertical;
        public event Action<int> OnParserSwitchTab;

        public InputHandler(Input input)
        {
            _input = input;
            SubscribeEvents();
        }

        /// <summary>
        /// Changes the active input context.
        /// </summary>
        public void SetMode(InputMode mode)
        {
            _mode = mode;
        }

        /// <summary>
        /// Subscribes to raw input events and dispatches them
        /// according to the active input mode.
        /// </summary>
        /// <remarks>
        /// The routing order is intentional.
        /// Parser and Settings consume their assigned inputs
        /// before the main screen can handle them.
        /// </remarks>
        private void SubscribeEvents()
        {
            // Horizontal navigation.
            _input.OnLeftRight += dir =>
            {
                if (_mode == InputMode.Parser)
                    OnParserNavigateHorizontal?.Invoke(dir);
                else if (_mode == InputMode.Settings)
                    OnSettingsNavigate?.Invoke(dir);
                else if (_mode == InputMode.Main)
                    OnMoveSelection?.Invoke(dir);
            };

            // Vertical navigation is used by overlays.
            _input.OnUpDown += dir =>
            {
                if (_mode == InputMode.Parser)
                    OnParserNavigateVertical?.Invoke(dir);
                else if (_mode == InputMode.Settings)
                    OnSettingsNavigateVertical?.Invoke(dir);
            };

            // Primary confirmation action.
            _input.OnA += () =>
            {
                if (_mode == InputMode.Parser)
                    OnParserConfirm?.Invoke();
                else if (_mode == InputMode.Settings)
                    OnSettingsConfirm?.Invoke();
                else if (_mode == InputMode.Main)
                    OnLaunchSelected?.Invoke();
            };

            // Secondary back action.
            _input.OnB += () =>
            {
                if (_mode == InputMode.Parser)
                    OnParserBack?.Invoke();
                else if (_mode == InputMode.Settings)
                    OnSettingsBack?.Invoke();
            };

            // Open artwork search from the main screen.
            _input.OnX += () =>
            {
                if (_mode == InputMode.Main)
                    OnOpenParser?.Invoke();
            };

            // Open manual search in the parser.
            _input.OnY += () =>
            {
                if (_mode == InputMode.Parser)
                    OnParserSearch?.Invoke();
            };

            // Left shoulder switches the active tab.
            _input.OnLB += () =>
            {
                if (_mode == InputMode.Parser)
                    OnParserSwitchTab?.Invoke(-1);
                else if (_mode == InputMode.Main)
                    OnSwitchToGamelist?.Invoke();
            };

            // Right shoulder switches the active tab.
            _input.OnRB += () =>
            {
                if (_mode == InputMode.Parser)
                    OnParserSwitchTab?.Invoke(1);
                else if (_mode == InputMode.Main)
                    OnSwitchToLastPlayed?.Invoke();
            };

            // Start and Back share the same contextual action.
            _input.OnStart += HandleStartBack;
            _input.OnBack += HandleStartBack;
        }

        /// <summary>
        /// Handles Start and Back according to the current
        /// input mode, preventing rapid duplicate commands.
        /// </summary>
        private void HandleStartBack()
        {
            // The empty library screen has no contextual actions.
            if (_mode == InputMode.Empty)
                return;

            // Preserve the original 300 ms shared cooldown.
            if ((DateTime.Now - _lastStartBackTime).TotalMilliseconds < 300) return;

            _lastStartBackTime = DateTime.Now;

            if (_mode == InputMode.Parser)
                OnParserBack?.Invoke();
            else if (_mode == InputMode.Settings)
                OnSettingsBack?.Invoke();
            else
                OnOpenSettings?.Invoke();
        }
    }
}
