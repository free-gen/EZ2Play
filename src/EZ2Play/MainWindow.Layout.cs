
using System;
using System.Windows;
using System.Windows.Threading;
using EZ2Play.App;

namespace EZ2Play
{
    /// <summary>
    /// Handles scale-dependent resources, carousel layout, and selector
    /// positioning when the main window layout changes.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Initializes the visual selection for the carousel.
        /// </summary>
        private void InitializeCarouselSelectedItem()
        {
            CarouselAnimation.InitializeSelectedItem(ItemsListBox);
        }

        /// <summary>
        /// Restores or initializes the selected carousel item once
        /// WPF has generated the item containers.
        /// </summary>
        private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {
            if (ItemsListBox.ItemContainerGenerator.Status !=
                System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                return;
            }

            if (CarouselAnimation.RetryPendingSelection(ItemsListBox))
                return;

            InitializeCarouselSelectedItem();
        }

        /// <summary>
        /// Applies scale resources only when the calculated scale changes.
        /// Preserves the original comparison threshold and update order.
        /// </summary>
        private void UpdateUiScaleResources(double windowHeight)
        {
            double scale = LayoutScaler.GetScaleFactor(windowHeight);

            DebugLog.Write(
                "Layout",
                $"Scale: height={windowHeight:F1}, " +
                $"new={scale:F3}, previous={_lastUiScaleFactor:F3}");

            if (Math.Abs(_lastUiScaleFactor - scale) < 0.0001) return;

            LayoutScaler.ApplyUiScaleToDictionary(this.Resources, windowHeight);
            _lastUiScaleFactor = scale;
        }

        /// <summary>
        /// Refreshes the selector belonging to the currently visible screen.
        /// </summary>
        private void RefreshActiveSelector()
        {
            if (_isExiting) return;

            if (_settingsOverlay?.Visibility == Visibility.Visible)
                _settingsOverlay.RefreshSelector();
            else if (_parserOverlay?.Visibility == Visibility.Visible)
                _parserOverlay.RefreshSelector();
            else
                _selectorCoordinator?.UpdateMain();
        }

        /// <summary>
        /// Defers display layout updates until WPF has processed layout.
        /// Two DispatcherPriority.Loaded callbacks preserve the original
        /// order of scaling, carousel selection, selector, and pan updates.
        /// </summary>
        private void ScheduleDisplayLayoutRefresh()
        {
            if (_displayLayoutRefreshScheduled || _isExiting) return;
            _displayLayoutRefreshScheduled = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_isExiting && ActualHeight > 0)
                {
                    UpdateUiScaleResources(ActualHeight);
                    RefreshMaximizedClientAreaPadding();
                    ItemsListBox?.InvalidateMeasure();
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _displayLayoutRefreshScheduled = false;
                    if (_isExiting || ActualHeight <= 0) return;

                    InitializeCarouselSelectedItem();
                    RefreshActiveSelector();
                    _backgroundController?.RefreshPan();

                    // Inspect the frame compensation applied by WPF UI.
                    var presenter = Template?.FindName("ContentPresenter", this)
                        as System.Windows.Controls.ContentPresenter;

                    var clientBorder = presenter == null
                        ? null
                        : System.Windows.Media.VisualTreeHelper.GetParent(presenter)
                            as Wpf.Ui.Controls.ClientAreaBorder;

                    if (clientBorder != null)
                    {
                        DebugLog.Write(
                            "Layout",
                            $"Chrome: dpi={System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX:F2}, " +
                            $"padding={clientBorder.Padding}, " +
                            $"window={ActualWidth:F0}x{ActualHeight:F0}, " +
                            $"content={RootGrid.ActualWidth:F0}x{RootGrid.ActualHeight:F0}");
                    }
                }), DispatcherPriority.Loaded);
            }), DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Corrects the maximized window's client-area padding
        /// after a display or DPI change.
        /// </summary>
        /// <remarks>
        /// WPF UI caches the non-client frame thickness without
        /// accounting for subsequent per-monitor DPI changes.
        /// The padding is calculated from the maximized window
        /// bounds and the primary display dimensions.
        /// </remarks>
        private void RefreshMaximizedClientAreaPadding()
        {
            if (WindowState != WindowState.Maximized)
                return;

            var presenter = Template?.FindName("ContentPresenter", this)
                as System.Windows.Controls.ContentPresenter;

            var border = presenter == null
                ? null
                : System.Windows.Media.VisualTreeHelper.GetParent(presenter)
                    as Wpf.Ui.Controls.ClientAreaBorder;

            if (border == null)
                return;

            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);

            double screenWidth =
                SystemParameters.PrimaryScreenWidth / dpi.DpiScaleX;

            double screenHeight =
                SystemParameters.PrimaryScreenHeight / dpi.DpiScaleY;

            double horizontalPadding =
                (ActualWidth - screenWidth) / 2;

            double verticalPadding =
                (ActualHeight - screenHeight) / 2;

            // Skip incomplete display transitions and invalid bounds.
            if (horizontalPadding < 0 || horizontalPadding > 32 ||
                verticalPadding < 0 || verticalPadding > 32)
            {
                return;
            }

            var padding = new Thickness(
                horizontalPadding,
                verticalPadding,
                horizontalPadding,
                verticalPadding);

            if (border.Padding == padding)
                return;

            border.Padding = padding;

            DebugLog.Write(
                "Layout",
                $"Chrome padding corrected: {padding}, " +
                $"dpi={dpi.DpiScaleX:F2}");
        }

        /// <summary>
        /// Requests a deferred layout update after the window size changes.
        /// </summary>
        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            DebugLog.Write(
                "Layout",
                $"SizeChanged: {e.NewSize.Width:F1}x{e.NewSize.Height:F1}");

            ScheduleDisplayLayoutRefresh();
        }

        /// <summary>
        /// Requests the same deferred layout update when the window DPI changes.
        /// </summary>
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);

            DebugLog.Write(
                "Layout",
                $"DPI changed: {oldDpi.DpiScaleX:F2} -> {newDpi.DpiScaleX:F2}");

            ScheduleDisplayLayoutRefresh();
        }
    }
}
