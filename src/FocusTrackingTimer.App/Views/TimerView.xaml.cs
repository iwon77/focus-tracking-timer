using System.Windows;
using System.Windows.Controls;

namespace FocusTrackingTimer.App.Views;

public partial class TimerView : UserControl
{
    public TimerView()
    {
        InitializeComponent();
    }

    private MainWindow HostWindow => (MainWindow)Window.GetWindow(this)!;

    private void AddProjectButton_Click(object sender, RoutedEventArgs e) => HostWindow.AddProjectButton_Click(sender, e);

    private void DeleteProjectButton_Click(object sender, RoutedEventArgs e) => HostWindow.DeleteProjectButton_Click(sender, e);

    private void EditSelectedProjectButton_Click(object sender, RoutedEventArgs e) => HostWindow.EditSelectedProjectButton_Click(sender, e);

    private void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e) => HostWindow.ProjectList_SelectionChanged(sender, e);

    private void TimerActionButton_Click(object sender, RoutedEventArgs e) => HostWindow.TimerActionButton_Click(sender, e);

    private void OpenProgramManagerButton_Click(object sender, RoutedEventArgs e) => HostWindow.OpenProgramManagerButton_Click(sender, e);

    private void EditProgramButton_Click(object sender, RoutedEventArgs e) => HostWindow.EditProgramButton_Click(sender, e);

    private void DeleteProgramButton_Click(object sender, RoutedEventArgs e) => HostWindow.DeleteProgramButton_Click(sender, e);

    private void FocusRegisteredProgramButton_Click(object sender, RoutedEventArgs e) => HostWindow.FocusRegisteredProgramButton_Click(sender, e);

    private void ProgramSort_SelectionChanged(object sender, SelectionChangedEventArgs e) => HostWindow.ProgramSort_SelectionChanged(sender, e);
}
