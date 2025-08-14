using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SharpDX.DirectInput;

namespace EZ2Play
{
    public class Input : IDisposable
    {
        private DirectInput _directInput;
        private Joystick _joystick;
        private DispatcherTimer _keyboardTimer;
        private DispatcherTimer _gamepadTimer;
        private DispatcherTimer _gamepadCheckTimer;
        
        private bool _upKeyPressed = false;
        private bool _downKeyPressed = false;
        private bool _leftKeyPressed = false;
        private bool _rightKeyPressed = false;
        private bool _isHorizontalMode = false;
        private DateTime _keyHoldStart = DateTime.MinValue;
        private DateTime _gamepadHoldStart = DateTime.MinValue;
        private DateTime _lastGamepadInput = DateTime.MinValue;
        private DateTime _lastKeyboardInput = DateTime.MinValue;
        
        private const int INITIAL_DELAY = 100; // Начальная задержка в мс
        private const int REPEAT_DELAY = 150; // Задержка повтора в мс
        private const int FAST_REPEAT_DELAY = 100; // Быстрая задержка повтора в мс

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

        public void SetHorizontalMode(bool isHorizontal)
        {
            _isHorizontalMode = isHorizontal;
        }

        private void UpdateConnectionState(bool isConnected)
        {
            if (IsGamepadConnected == isConnected) return;
            IsGamepadConnected = isConnected;
            try { OnGamepadConnectionChanged?.Invoke(isConnected); } catch {}
        }

        private void InitializeGamepad()
        {
            try
            {
                _directInput = new DirectInput();
                ConnectGamepad();
            }
            catch {}
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
                        _gamepadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
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
            catch {}
        }

        private void InitializeKeyboardTimer()
        {
            _keyboardTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60 FPS
            _keyboardTimer.Tick += CheckKeyboardInput;
            _keyboardTimer.Start();
        }

        private void InitializeGamepadCheckTimer()
        {
            _gamepadCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; // Проверяем каждые 2 секунды
            _gamepadCheckTimer.Tick += CheckGamepadConnection;
            _gamepadCheckTimer.Start();
        }

