using System.Windows;
using System.Windows.Controls;

namespace FocusTrackingTimer.App.Views.Timer;

public partial class RegisteredProgramListView : UserControl
{
    public RegisteredProgramListView()
    {
        InitializeComponent();
    }

    private MainWindow HostWindow => (MainWindow)Window.GetWindow(this)!;

    private void DeleteProgramButton_Click(object sender, RoutedEventArgs e) => HostWindow.DeleteProgramButton_Click(sender, e);

    private void EditProgramButton_Click(object sender, RoutedEventArgs e) => HostWindow.EditProgramButton_Click(sender, e);

    private void FocusRegisteredProgramButton_Click(object sender, RoutedEventArgs e) => HostWindow.FocusRegisteredProgramButton_Click(sender, e);

    private void OpenProgramManagerButton_Click(object sender, RoutedEventArgs e) => HostWindow.OpenProgramManagerButton_Click(sender, e);

    private void PinProgramButton_Click(object sender, RoutedEventArgs e) => HostWindow.PinProgramButton_Click(sender, e);

    private void ProgramSort_SelectionChanged(object sender, SelectionChangedEventArgs e) => HostWindow.ProgramSort_SelectionChanged(sender, e);
}
