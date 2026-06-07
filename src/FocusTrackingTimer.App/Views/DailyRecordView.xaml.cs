using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FocusTrackingTimer.App.Views;

public partial class DailyRecordView : UserControl
{
    public DailyRecordView()
    {
        InitializeComponent();
    }

    private MainWindow HostWindow => (MainWindow)Window.GetWindow(this)!;

    private void CalendarRecordButton_Click(object sender, RoutedEventArgs e) => HostWindow.CalendarRecordButton_Click(sender, e);

    private void RecentRecordButton_Click(object sender, RoutedEventArgs e) => HostWindow.RecentRecordButton_Click(sender, e);

    private void PreviousRecordYearButton_Click(object sender, RoutedEventArgs e) => HostWindow.PreviousRecordYearButton_Click(sender, e);

    private void PreviousRecordMonthButton_Click(object sender, RoutedEventArgs e) => HostWindow.PreviousRecordMonthButton_Click(sender, e);

    private void NextRecordMonthButton_Click(object sender, RoutedEventArgs e) => HostWindow.NextRecordMonthButton_Click(sender, e);

    private void NextRecordYearButton_Click(object sender, RoutedEventArgs e) => HostWindow.NextRecordYearButton_Click(sender, e);

    private void CurrentRecordMonthButton_Click(object sender, RoutedEventArgs e) => HostWindow.CurrentRecordMonthButton_Click(sender, e);

    private void RecordFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => HostWindow.RecordFilter_SelectionChanged(sender, e);

    private void CalendarDayBorder_MouseEnter(object sender, MouseEventArgs e) => HostWindow.CalendarDayBorder_MouseEnter(sender, e);

    private void CalendarDayBorder_MouseLeave(object sender, MouseEventArgs e) => HostWindow.CalendarDayBorder_MouseLeave(sender, e);
}
