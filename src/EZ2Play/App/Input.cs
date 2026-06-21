using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using SharpDX.XInput;

namespace EZ2Play.App
{
    public class Input : IDisposable
    {
        // Задержки ввода (мс)
        private const int InitialDelay = 250;
        private const int RepeatDelay = 75;
        private const int GamepadButtonCooldown = 200;

        // Частота опроса геймпада (мс)
        private const int GamepadPollInterval = 16;
        private const int GamepadCheckInterval = 2000;

        // Мертвая зона для стика
        private const short StickDeadZone = 8000;

        // Поля
        private Controller _controller;
        private DispatcherTimer _keyboardTimer;
        private DispatcherTimer _gamepadTimer;
        private DispatcherTimer _gamepadCheckTimer;
        private bool _leftKeyPressed;
        private bool _rightKeyPressed;
        private long _keyHoldStart = -1;
        private long _lastKeyboardInput;
        private long _gamepadHoldStart = -1;
        private long _lastGamepadNavInput;
        private long _lastGamepadButtonInput;
        private long _verticalHoldStart = -1;
        private long _lastVerticalInput;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private bool _isReconnecting;
        private bool _lastStartState;
        private bool _lastBackState;

        // События (нейтральные, без логики)
        public event Action<int> OnLeftRight;
        public event Action<int> OnUpDown;
        public event Action OnA;
        public event Action OnB;
        public event Action OnX;
        public event Action OnLB;
        public event Action OnRB;
        public event Action OnStart;
        public event Action OnBack;
        public event Action<bool, string> OnGamepadConnectionChanged;

        public event Action OnStartReleased;
        public event Action OnBackReleased;

        public bool IsGamepadConnected { get; private set; }

        public Input()
        {
            InitializeGamepad();
            InitializeKeyboardTimer();
            InitializeGamepadCheckTimer();
        }

        private void InitializeGamepad()
        {
            try
            {
                ConnectToFirstAvailableGamepad();
            }
            catch
            {
                UpdateConnectionState(false, "XInput initialization failed");
            }
        }

        private void ConnectToFirstAvailableGamepad()
        {
            for (int i = 0; i < 4; i++)
            {
                var testController = new Controller((UserIndex)i);
                if (testController.IsConnected)
                {
                    SetActiveController(testController, i);
                    return;
                }
            }
            UpdateConnectionState(false, "No XInput controller found");
        }

        private void SetActiveController(Controller newController, int userId)
        {
            _controller = newController;
            string deviceName = $"XInput Controller {userId + 1}";
            UpdateConnectionState(true, deviceName);
            StartGamepadTimer();
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

        private void UpdateConnectionState(bool isConnected, string deviceName)
        {
            if (IsGamepadConnected == isConnected) return;
            IsGamepadConnected = isConnected;
            OnGamepadConnectionChanged?.Invoke(isConnected, deviceName);
        }

        private void CheckGamepadConnection(object sender, EventArgs e)
        {
            if (_isReconnecting) return;
            try
            {
                if (_controller == null) { TryReconnectGamepad(); return; }
                if (!_controller.IsConnected)
                {
                    _controller = null;
                    UpdateConnectionState(false, "Gamepad disconnected");
                    TryReconnectGamepad();
                }
            }
            catch
            {
                _controller = null;
                UpdateConnectionState(false, "Gamepad error");
                TryReconnectGamepad();
            }
        }

        private void TryReconnectGamepad()
        {
            _isReconnecting = true;
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    var testController = new Controller((UserIndex)i);
                    if (testController.IsConnected)
                    {
                        SetActiveController(testController, i);
                        return;
                    }
                }
            }
            finally { _isReconnecting = false; }
        }

        private void CheckKeyboardInput(object sender, EventArgs e)
        {
            if (!SystemProvider.IsForeground()) return;
            if (_keyHoldStart < 0) return;

            long now = _stopwatch.ElapsedMilliseconds;
            if (now - _keyHoldStart < InitialDelay) return;

            long timeSinceLast = now - _lastKeyboardInput;
            if (_leftKeyPressed && timeSinceLast >= RepeatDelay)
            {
                _lastKeyboardInput = now;
                OnLeftRight?.Invoke(-1);
            }
            else if (_rightKeyPressed && timeSinceLast >= RepeatDelay)
            {
                _lastKeyboardInput = now;
                OnLeftRight?.Invoke(1);
            }
        }

        public void HandleKeyDown(Key key)
        {
            if (!SystemProvider.IsForeground()) return;

            switch (key)
            {
                case Key.Left:
                    if (!_leftKeyPressed)
                    {
                        _leftKeyPressed = true;
                        _keyHoldStart = _stopwatch.ElapsedMilliseconds;
                        _lastKeyboardInput = _stopwatch.ElapsedMilliseconds;
                        OnLeftRight?.Invoke(-1);
                    }
                    break;
                case Key.Right:
                    if (!_rightKeyPressed)
                    {
                        _rightKeyPressed = true;
                        _keyHoldStart = _stopwatch.ElapsedMilliseconds;
                        _lastKeyboardInput = _stopwatch.ElapsedMilliseconds;
                        OnLeftRight?.Invoke(1);
                    }
                    break;
                case Key.Up:
                    OnUpDown?.Invoke(-1);
                    break;
                case Key.Down:
                    OnUpDown?.Invoke(1);
                    break;
                case Key.Enter:
                    OnA?.Invoke();
                    break;
                case Key.Escape:
                    OnBack?.Invoke();
                    break;
                case Key.Q:
                    OnLB?.Invoke();
                    break;
                case Key.E:
                    OnRB?.Invoke();
                    break;
            }
        }

