using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using FocusTrackingTimer.App.Infrastructure;

namespace FocusTrackingTimer.App.ViewModels;

public sealed class DailyRecordViewModel : ObservableObject
{
    private string _recordHeadlineText = "오늘은 아직 작업 기록이 없습니다.";
    private string _todayWorkedText = "00:00:00";
    private string _displayedRecordMonthText = string.Empty;
    private string _calendarHoverTitle = string.Empty;
    private string _selectedRecordFilterLabel = "<모든 프로젝트>";
    private Brush _calendarButtonBackground;
    private Brush _recentButtonBackground;
    private Visibility _calendarRecordVisibility = Visibility.Visible;
    private Visibility _recentRecordVisibility = Visibility.Collapsed;
    private Visibility _calendarHoverCardVisibility = Visibility.Collapsed;
    private RecordFilterOption? _selectedRecordFilter;

    public DailyRecordViewModel(Brush calendarButtonBackground, Brush recentButtonBackground)
    {
        _calendarButtonBackground = calendarButtonBackground;
        _recentButtonBackground = recentButtonBackground;
    }

    public ObservableCollection<RecordFilterOption> RecordFilterOptions { get; } = [];

    public ObservableCollection<CalendarDayRow> CalendarRows { get; } = [];

    public ObservableCollection<RecentRecordRow> RecentRecordRows { get; } = [];

    public ObservableCollection<string> CalendarHoverLines { get; } = [];

    public string RecordHeadlineText
    {
        get => _recordHeadlineText;
        set => SetProperty(ref _recordHeadlineText, value);
    }

    public string TodayWorkedText
    {
        get => _todayWorkedText;
        set => SetProperty(ref _todayWorkedText, value);
    }

    public string DisplayedRecordMonthText
    {
        get => _displayedRecordMonthText;
        set => SetProperty(ref _displayedRecordMonthText, value);
    }

    public string CalendarHoverTitle
    {
        get => _calendarHoverTitle;
        set => SetProperty(ref _calendarHoverTitle, value);
    }

    public string SelectedRecordFilterLabel
    {
        get => _selectedRecordFilterLabel;
        set => SetProperty(ref _selectedRecordFilterLabel, value);
    }

    public Brush CalendarButtonBackground
    {
        get => _calendarButtonBackground;
        set => SetProperty(ref _calendarButtonBackground, value);
    }

    public Brush RecentButtonBackground
    {
        get => _recentButtonBackground;
        set => SetProperty(ref _recentButtonBackground, value);
    }

    public Visibility CalendarRecordVisibility
    {
        get => _calendarRecordVisibility;
        set => SetProperty(ref _calendarRecordVisibility, value);
    }

    public Visibility RecentRecordVisibility
    {
        get => _recentRecordVisibility;
        set => SetProperty(ref _recentRecordVisibility, value);
    }

    public Visibility CalendarHoverCardVisibility
    {
        get => _calendarHoverCardVisibility;
        set => SetProperty(ref _calendarHoverCardVisibility, value);
    }

    public RecordFilterOption? SelectedRecordFilter
    {
        get => _selectedRecordFilter;
        set => SetProperty(ref _selectedRecordFilter, value);
    }
}