        private void CheckGamepadConnection(object sender, EventArgs e)
        {
            try
            {
                if (_joystick == null)
                {
                    // Если геймпад не подключен, попробуем подключить
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
            catch {}
        }

        private void CheckKeyboardInput(object sender, EventArgs e)
        {
            if (!IsThisApplicationActive()) return;

            var now = DateTime.Now;
            var holdDuration = (now - _keyHoldStart).TotalMilliseconds;

            if (holdDuration < INITIAL_DELAY) return;

            var timeSinceLastInput = (now - _lastKeyboardInput).TotalMilliseconds;
            
            var repeatDelay = holdDuration > 2000 ? FAST_REPEAT_DELAY : REPEAT_DELAY;

            if (_isHorizontalMode)
            {
                if (_leftKeyPressed && timeSinceLastInput >= repeatDelay)
                {
                    _lastKeyboardInput = now;
                    OnMoveSelection?.Invoke(-1);
                }
                else if (_rightKeyPressed && timeSinceLastInput >= repeatDelay)
                {
                    _lastKeyboardInput = now;
                    OnMoveSelection?.Invoke(1);
                }
            }
            else
            {
                if (_upKeyPressed && timeSinceLastInput >= repeatDelay)
                {
                    _lastKeyboardInput = now;
                    OnMoveSelection?.Invoke(-1);
                }
                else if (_downKeyPressed && timeSinceLastInput >= repeatDelay)
                {
                    _lastKeyboardInput = now;
                    OnMoveSelection?.Invoke(1);
                }
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

                bool upPressed = state.PointOfViewControllers[0] == 0 || state.Y < 16384;
                bool downPressed = state.PointOfViewControllers[0] == 18000 || state.Y > 49152;
                bool leftPressed = state.PointOfViewControllers[0] == 27000 || state.X < 16384;
                bool rightPressed = state.PointOfViewControllers[0] == 9000 || state.X > 49152;
                bool selectPressed = state.Buttons[0];
                bool backPressed = state.Buttons[1];
                bool xButtonPressed = state.Buttons[2];

                var now = DateTime.Now;
                var gamepadHoldDuration = (now - _gamepadHoldStart).TotalMilliseconds;

                bool navigationPressed;
                if (_isHorizontalMode)
                {
                    navigationPressed = leftPressed || rightPressed;
                }
                else
                {
                    navigationPressed = upPressed || downPressed;
                }

                if (navigationPressed)
                {
                    if (_gamepadHoldStart == DateTime.MinValue)
                    {
                        _gamepadHoldStart = now;
                        _lastGamepadInput = now;
                        
                        if (_isHorizontalMode)
                        {
                            if (leftPressed) OnMoveSelection?.Invoke(-1);
                            else if (rightPressed) OnMoveSelection?.Invoke(1);
                        }
                        else
                        {
                            if (upPressed) OnMoveSelection?.Invoke(-1);
                            else if (downPressed) OnMoveSelection?.Invoke(1);
                        }
                    }
                    else
                    {
                        if (gamepadHoldDuration >= INITIAL_DELAY)
                        {
                            var timeSinceLastInput = (now - _lastGamepadInput).TotalMilliseconds;
                            
                            var repeatDelay = gamepadHoldDuration > 2000 ? FAST_REPEAT_DELAY : REPEAT_DELAY;

                            if (timeSinceLastInput >= repeatDelay)
                            {
                                _lastGamepadInput = now;
                                if (_isHorizontalMode)
                                {
                                    if (leftPressed) OnMoveSelection?.Invoke(-1);
                                    else if (rightPressed) OnMoveSelection?.Invoke(1);
                                }
                                else
                                {
                                    if (upPressed) OnMoveSelection?.Invoke(-1);
                                    else if (downPressed) OnMoveSelection?.Invoke(1);
                                }
                            }
                        }
                    }
                }
                else
                {
                    _gamepadHoldStart = DateTime.MinValue;
                }

                if (selectPressed && (now - _lastGamepadInput).TotalMilliseconds >= 300)
                {
                    _lastGamepadInput = now;
                    OnLaunchSelected?.Invoke();
                }
                else if (backPressed && (now - _lastGamepadInput).TotalMilliseconds >= 300)
                {
                    _lastGamepadInput = now;
                    OnExitApplication?.Invoke();
                }
                else if (xButtonPressed && (now - _lastGamepadInput).TotalMilliseconds >= 300)
                {
                    _lastGamepadInput = now;
                    OnToggleDisplay?.Invoke();
                }
            }
            catch {}
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
                    case System.Windows.Input.Key.Up:
                        if (!_isHorizontalMode && !_upKeyPressed)
                        {
                            _upKeyPressed = true;
                            _keyHoldStart = DateTime.Now;
                            _lastKeyboardInput = DateTime.Now;
                            OnMoveSelection?.Invoke(-1);
                        }
                        break;
                    case System.Windows.Input.Key.Down:
                        if (!_isHorizontalMode && !_downKeyPressed)
                        {
                            _downKeyPressed = true;
                            _keyHoldStart = DateTime.Now;
                            _lastKeyboardInput = DateTime.Now;
                            OnMoveSelection?.Invoke(1);
                        }
                        break;
                    case System.Windows.Input.Key.Left:
                        if (_isHorizontalMode && !_leftKeyPressed)
                        {
                            _leftKeyPressed = true;
                            _keyHoldStart = DateTime.Now;
                            _lastKeyboardInput = DateTime.Now;
                            OnMoveSelection?.Invoke(-1);
                        }
                        break;
                    case System.Windows.Input.Key.Right:
                        if (_isHorizontalMode && !_rightKeyPressed)
                        {
                            _rightKeyPressed = true;
                            _keyHoldStart = DateTime.Now;
                            _lastKeyboardInput = DateTime.Now;
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
            catch {}
        }

        public void HandleKeyUp(System.Windows.Input.Key key)
        {
            try
            {
                switch (key)
                {
                    case System.Windows.Input.Key.Up:
                        _upKeyPressed = false;
                        _keyHoldStart = DateTime.MinValue;
                        break;
                    case System.Windows.Input.Key.Down:
                        _downKeyPressed = false;
                        _keyHoldStart = DateTime.MinValue;
                        break;
                    case System.Windows.Input.Key.Left:
                        _leftKeyPressed = false;
                        _keyHoldStart = DateTime.MinValue;
                        break;
                    case System.Windows.Input.Key.Right:
                        _rightKeyPressed = false;
                        _keyHoldStart = DateTime.MinValue;
                        break;
                }
            }
            catch {}
        }


        public void Dispose()
        {
            _gamepadTimer?.Stop();
            _keyboardTimer?.Stop();
            _gamepadCheckTimer?.Stop();
            _joystick?.Dispose();
            _directInput?.Dispose();
        }
    }
} 