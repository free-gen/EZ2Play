using System;
using System.Windows;

namespace EZ2Play.App
{
    public class InputHandler
    {
        private readonly Input _input;
        private bool _settingsOpen;
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
        

        public InputHandler(Input input)
        {
            _input = input;
            SubscribeEvents();
        }

        public void SetSettingsOpen(bool open)
        {
            _settingsOpen = open;
        }

        public void RegisterSettingsOverlay(SettingsOverlay overlay) // <- ДОБАВИТЬ
        {
            _settingsOverlay = overlay;
        }

        private void SubscribeEvents()
        {
            _input.OnLeftRight += (dir) => 
            { 
                if (_settingsOverlay != null && _settingsOverlay.Visibility == Visibility.Visible)
                OnSettingsNavigate?.Invoke(dir);
                else 
                    OnMoveSelection?.Invoke(dir);
            };

            _input.OnUpDown += (dir) => 
            { 
                if (_settingsOverlay != null && _settingsOverlay.Visibility == Visibility.Visible)
                    OnSettingsNavigateVertical?.Invoke(dir);
            };
            
            _input.OnA += () =>
            {
                if (_settingsOverlay != null && _settingsOverlay.Visibility == Visibility.Visible)
                    OnSettingsConfirm?.Invoke();
                else OnLaunchSelected?.Invoke();
            };
            
            _input.OnB += () => 
            { 
                if (_settingsOverlay != null && _settingsOverlay.Visibility == Visibility.Visible)
                    OnSettingsBack?.Invoke(); 
            };
            _input.OnLB += () => { if (!_settingsOpen) OnSwitchToGamelist?.Invoke(); };
            _input.OnRB += () => { if (!_settingsOpen) OnSwitchToLastPlayed?.Invoke(); };
            
            _input.OnStart += () =>
            {
                if ((DateTime.Now - _lastStartBackTime).TotalMilliseconds < 300) return;
                _lastStartBackTime = DateTime.Now;
                
                if (_settingsOpen) OnSettingsBack?.Invoke();
                else OnOpenSettings?.Invoke();
            };
            
            _input.OnBack += () =>
            {
                if ((DateTime.Now - _lastStartBackTime).TotalMilliseconds < 300) return;
                _lastStartBackTime = DateTime.Now;
                
                if (_settingsOpen) OnSettingsBack?.Invoke();
                else OnOpenSettings?.Invoke();
            };
        }
    }
}