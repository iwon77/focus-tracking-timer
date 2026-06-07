using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FocusTrackingTimer.App.Views.Timer;

public partial class ProjectSidebarView : UserControl
{
    public ProjectSidebarView()
    {
        InitializeComponent();
    }

    private MainWindow HostWindow => (MainWindow)Window.GetWindow(this)!;

    private void AddProjectButton_Click(object sender, RoutedEventArgs e) => HostWindow.AddProjectButton_Click(sender, e);

    private void PinProjectRowButton_Click(object sender, RoutedEventArgs e) => HostWindow.PinProjectRowButton_Click(sender, e);

    private void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e) => HostWindow.ProjectList_SelectionChanged(sender, e);

    private void ProjectSort_SelectionChanged(object sender, SelectionChangedEventArgs e) => HostWindow.ProjectSort_SelectionChanged(sender, e);

    private void RunningProjectSummary_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        HostWindow.RunningProjectSummary_MouseLeftButtonUp(sender, e);
}
