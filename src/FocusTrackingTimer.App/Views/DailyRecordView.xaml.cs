using System.Windows;
using System.Windows.Controls;

namespace FocusTrackingTimer.App.Views;

public partial class DailyRecordView : UserControl
{
    public DailyRecordView()
    {
        InitializeComponent();
    }

    private MainWindow HostWindow => (MainWindow)Window.GetWindow(this)!;

    private void PreviousRecordYearButton_Click(object sender, RoutedEventArgs e) => HostWindow.PreviousRecordYearButton_Click(sender, e);

    private void PreviousRecordMonthButton_Click(object sender, RoutedEventArgs e) => HostWindow.PreviousRecordMonthButton_Click(sender, e);

    private void NextRecordMonthButton_Click(object sender, RoutedEventArgs e) => HostWindow.NextRecordMonthButton_Click(sender, e);

    private void NextRecordYearButton_Click(object sender, RoutedEventArgs e) => HostWindow.NextRecordYearButton_Click(sender, e);

    private void CurrentRecordMonthButton_Click(object sender, RoutedEventArgs e) => HostWindow.CurrentRecordMonthButton_Click(sender, e);

    private void RecordFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => HostWindow.RecordFilter_SelectionChanged(sender, e);

    private void CalendarDayButton_Click(object sender, RoutedEventArgs e) => HostWindow.CalendarDayButton_Click(sender, e);
}
