using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace EthPriceMonitor.Views;

/// <summary>
/// Borderless floating window for ETH/USDT price display.
/// Supports click-through mode via Win32 WS_EX_TRANSPARENT.
/// When pinned (click-through), mouse events pass through to windows below.
/// </summary>
public partial class FloatingWindow : Window
{
    public FloatingWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Subscribe to ViewModel's click-through change events
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.ClickThroughChanged += OnClickThroughChanged;
            // Apply initial state
            if (vm.IsClickThrough)
                SetClickThrough(true);
        }

        DataContextChanged += (s, args) =>
        {
            // Unsubscribe from old ViewModel
            if (args.OldValue is ViewModels.MainViewModel oldVm)
                oldVm.ClickThroughChanged -= OnClickThroughChanged;

            // Subscribe to new ViewModel
            if (args.NewValue is ViewModels.MainViewModel newVm)
            {
                newVm.ClickThroughChanged += OnClickThroughChanged;
                if (newVm.IsClickThrough)
                    SetClickThrough(true);
            }
        };
    }

    private void OnClickThroughChanged(bool isClickThrough)
    {
        Dispatcher.Invoke(() => SetClickThrough(isClickThrough));
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    #region Win32 Click-Through

    private void SetClickThrough(bool enable)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

        if (enable)
        {
            // WS_EX_TRANSPARENT: mouse clicks pass through to windows below
            // WS_EX_LAYERED: required for WS_EX_TRANSPARENT to work with AllowsTransparency
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
        }
        else
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
        }
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    #endregion
}
