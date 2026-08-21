using System;
using System.Windows;

namespace EZ2Play.App
{
    public class InputHandler
    {
        private readonly Input _input;

        private bool _settingsOpen;
        private bool _parserOpen;

        private DateTime _lastStartBackTime;
        private SettingsOverlay _settingsOverlay;

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

        public InputHandler(Input input)
        {
            _input = input;
            SubscribeEvents();
        }

        public void SetSettingsOpen(bool open)
        {
            _settingsOpen = open;
        }

        public void SetParserOpen(bool open)
        {
            _parserOpen = open;
        }

        public void RegisterSettingsOverlay(SettingsOverlay overlay)
        {
            _settingsOverlay = overlay;
        }

        // Route input based on the currently active overlay.
        private void SubscribeEvents()
        {
            _input.OnLeftRight += dir =>
            {
                if (_parserOpen)
                    OnParserNavigateHorizontal?.Invoke(dir);
                else if (_settingsOverlay != null && _settingsOverlay.Visibility == Visibility.Visible)
                    OnSettingsNavigate?.Invoke(dir);
                else
                    OnMoveSelection?.Invoke(dir);
            };

            _input.OnUpDown += dir =>
            {
                if (_parserOpen)
                    OnParserNavigateVertical?.Invoke(dir);
                else if (_settingsOverlay != null && _settingsOverlay.Visibility == Visibility.Visible)
                    OnSettingsNavigateVertical?.Invoke(dir);
            };

            _input.OnA += () =>
            {
                if (_parserOpen)
                    OnParserConfirm?.Invoke();
                else if (_settingsOverlay != null && _settingsOverlay.Visibility == Visibility.Visible)
                    OnSettingsConfirm?.Invoke();
                else
                    OnLaunchSelected?.Invoke();
            };

            _input.OnB += () =>
            {
                if (_parserOpen)
                    OnParserBack?.Invoke();
                else if (_settingsOverlay != null && _settingsOverlay.Visibility == Visibility.Visible)
                    OnSettingsBack?.Invoke();
            };

            _input.OnX += () =>
            {
                if (!_settingsOpen && !_parserOpen)
                    OnOpenParser?.Invoke();
            };

            _input.OnY += () =>
            {
                if (_parserOpen)
                    OnParserSearch?.Invoke();
            };

            _input.OnLB += () =>
            {
                if (!_settingsOpen && !_parserOpen)
                    OnSwitchToGamelist?.Invoke();
            };

            _input.OnRB += () =>
            {
                if (!_settingsOpen && !_parserOpen)
                    OnSwitchToLastPlayed?.Invoke();
            };

            _input.OnStart += () =>
            {
                if ((DateTime.Now - _lastStartBackTime).TotalMilliseconds < 300) return;

                _lastStartBackTime = DateTime.Now;

                if (_parserOpen)
                    OnParserBack?.Invoke();
                else if (_settingsOpen)
                    OnSettingsBack?.Invoke();
                else
                    OnOpenSettings?.Invoke();
            };

            _input.OnBack += () =>
            {
                if ((DateTime.Now - _lastStartBackTime).TotalMilliseconds < 300) return;

                _lastStartBackTime = DateTime.Now;

                if (_parserOpen)
                    OnParserBack?.Invoke();
                else if (_settingsOpen)
                    OnSettingsBack?.Invoke();
                else
                    OnOpenSettings?.Invoke();
            };
        }
    }
}