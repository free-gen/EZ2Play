
using System;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using SharpDX.XInput;

namespace EZ2Play.App
{
    /// <summary>
    /// Handles keyboard and XInput gamepad input.
    /// Provides navigation and button events to higher-level
    /// application components.
    /// </summary>
    /// <remarks>
    /// Input polling uses WPF DispatcherTimers.
    /// Keyboard and gamepad navigation have independent repeat states.
    /// Input is processed only while the application is in the foreground.
    /// </remarks>
    public class Input : IDisposable
    {
        // Input repeat timing in milliseconds.
        private const int InitialDelay = 250;
        private const int RepeatDelay = 75;
        private const int GamepadButtonCooldown = 200;

        // Gamepad polling intervals in milliseconds.
        private const int GamepadPollInterval = 16;
        private const int GamepadCheckInterval = 2000;

        // Analog stick dead zone.
        private const short StickDeadZone = 8000;

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

        private bool _lastAState;
        private bool _lastBState;
        private bool _enterKeyPressed;
        private bool _escapeKeyPressed;
        private bool _lastBackState;

        // Navigation events.
        public event Action<int> OnLeftRight;
        public event Action<int> OnUpDown;

        // Action button events.
        public event Action OnA;
        public event Action OnB;
        public event Action OnX;
        public event Action OnY;
        public event Action OnLB;
        public event Action OnRB;
        public event Action OnStart;
        public event Action OnBack;

        // Device connection events.
        public event Action<bool, string> OnGamepadConnectionChanged;

        public bool IsGamepadConnected { get; private set; }

        public Input()
        {
            InitializeGamepad();
            InitializeKeyboardTimer();
            InitializeGamepadCheckTimer();
        }

        /// <summary>
        /// Initializes XInput and searches for an available controller.
        /// </summary>
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

        /// <summary>
        /// Searches the four supported XInput controller slots.
        /// </summary>
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

        /// <summary>
        /// Assigns the active controller and starts input polling.
        /// </summary>
        private void SetActiveController(Controller newController, int userId)
        {
            _controller = newController;
            string deviceName = $"XInput Controller {userId + 1}";

            UpdateConnectionState(true, deviceName);
            StartGamepadTimer();
        }

        /// <summary>
        /// Starts the gamepad input polling timer if necessary.
        /// </summary>
        private void StartGamepadTimer()
        {
            if (_gamepadTimer != null) return;

            _gamepadTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(GamepadPollInterval)
            };

            _gamepadTimer.Tick += CheckGamepadInput;
            _gamepadTimer.Start();
        }

        /// <summary>
        /// Starts the keyboard repeat polling timer.
        /// </summary>
        private void InitializeKeyboardTimer()
        {
            _keyboardTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(GamepadPollInterval)
            };

