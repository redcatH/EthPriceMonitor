using System.Windows;
using System.Windows.Input;

namespace EthPriceMonitor.Views;

/// <summary>
/// Borderless floating window for ETH/USDT price display.
/// Draggable via mouse left button on the window background.
/// </summary>
public partial class FloatingWindow : Window
{
    public FloatingWindow()
    {
        InitializeComponent();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
}
