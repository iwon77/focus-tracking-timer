using System.Windows;
using System.Windows.Controls;

namespace FocusTrackingTimer.App.Views.Timer;

public partial class ProjectDetailView : UserControl
{
    public ProjectDetailView()
    {
        InitializeComponent();
    }

    private MainWindow HostWindow => (MainWindow)Window.GetWindow(this)!;

    private void DeleteProjectButton_Click(object sender, RoutedEventArgs e) => HostWindow.DeleteProjectButton_Click(sender, e);

    private void EditSelectedProjectButton_Click(object sender, RoutedEventArgs e) => HostWindow.EditSelectedProjectButton_Click(sender, e);

    private void MemoSelectedProjectButton_Click(object sender, RoutedEventArgs e) => HostWindow.MemoSelectedProjectButton_Click(sender, e);

    private void TimerActionButton_Click(object sender, RoutedEventArgs e) => HostWindow.TimerActionButton_Click(sender, e);

    private void StopTimerButton_Click(object sender, RoutedEventArgs e) => HostWindow.StopTimerButton_Click(sender, e);
}