            _keyboardTimer.Tick += CheckKeyboardInput;
            _keyboardTimer.Start();
        }

        /// <summary>
        /// Starts periodic controller connection checks.
        /// </summary>
        private void InitializeGamepadCheckTimer()
        {
            _gamepadCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(GamepadCheckInterval)
            };

            _gamepadCheckTimer.Tick += CheckGamepadConnection;
            _gamepadCheckTimer.Start();
        }

        /// <summary>
        /// Updates the gamepad connection state and notifies listeners
        /// only when the connection status actually changes.
        /// </summary>
        private void UpdateConnectionState(bool isConnected, string deviceName)
        {
            if (IsGamepadConnected == isConnected) return;

            IsGamepadConnected = isConnected;
            OnGamepadConnectionChanged?.Invoke(isConnected, deviceName);
        }

        /// <summary>
        /// Checks whether the active controller is still connected.
        /// Attempts reconnection when necessary.
        /// </summary>
        private void CheckGamepadConnection(object sender, EventArgs e)
        {
            if (_isReconnecting) return;

            try
            {
                if (_controller == null)
                {
                    TryReconnectGamepad();
                    return;
                }

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

        /// <summary>
        /// Attempts to reconnect to an available XInput controller.
        /// </summary>
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
            finally
            {
                _isReconnecting = false;
            }
        }

        /// <summary>
        /// Processes repeated horizontal keyboard navigation.
        /// </summary>
        /// <remarks>
        /// The first navigation event is generated by HandleKeyDown.
        /// Repetition begins after InitialDelay and continues
        /// at RepeatDelay intervals.
        /// </remarks>
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

        /// <summary>
        /// Handles a keyboard key press and raises the
        /// corresponding navigation or action event.
        /// </summary>
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
                    if (!_enterKeyPressed)
                    {
                        _enterKeyPressed = true;
                        OnA?.Invoke();
                    }
                    break;

                case Key.Escape:
                    if (!_escapeKeyPressed)
                    {
                        _escapeKeyPressed = true;
                        OnBack?.Invoke();
                    }
                    break;

                case Key.X:
                    OnX?.Invoke();
                    break;

                case Key.Y:
                    OnY?.Invoke();
                    break;

                case Key.Q:
                    OnLB?.Invoke();
                    break;

                case Key.E:
                    OnRB?.Invoke();
                    break;
            }
        }

        /// <summary>
        /// Releases keyboard hold states when keys are released.
        /// </summary>
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

                case Key.Enter:
                    _enterKeyPressed = false;
                    break;

                case Key.Escape:
                    _escapeKeyPressed = false;
                    break;
            }
        }

        /// <summary>
        /// Polls the active gamepad and translates controller
        /// state into navigation and action events.
        /// </summary>
        private void CheckGamepadInput(object sender, EventArgs e)
        {
            if (!SystemProvider.IsForeground()) return;
            if (_controller == null || !_controller.IsConnected) return;

            try
            {
                var state = _controller.GetState();
                var gamepad = state.Gamepad;
                long now = _stopwatch.ElapsedMilliseconds;

                // Combine directional pad input with analog stick input.
                bool left = (gamepad.Buttons & GamepadButtonFlags.DPadLeft) != 0 || gamepad.LeftThumbX < -StickDeadZone;
                bool right = (gamepad.Buttons & GamepadButtonFlags.DPadRight) != 0 || gamepad.LeftThumbX > StickDeadZone;
                bool up = (gamepad.Buttons & GamepadButtonFlags.DPadUp) != 0 || gamepad.LeftThumbY > StickDeadZone;
                bool down = (gamepad.Buttons & GamepadButtonFlags.DPadDown) != 0 || gamepad.LeftThumbY < -StickDeadZone;

                HandleHorizontalNavigation(left, right, now);
                HandleVerticalNavigation(up, down, now);
                HandleGamepadButtons(gamepad, now);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Handles horizontal gamepad navigation with
        /// an initial press and timed repetition.
        /// </summary>
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

        /// <summary>
        /// Handles vertical gamepad navigation independently
        /// from horizontal navigation.
        /// </summary>
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

        /// <summary>
        /// Processes gamepad action buttons, edge-triggered presses,
        /// and button cooldowns.
        /// </summary>
        /// <remarks>
        /// Back, A, and B are triggered once per physical press.
        /// Other action buttons use the shared cooldown.
        /// The order of checks and early returns is intentional.
        /// </remarks>
        private void HandleGamepadButtons(Gamepad gamepad, long now)
        {
            bool aPressed = (gamepad.Buttons & GamepadButtonFlags.A) != 0;
            bool bPressed = (gamepad.Buttons & GamepadButtonFlags.B) != 0;
            bool backPressed = (gamepad.Buttons & GamepadButtonFlags.Back) != 0;

            // Back fires only once per physical button press.
            if (backPressed && !_lastBackState)
            {
                _lastBackState = true;
                OnBack?.Invoke();
                return;
            }

            if (!backPressed)
                _lastBackState = false;

            // A fires only once per physical button press.
            if (aPressed && !_lastAState)
            {
                _lastAState = true;
                _lastGamepadButtonInput = now;
                OnA?.Invoke();
                return;
            }

            if (!aPressed)
                _lastAState = false;

            // B fires only once per physical button press.
            if (bPressed && !_lastBState)
            {
                _lastBState = true;
                _lastGamepadButtonInput = now;
                OnB?.Invoke();
                return;
            }

            if (!bPressed)
                _lastBState = false;

            if (now - _lastGamepadButtonInput < GamepadButtonCooldown) return;

            if ((gamepad.Buttons & GamepadButtonFlags.X) != 0)
            {
                _lastGamepadButtonInput = now;
                OnX?.Invoke();
            }
            else if ((gamepad.Buttons & GamepadButtonFlags.Y) != 0)
            {
                _lastGamepadButtonInput = now;
                OnY?.Invoke();
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
        }

        /// <summary>
        /// Stops input polling and releases the active
        /// controller reference.
        /// </summary>
        public void Dispose()
        {
            _gamepadTimer?.Stop();
            _keyboardTimer?.Stop();
            _gamepadCheckTimer?.Stop();
            _controller = null;
        }
    }
}
