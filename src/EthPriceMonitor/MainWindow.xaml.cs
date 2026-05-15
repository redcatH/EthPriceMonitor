using System.ComponentModel;
using System.Windows;

namespace EthPriceMonitor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Hide window instead of closing (minimize to tray)
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
