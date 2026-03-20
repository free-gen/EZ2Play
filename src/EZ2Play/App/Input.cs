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
    // --------------- Класс управления вводом (клавиатура + геймпад) ---------------

    public class Input : IDisposable
    {
        // --------------- Настройки и константы ---------------

        // Задержки ввода (мс)
        private const int InitialDelay = 300;
        private const int RepeatDelay = 70;
        private const int GamepadButtonCooldown = 200;

        // Частота опроса геймпада (мс)
        private const int GamepadPollInterval = 16;
        private const int GamepadCheckInterval = 2000;

        // --------------- Поля DirectInput ---------------

        private DirectInput _directInput;
        private Joystick _joystick;

        // --------------- Таймеры ---------------

        private DispatcherTimer _keyboardTimer;
        private DispatcherTimer _gamepadTimer;
        private DispatcherTimer _gamepadCheckTimer;

        // --------------- Состояние клавиатуры ---------------

        private bool _leftKeyPressed;
        private bool _rightKeyPressed;
        private long _keyHoldStart;
        private long _lastKeyboardInput;

        // --------------- Состояние геймпада ---------------

        private long _gamepadHoldStart;
        private long _lastGamepadNavInput;
        private long _lastGamepadButtonInput;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        // --------------- События ---------------

        public event Action<int> OnMoveSelection;
        public event Action OnLaunchSelected;
        public event Action OnExitApplication;
        public event Action OnToggleDisplay;
        public event Action<bool, string> OnGamepadConnectionChanged;

        // --------------- Публичные свойства ---------------

        public bool IsGamepadConnected { get; private set; }

        // --------------- Native imports ---------------

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        // --------------- Конструктор ---------------

        public Input()
        {
            InitializeGamepad();
            InitializeKeyboardTimer();
            InitializeGamepadCheckTimer();
        }

        // --------------- Инициализация ---------------

        // Настройка DirectInput и подключение геймпада
        private void InitializeGamepad()
        {
            try
            {
                _directInput = new DirectInput();
                ConnectGamepad();
            }
            catch { }
        }

        // Поиск и подключение первого доступного геймпада
        private void ConnectGamepad()
        {
            try
            {
                // Освобождаем старое устройство
                if (_joystick != null)
                {
                    _joystick.Unacquire();
                    _joystick.Dispose();
                    _joystick = null;
                }

                // Поиск устройств
                var devices = _directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices)
                    .Concat(_directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                    .ToList();

                if (devices.Count > 0)
                {
                    // Подключаем первое найденное устройство
                    _joystick = new Joystick(_directInput, devices[0].InstanceGuid);
                    _joystick.Properties.BufferSize = 128;
                    _joystick.Acquire();

                    // Получаем имя устройства
                    string deviceName = "Gamepad";
                    try
                    {
                        deviceName = devices[0].ProductName;
                        if (string.IsNullOrWhiteSpace(deviceName))
                            deviceName = "Gamepad";
                    }
                    catch { deviceName = "Gamepad"; }

                    // Запускаем таймер опроса
                    if (_gamepadTimer == null)
                    {
                        _gamepadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(GamepadPollInterval) };
                        _gamepadTimer.Tick += CheckGamepadInput;
                        _gamepadTimer.Start();
                    }

                    // Передаем true и имя устройства
                    UpdateConnectionState(true, deviceName);
                }
                else
                {
                    UpdateConnectionState(false, "Gamepad");
                }
            }
            catch { }
        }

        // Таймер обработки клавиатуры
        private void InitializeKeyboardTimer()
        {
            _keyboardTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(GamepadPollInterval) };
            _keyboardTimer.Tick += CheckKeyboardInput;
            _keyboardTimer.Start();
        }

        // Таймер проверки подключения геймпада (каждые 2 секунды)
        private void InitializeGamepadCheckTimer()
        {
            _gamepadCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(GamepadCheckInterval) };
            _gamepadCheckTimer.Tick += CheckGamepadConnection;
            _gamepadCheckTimer.Start();
        }

        // --------------- Управление состоянием подключения ---------------

        // Обновление статуса подключения геймпада
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

        // Проверка подключения геймпада (вызывается таймером)
        private void CheckGamepadConnection(object sender, EventArgs e)
        {
            if (_directInput == null) return;
            
            try
            {
                if (_joystick == null)
                {
                    // Пытаемся подключить если отсутствует
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
                else
                {
                    // Проверяем текущее устройство
                    try
                    {
                        _joystick.Poll();
                        _ = _joystick.GetCurrentState();
                    }
                    catch
                    {
                        // Устройство отключилось - переподключаем
                        _joystick.Unacquire();
                        _joystick.Dispose();
                        _joystick = null;
                        ConnectGamepad();
                    }
                }
            }
            catch { }
        }

        // --------------- Обработка клавиатуры ---------------

        // Проверка зажатых клавиш (повтор при удержании)
        private void CheckKeyboardInput(object sender, EventArgs e)
        {
            if (_keyHoldStart < 0) return;

            var now = _stopwatch.ElapsedMilliseconds;
            var holdDuration = now - _keyHoldStart;

            // Ждём начальную задержку
            if (holdDuration < InitialDelay) return;

            var timeSinceLastInput = now - _lastKeyboardInput;

            // Обработка влево
            if (_leftKeyPressed && timeSinceLastInput >= RepeatDelay)
            {
                _lastKeyboardInput = now;
                OnMoveSelection?.Invoke(-1);
            }
            // Обработка вправо
            else if (_rightKeyPressed && timeSinceLastInput >= RepeatDelay)
            {
                _lastKeyboardInput = now;
                OnMoveSelection?.Invoke(1);
            }
        }

        // Обработка нажатия клавиши
        // Используем полное имя namespace для избежания конфликта с SharpDX.DirectInput.Key
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
                }
            }
            catch { }
        }

        // Обработка отпускания клавиши
        // Используем полное имя namespace для избежания конфликта с SharpDX.DirectInput.Key
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

        // --------------- Обработка геймпада ---------------

        // Опрос состояния геймпада (каждые 16 мс)
        private void CheckGamepadInput(object sender, EventArgs e)
        {
            if (_joystick == null || !IsApplicationActive()) return;

            try
            {
                _joystick.Poll();
                var state = _joystick.GetCurrentState();

                // Навигация: D-Pad или левый стик
                bool leftPressed = state.PointOfViewControllers[0] == 27000 || state.X < 16384;
                bool rightPressed = state.PointOfViewControllers[0] == 9000 || state.X > 49152;

                // Кнопки: A, Back, X
                bool selectPressed = state.Buttons[0];
                bool backPressed = state.Buttons[1];
                bool xButtonPressed = state.Buttons[2];

                long now = _stopwatch.ElapsedMilliseconds;

                // Обработка навигации с задержками
                HandleGamepadNavigation(leftPressed, rightPressed, now);

                // Обработка кнопок с cooldown
                HandleGamepadButtons(selectPressed, backPressed, xButtonPressed, now);
            }
            catch { }
        }

        // Обработка навигации геймпада (влево/вправо)
        private void HandleGamepadNavigation(bool leftPressed, bool rightPressed, long now)
        {
            bool navigationPressed = leftPressed || rightPressed;

            if (navigationPressed)
            {
                // Первое нажатие
                if (_gamepadHoldStart < 0)
                {
                    _gamepadHoldStart = now;
                    _lastGamepadNavInput = now;

                    if (leftPressed) OnMoveSelection?.Invoke(-1);
                    else if (rightPressed) OnMoveSelection?.Invoke(1);
                }
                // Повтор при удержании
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
                // Сброс при отпускании
                _gamepadHoldStart = -1;
            }
        }

        // Обработка кнопок геймпада (A, Back, X)
        private void HandleGamepadButtons(bool selectPressed, bool backPressed, bool xButtonPressed, long now)
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
        }

        // --------------- Проверка активности окна ---------------

        // Проверка: наше ли окно сейчас активно
        private bool IsApplicationActive()
        {
            try
            {
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero) return false;

                GetWindowThreadProcessId(foregroundWindow, out int foregroundProcessId);
                return foregroundProcessId == Process.GetCurrentProcess().Id;
            }
            catch 
            { 
                return false; 
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