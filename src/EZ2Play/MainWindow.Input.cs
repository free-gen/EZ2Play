
using System;
using System.Windows.Input;

namespace EZ2Play
{
    /// <summary>
    /// Connects keyboard and gamepad input to the main window,
    /// launcher, settings overlay, and artwork parser.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Forwards key presses to the application input controller.
        /// </summary>
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            _input.HandleKeyDown(e.Key);
        }

        /// <summary>
        /// Forwards key releases to the application input controller.
        /// </summary>
        private void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            _input.HandleKeyUp(e.Key);
        }

        /// <summary>
        /// Connects input commands to the launcher and specialized overlays.
        /// The original callback order and guard conditions are preserved.
        /// </summary>
        private void SetupInputEvents()
        {
            _inputHandler.OnMoveSelection += direction =>
            {
                if (_isExiting)
                    return;

                _backgroundTransitionDirection = Math.Sign(direction);
                _launcher.MoveSelection(direction);
            };

            _inputHandler.OnLaunchSelected += () =>
            {
                if (_isExiting) return;

                _launcher.LaunchSelected();
            };

            _inputHandler.OnSwitchToGamelist += () =>
            {
                if (_isExiting) return;
                _tabsController.SwitchToGamelist();
            };

            _inputHandler.OnSwitchToLastPlayed += () =>
            {
                if (_isExiting) return;
                _tabsController.SwitchToLastPlayed();
            };

            _inputHandler.OnOpenSettings += () =>
            {
                if (_isExiting || !_isMainScreenActive || _isEmptyState) return;

                _settingsOverlay.Open();
            };

            _inputHandler.OnSettingsBack += () => _settingsOverlay.Back();
            _inputHandler.OnSettingsConfirm += () => _settingsOverlay.Confirm();

            _inputHandler.OnSettingsNavigate += (dir) => _settingsOverlay.Navigate(dir, true);
            _inputHandler.OnSettingsNavigateVertical += (dir) => _settingsOverlay.Navigate(dir, false);

            _inputHandler.OnOpenParser += () =>
            {
                if (_isExiting || !_isMainScreenActive) return;

                _parserOverlay.Open();
            };

            _inputHandler.OnParserBack += () => _parserOverlay.Back();
            _inputHandler.OnParserConfirm += () => _parserOverlay.Confirm();
            _inputHandler.OnParserSearch += () => _parserOverlay.Search();
            _inputHandler.OnParserNavigateHorizontal += dir => _parserOverlay.NavigateHorizontal(dir);
            _inputHandler.OnParserNavigateVertical += dir => _parserOverlay.NavigateVertical(dir);
            _inputHandler.OnParserSwitchTab += dir => _parserOverlay.SwitchAssetTab(dir);
        }

        /// <summary>
        /// Updates the input hints and reports a newly connected gamepad.
        /// </summary>
        private void OnGamepadConnectionChanged(bool connected, string deviceName)
        {
            IsGamepadConnected = connected;
            _uiRegistry.RefreshHintIcons(connected);

            if (connected)
            {
                _uiRegistry.Notifications.HotPlug(0, 3, deviceName);
            }
        }

        /// <summary>
        /// Preserves the empty-library Escape shortcut and suppresses Tab.
        /// </summary>
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_isEmptyState && e.Key == Key.Escape)
            {
                e.Handled = true;
                ExitApplication();
                return;
            }

            if (e.Key == Key.Tab)
            {
                e.Handled = true;
            }
        }
    }
}
