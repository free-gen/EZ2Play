using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using SharpDX.DirectInput;

namespace EZ2Play.App
{
    // --------------- Класс управления вводом (клавиатура + геймпад) ---------------

    public class Input : IDisposable
    {
        // --------------- Константы ---------------

        // Задержки ввода (мс)
        private const int InitialDelay = 300;
        private const int RepeatDelay = 70;
        private const int GamepadButtonCooldown = 200;

        // Частота опроса геймпада (мс)
        private const int GamepadPollInterval = 16;
        private const int GamepadCheckInterval = 2000;

        // Мертвые зоны стика
        private const int StickLeftThreshold = 16384;
        private const int StickRightThreshold = 49152;

        // D-Pad направления (градусы)
        private const int DPadLeft = 27000;
        private const int DPadRight = 9000;

        // --------------- Поля ---------------

        // DirectInput
        private DirectInput _directInput;
        private Joystick _joystick;

        // Таймеры
        private DispatcherTimer _keyboardTimer;
        private DispatcherTimer _gamepadTimer;
        private DispatcherTimer _gamepadCheckTimer;

        // Состояние клавиатуры
        private bool _leftKeyPressed;
        private bool _rightKeyPressed;
        private long _keyHoldStart = -1;
        private long _lastKeyboardInput;

        // Состояние геймпада
        private long _gamepadHoldStart = -1;
        private long _lastGamepadNavInput;
        private long _lastGamepadButtonInput;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        // --------------- События ---------------

        public event Action<int> OnMoveSelection;
        public event Action OnLaunchSelected;
        public event Action OnExitApplication;
        public event Action OnToggleDisplay;
        public event Action<bool, string> OnGamepadConnectionChanged;
        public event Action OnSwitchToGamelist;
        public event Action OnSwitchToLastPlayed;

        // --------------- Свойства ---------------

        public bool IsGamepadConnected { get; private set; }

        // --------------- Конструктор ---------------

        public Input()
        {
            InitializeGamepad();
            InitializeKeyboardTimer();
            InitializeGamepadCheckTimer();
        }

        // --------------- Инициализация ---------------

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

