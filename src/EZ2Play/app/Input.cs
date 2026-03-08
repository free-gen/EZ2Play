using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SharpDX.DirectInput;

namespace EZ2Play.App
{
    public class Input : IDisposable
    {
        private DirectInput _directInput;
        private Joystick _joystick;
        private DispatcherTimer _keyboardTimer;
        private DispatcherTimer _gamepadTimer;
        private DispatcherTimer _gamepadCheckTimer;
        
        private bool _leftKeyPressed = false;
        private bool _rightKeyPressed = false;
        private long _keyHoldStart = -1;
        private long _gamepadHoldStart = -1;
        private long _lastGamepadNavInput = -1;
        private long _lastGamepadButtonInput = -1;
        private readonly Stopwatch _gamepadStopwatch = Stopwatch.StartNew();
        
        private long _lastKeyboardInput = -1;
        
        private const int INITIAL_DELAY = 350;
        private const int REPEAT_DELAY = 200;
        private const int GAMEPAD_INITIAL_DELAY = 250;
        private const int GAMEPAD_REPEAT_DELAY = 100;

        public event Action<int> OnMoveSelection;
        public event Action OnLaunchSelected;
        public event Action OnExitApplication;
        public event Action OnToggleDisplay;
        public event Action<bool> OnGamepadConnectionChanged;

        public bool IsGamepadConnected { get; private set; }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        public Input()
        {
            InitializeGamepad();
            InitializeKeyboardTimer();
            InitializeGamepadCheckTimer();
        }

        private void UpdateConnectionState(bool isConnected)
        {
            if (IsGamepadConnected == isConnected) return;
            IsGamepadConnected = isConnected;
            try { OnGamepadConnectionChanged?.Invoke(isConnected); }
            catch { }
        }

        private void InitializeGamepad()
        {
            try
            {
                _directInput = new DirectInput();
                ConnectGamepad();
            }
            catch { }
        }

        private void ConnectGamepad()
        {
            try
            {
                if (_joystick != null)
                {
                    _joystick.Unacquire();
                    _joystick.Dispose();
                    _joystick = null;
                }

                var devices = _directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices)
                    .Concat(_directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                    .ToList();

                if (devices.Count > 0)
                {
                    _joystick = new Joystick(_directInput, devices[0].InstanceGuid);
                    _joystick.Properties.BufferSize = 128;
                    _joystick.Acquire();

                    if (_gamepadTimer == null)
                    {
                        _gamepadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                        _gamepadTimer.Tick += CheckGamepadInput;
                        _gamepadTimer.Start();
                    }

                    UpdateConnectionState(true);
                }
                else
                {
                    UpdateConnectionState(false);
                }
            }
            catch { }
        }

        private void InitializeKeyboardTimer()
        {
            _keyboardTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _keyboardTimer.Tick += CheckKeyboardInput;
            _keyboardTimer.Start();
        }

        private void InitializeGamepadCheckTimer()
        {
            _gamepadCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _gamepadCheckTimer.Tick += CheckGamepadConnection;
            _gamepadCheckTimer.Start();
        }

        private void CheckGamepadConnection(object sender, EventArgs e)
        {
            if (_directInput == null) return;
            try
            {
                if (_joystick == null)
                {
                    var devices = _directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices)
                        .Concat(_directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                        .ToList();

                    if (devices.Count > 0)
                    {
                        ConnectGamepad();
                    }
                    else
                    {
                        UpdateConnectionState(false);
                    }
                }
                else
                {
                    try
                    {
                        _joystick.Poll();
                        var state = _joystick.GetCurrentState();
                    }
                    catch
                    {
                        _joystick.Unacquire();
                        _joystick.Dispose();
                        _joystick = null;
                        ConnectGamepad();
                    }
                }
            }
            catch { }
        }

        private void CheckKeyboardInput(object sender, EventArgs e)
        {
            if (_keyHoldStart < 0)
                return;

            var now = _gamepadStopwatch.ElapsedMilliseconds;

            var holdDuration = now - _keyHoldStart;

            if (holdDuration < INITIAL_DELAY)
                return;

            var timeSinceLastInput = now - _lastKeyboardInput;

            if (_leftKeyPressed && timeSinceLastInput >= REPEAT_DELAY)
            {
                _lastKeyboardInput = now;
                OnMoveSelection?.Invoke(-1);
            }
            else if (_rightKeyPressed && timeSinceLastInput >= REPEAT_DELAY)
            {
                _lastKeyboardInput = now;
                OnMoveSelection?.Invoke(1);
            }
        }

