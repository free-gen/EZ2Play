using System;

namespace EZ2Play.App
{
    public class InputHandler
    {
        public enum InputMode
        {
            Main,
            Settings,
            Parser
        }

        private readonly Input _input;
        private InputMode _mode = InputMode.Main;
        private DateTime _lastStartBackTime;

        public event Action<int> OnMoveSelection;
        public event Action<int> OnSettingsNavigate;
        public event Action<int> OnSettingsNavigateVertical;

        public event Action OnLaunchSelected;
        public event Action OnOpenSettings;
        public event Action OnSwitchToGamelist;
        public event Action OnSwitchToLastPlayed;

        public event Action OnSettingsConfirm;
        public event Action OnSettingsBack;

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

        public void SetMode(InputMode mode)
        {
            _mode = mode;
        }

        // Route input based on the currently active overlay.
        private void SubscribeEvents()
        {
            _input.OnLeftRight += dir =>
            {
                if (_mode == InputMode.Parser)
                    OnParserNavigateHorizontal?.Invoke(dir);
                else if (_mode == InputMode.Settings)
                    OnSettingsNavigate?.Invoke(dir);
                else
                    OnMoveSelection?.Invoke(dir);
            };

            _input.OnUpDown += dir =>
            {
                if (_mode == InputMode.Parser)
                    OnParserNavigateVertical?.Invoke(dir);
                else if (_mode == InputMode.Settings)
                    OnSettingsNavigateVertical?.Invoke(dir);
            };

            _input.OnA += () =>
            {
                if (_mode == InputMode.Parser)
                    OnParserConfirm?.Invoke();
                else if (_mode == InputMode.Settings)
                    OnSettingsConfirm?.Invoke();
                else
                    OnLaunchSelected?.Invoke();
            };

            _input.OnB += () =>
            {
                if (_mode == InputMode.Parser)
                    OnParserBack?.Invoke();
                else if (_mode == InputMode.Settings)
                    OnSettingsBack?.Invoke();
            };

            _input.OnX += () =>
            {
                if (_mode == InputMode.Main)
                    OnOpenParser?.Invoke();
            };

            _input.OnY += () =>
            {
                if (_mode == InputMode.Parser)
                    OnParserSearch?.Invoke();
            };

            _input.OnLB += () =>
            {
                if (_mode == InputMode.Parser)
                    OnParserSwitchTab?.Invoke(-1);
                else if (_mode == InputMode.Main)
                    OnSwitchToGamelist?.Invoke();
            };

            _input.OnRB += () =>
            {
                if (_mode == InputMode.Parser)
                    OnParserSwitchTab?.Invoke(1);
                else if (_mode == InputMode.Main)
                    OnSwitchToLastPlayed?.Invoke();
            };

            _input.OnStart += HandleStartBack;
            _input.OnBack += HandleStartBack;
        }

        private void HandleStartBack()
        {
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