                    string deviceName = GetDeviceName(devices[0]);
                    StartGamepadTimer();
                    UpdateConnectionState(true, deviceName);
                }
                else
                {
                    UpdateConnectionState(false, "Gamepad");
                }
            }
            catch { }
        }

        private string GetDeviceName(DeviceInstance device)
        {
            try
            {
                string name = device.ProductName;
                return string.IsNullOrWhiteSpace(name) ? "Gamepad" : name;
            }
            catch
            {
                return "Gamepad";
            }
        }

        private void StartGamepadTimer()
        {
            if (_gamepadTimer != null) return;
            
            _gamepadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(GamepadPollInterval) };
            _gamepadTimer.Tick += CheckGamepadInput;
            _gamepadTimer.Start();
        }

        private void InitializeKeyboardTimer()
        {
            _keyboardTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(GamepadPollInterval) };
            _keyboardTimer.Tick += CheckKeyboardInput;
            _keyboardTimer.Start();
        }

        private void InitializeGamepadCheckTimer()
        {
            _gamepadCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(GamepadCheckInterval) };
            _gamepadCheckTimer.Tick += CheckGamepadConnection;
            _gamepadCheckTimer.Start();
        }

        // --------------- Подключение геймпада ---------------

        private void UpdateConnectionState(bool isConnected, string deviceName)
        {
            if (IsGamepadConnected == isConnected) return;

            IsGamepadConnected = isConnected;

            try
            {
                OnGamepadConnectionChanged?.Invoke(isConnected, deviceName);
            }
            catch { }
        }

        private void CheckGamepadConnection(object sender, EventArgs e)
        {
            if (_directInput == null) return;
            
            try
            {
                if (_joystick == null)
                {
                    TryReconnectGamepad();
                }
                else
                {
                    ValidateCurrentGamepad();
                }
            }
            catch { }
        }

        private void TryReconnectGamepad()
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
                UpdateConnectionState(false, "Gamepad");
            }
        }

        private void ValidateCurrentGamepad()
        {
            try
            {
                _joystick.Poll();
                _ = _joystick.GetCurrentState();
            }
            catch
            {
                _joystick.Unacquire();
                _joystick.Dispose();
                _joystick = null;
                ConnectGamepad();
            }
        }

        // --------------- Клавиатура ---------------

        private void CheckKeyboardInput(object sender, EventArgs e)
        {
            if (_keyHoldStart < 0) return;

            long now = _stopwatch.ElapsedMilliseconds;
            long holdDuration = now - _keyHoldStart;

            if (holdDuration < InitialDelay) return;

            long timeSinceLastInput = now - _lastKeyboardInput;

            if (_leftKeyPressed && timeSinceLastInput >= RepeatDelay)
            {
                _lastKeyboardInput = now;
                OnMoveSelection?.Invoke(-1);
            }
            else if (_rightKeyPressed && timeSinceLastInput >= RepeatDelay)
            {
                _lastKeyboardInput = now;
                OnMoveSelection?.Invoke(1);
            }
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
                            _keyHoldStart = _stopwatch.ElapsedMilliseconds;
                            _lastKeyboardInput = _stopwatch.ElapsedMilliseconds;
                            OnMoveSelection?.Invoke(-1);
                        }
                        break;

                    case System.Windows.Input.Key.Right:
                        if (!_rightKeyPressed)
                        {
                            _rightKeyPressed = true;
                            _keyHoldStart = _stopwatch.ElapsedMilliseconds;
                            _lastKeyboardInput = _stopwatch.ElapsedMilliseconds;
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

                    case System.Windows.Input.Key.Q:
                        OnSwitchToGamelist?.Invoke();
                        break;

                    case System.Windows.Input.Key.E:
                        OnSwitchToLastPlayed?.Invoke();
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

        // --------------- Геймпад ---------------

        private void CheckGamepadInput(object sender, EventArgs e)
        {
            if (_joystick == null || !SystemProvider.IsForeground()) return;

            try
            {
                _joystick.Poll();
                var state = _joystick.GetCurrentState();

                bool leftPressed = state.PointOfViewControllers[0] == DPadLeft || state.X < StickLeftThreshold;
                bool rightPressed = state.PointOfViewControllers[0] == DPadRight || state.X > StickRightThreshold;

                bool selectPressed = state.Buttons[0];
                bool backPressed = state.Buttons[1];
                bool xButtonPressed = state.Buttons[2];
                bool lButtonPressed = state.Buttons[4];
                bool rButtonPressed = state.Buttons[5];

                long now = _stopwatch.ElapsedMilliseconds;

                HandleGamepadNavigation(leftPressed, rightPressed, now);
                HandleGamepadButtons(selectPressed, backPressed, xButtonPressed, lButtonPressed, rButtonPressed, now);
            }
            catch { }
        }

        private void HandleGamepadNavigation(bool leftPressed, bool rightPressed, long now)
        {
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
                else if (now - _gamepadHoldStart >= InitialDelay)
                {
                    if (now - _lastGamepadNavInput >= RepeatDelay)
                    {
                        _lastGamepadNavInput = now;

                        if (leftPressed) OnMoveSelection?.Invoke(-1);
                        else if (rightPressed) OnMoveSelection?.Invoke(1);
                    }
                }
            }
            else
            {
                _gamepadHoldStart = -1;
            }
        }

        private void HandleGamepadButtons(bool selectPressed, bool backPressed, bool xButtonPressed,
                                          bool lButtonPressed, bool rButtonPressed, long now)
        {
            if (selectPressed && now - _lastGamepadButtonInput >= GamepadButtonCooldown)
            {
                _lastGamepadButtonInput = now;
                OnLaunchSelected?.Invoke();
            }
            else if (backPressed && now - _lastGamepadButtonInput >= GamepadButtonCooldown)
            {
                _lastGamepadButtonInput = now;
                OnExitApplication?.Invoke();
            }
            else if (xButtonPressed && now - _lastGamepadButtonInput >= GamepadButtonCooldown)
            {
                _lastGamepadButtonInput = now;
                OnToggleDisplay?.Invoke();
            }
            else if (lButtonPressed && now - _lastGamepadButtonInput >= GamepadButtonCooldown)
            {
                _lastGamepadButtonInput = now;
                OnSwitchToGamelist?.Invoke();
            }
            else if (rButtonPressed && now - _lastGamepadButtonInput >= GamepadButtonCooldown)
            {
                _lastGamepadButtonInput = now;
                OnSwitchToLastPlayed?.Invoke();
            }
        }

        // --------------- Очистка ресурсов ---------------

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