        private void CheckGamepadInput(object sender, EventArgs e)
        {
            if (_joystick == null || !IsThisApplicationActive())
                return;

            try
            {
                _joystick.Poll();
                var state = _joystick.GetCurrentState();

                bool leftPressed = state.PointOfViewControllers[0] == 27000 || state.X < 16384;
                bool rightPressed = state.PointOfViewControllers[0] == 9000 || state.X > 49152;
                bool selectPressed = state.Buttons[0];
                bool backPressed = state.Buttons[1];
                bool xButtonPressed = state.Buttons[2];

                long now = _gamepadStopwatch.ElapsedMilliseconds;

                bool navigationPressed = leftPressed || rightPressed;

                if (navigationPressed)
                {
                    if (_gamepadHoldStart < 0)
                    {
                        _gamepadHoldStart = now;
                        _lastGamepadNavInput = now;

                        if (leftPressed) OnMoveSelection?.Invoke(-1);
                        else if (rightPressed) OnMoveSelection?.Invoke(1);
                    }
                    else
                    {
                        if (now - _gamepadHoldStart >= GAMEPAD_INITIAL_DELAY)
                        {
                            if (now - _lastGamepadNavInput >= GAMEPAD_REPEAT_DELAY)
                            {
                                _lastGamepadNavInput = now;

                                if (leftPressed) OnMoveSelection?.Invoke(-1);
                                else if (rightPressed) OnMoveSelection?.Invoke(1);
                            }
                        }
                    }
                }
                else
                {
                    _gamepadHoldStart = -1;
                }

                // КНОПКИ — отдельный таймер
                if (selectPressed && now - _lastGamepadButtonInput >= 200)
                {
                    _lastGamepadButtonInput = now;
                    OnLaunchSelected?.Invoke();
                }
                else if (backPressed && now - _lastGamepadButtonInput >= 200)
                {
                    _lastGamepadButtonInput = now;
                    OnExitApplication?.Invoke();
                }
                else if (xButtonPressed && now - _lastGamepadButtonInput >= 200)
                {
                    _lastGamepadButtonInput = now;
                    OnToggleDisplay?.Invoke();
                }
            }
            catch { }
        }

        private bool IsThisApplicationActive()
        {
            try
            {
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero) return false;

                GetWindowThreadProcessId(foregroundWindow, out int foregroundProcessId);
                return foregroundProcessId == Process.GetCurrentProcess().Id;
            }
            catch { return false; }
        }

        public void HandleKeyDown(System.Windows.Input.Key key)
        {
            try
            {
                switch (key)
                {
                    case System.Windows.Input.Key.Left:
                        if (!_leftKeyPressed)
                        {
                            _leftKeyPressed = true;
                            _keyHoldStart = _gamepadStopwatch.ElapsedMilliseconds;
                            _lastKeyboardInput = _gamepadStopwatch.ElapsedMilliseconds;
                            OnMoveSelection?.Invoke(-1);
                        }
                        break;
                    case System.Windows.Input.Key.Right:
                        if (!_rightKeyPressed)
                        {
                            _rightKeyPressed = true;
                            _keyHoldStart = _gamepadStopwatch.ElapsedMilliseconds;
                            _lastKeyboardInput = _gamepadStopwatch.ElapsedMilliseconds;
                            OnMoveSelection?.Invoke(1);
                        }
                        break;
                    case System.Windows.Input.Key.Enter:
                        OnLaunchSelected?.Invoke();
                        break;
                    case System.Windows.Input.Key.Escape:
                        OnExitApplication?.Invoke();
                        break;
                    case System.Windows.Input.Key.X:
                        OnToggleDisplay?.Invoke();
                        break;
                }
            }
            catch { }
        }

        public void HandleKeyUp(System.Windows.Input.Key key)
        {
            try
            {
                switch (key)
                {
                    case System.Windows.Input.Key.Left:
                        _leftKeyPressed = false;
                        _keyHoldStart = -1;
                        break;
                    case System.Windows.Input.Key.Right:
                        _rightKeyPressed = false;
                        _keyHoldStart = -1;
                        break;
                }
            }
            catch { }
        }

        public void Dispose()
        {
            _gamepadTimer?.Stop();
            _gamepadTimer = null;
            _keyboardTimer?.Stop();
            _keyboardTimer = null;
            _gamepadCheckTimer?.Stop();
            _gamepadCheckTimer = null;
            _joystick?.Unacquire();
            _joystick?.Dispose();
            _joystick = null;
            _directInput?.Dispose();
            _directInput = null;
        }
    }
}