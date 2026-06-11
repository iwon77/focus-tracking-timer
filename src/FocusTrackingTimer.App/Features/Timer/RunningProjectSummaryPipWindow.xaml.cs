using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using FocusTrackingTimer.App.ViewModels;

namespace FocusTrackingTimer.App.Features.Timer;

internal sealed partial class RunningProjectSummaryPipWindow : Window
{
    private readonly MainWindow _hostWindow;
    private bool _isRestoringMainWindow;

    public RunningProjectSummaryPipWindow(MainWindow hostWindow, TimerViewModel viewModel)
    {
        _hostWindow = hostWindow ?? throw new ArgumentNullException(nameof(hostWindow));
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
    }

    public void RestoreMainWindowAndClose()
    {
        _isRestoringMainWindow = true;
        Close();
    }

    private void RestoreMainWindowButton_Click(object sender, RoutedEventArgs e)
    {
        _hostWindow.RestoreMainWindowFromRunningProjectSummaryPip();
    }

    private void CloseApplicationButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (_isRestoringMainWindow)
        {
            return;
        }

        _hostWindow.Close();
    }
}