        public void HandleKeyUp(Key key)
        {
            if (!SystemProvider.IsForeground()) return;
            switch (key)
            {
                case Key.Left:
                    _leftKeyPressed = false;
                    _keyHoldStart = -1;
                    break;
                case Key.Right:
                    _rightKeyPressed = false;
                    _keyHoldStart = -1;
                    break;
            }
        }

        private void CheckGamepadInput(object sender, EventArgs e)
        {
            if (!SystemProvider.IsForeground()) return;
            if (_controller == null || !_controller.IsConnected) return;

            try
            {
                var state = _controller.GetState();
                var gamepad = state.Gamepad;
                long now = _stopwatch.ElapsedMilliseconds;

                bool left = (gamepad.Buttons & GamepadButtonFlags.DPadLeft) != 0 || gamepad.LeftThumbX < -StickDeadZone;
                bool right = (gamepad.Buttons & GamepadButtonFlags.DPadRight) != 0 || gamepad.LeftThumbX > StickDeadZone;
                bool up = (gamepad.Buttons & GamepadButtonFlags.DPadUp) != 0 || gamepad.LeftThumbY > StickDeadZone;
                bool down = (gamepad.Buttons & GamepadButtonFlags.DPadDown) != 0 || gamepad.LeftThumbY < -StickDeadZone;

                HandleHorizontalNavigation(left, right, now);
                HandleVerticalNavigation(up, down, now);
                HandleGamepadButtons(gamepad, now);
            }
            catch { }
        }

        private void HandleHorizontalNavigation(bool left, bool right, long now)
        {
            bool pressed = left || right;
            if (pressed)
            {
                if (_gamepadHoldStart < 0)
                {
                    _gamepadHoldStart = now;
                    _lastGamepadNavInput = now;
                    OnLeftRight?.Invoke(left ? -1 : 1);
                }
                else if (now - _gamepadHoldStart >= InitialDelay && now - _lastGamepadNavInput >= RepeatDelay)
                {
                    _lastGamepadNavInput = now;
                    OnLeftRight?.Invoke(left ? -1 : 1);
                }
            }
            else
            {
                _gamepadHoldStart = -1;
            }
        }

        private void HandleVerticalNavigation(bool up, bool down, long now)
        {
            bool pressed = up || down;
            if (pressed)
            {
                if (_verticalHoldStart < 0)
                {
                    _verticalHoldStart = now;
                    _lastVerticalInput = now;
                    OnUpDown?.Invoke(up ? -1 : 1);
                }
                else if (now - _verticalHoldStart >= InitialDelay && now - _lastVerticalInput >= RepeatDelay)
                {
                    _lastVerticalInput = now;
                    OnUpDown?.Invoke(up ? -1 : 1);
                }
            }
            else
            {
                _verticalHoldStart = -1;
            }
        }

        private void HandleGamepadButtons(Gamepad gamepad, long now)
        {
            if (now - _lastGamepadButtonInput < GamepadButtonCooldown) return;

            if ((gamepad.Buttons & GamepadButtonFlags.A) != 0)
            {
                _lastGamepadButtonInput = now;
                OnA?.Invoke();
            }
            else if ((gamepad.Buttons & GamepadButtonFlags.B) != 0)
            {
                _lastGamepadButtonInput = now;
                OnB?.Invoke();
            }
            else if ((gamepad.Buttons & GamepadButtonFlags.X) != 0)
            {
                _lastGamepadButtonInput = now;
                OnX?.Invoke();
            }
            else if ((gamepad.Buttons & GamepadButtonFlags.LeftShoulder) != 0)
            {
                _lastGamepadButtonInput = now;
                OnLB?.Invoke();
            }
            else if ((gamepad.Buttons & GamepadButtonFlags.RightShoulder) != 0)
            {
                _lastGamepadButtonInput = now;
                OnRB?.Invoke();
            }
            else if ((gamepad.Buttons & GamepadButtonFlags.Start) != 0)
            {
                _lastGamepadButtonInput = now;
                OnStart?.Invoke();
            }
            // else if ((gamepad.Buttons & GamepadButtonFlags.Back) != 0)
            // {
            //     _lastGamepadButtonInput = now;
            //     OnBack?.Invoke();
            // }

            // Отслеживание отпускания кнопок
            bool currentStart = (gamepad.Buttons & GamepadButtonFlags.Start) != 0;
            bool currentBack = (gamepad.Buttons & GamepadButtonFlags.Back) != 0;

            if (_lastStartState && !currentStart)
                OnStartReleased?.Invoke();
            if (_lastBackState && !currentBack)
                OnBackReleased?.Invoke();

            _lastStartState = currentStart;
            _lastBackState = currentBack;
        }

        public void Dispose()
        {
            _gamepadTimer?.Stop();
            _keyboardTimer?.Stop();
            _gamepadCheckTimer?.Stop();
            _controller = null;
        }
    }
}