using System.Collections.ObjectModel;
using FocusTrackingTimer.App.Infrastructure;

namespace FocusTrackingTimer.App.ViewModels;

public sealed class DailyRecordViewModel : ObservableObject
{
    private string _displayedRecordMonthText = string.Empty;
    private string _monthlyWorkedDayCountText = "0일";
    private string _monthlyTotalWallClockDurationText = "00:00:00";
    private string _monthlyTotalFocusDurationText = "00:00:00";
    private string _monthlyAverageWallClockDurationText = "00:00:00";
    private string _monthlyAverageFocusDurationText = "00:00:00";
    private string _selectedDailyDateText = string.Empty;
    private string _selectedDailyTotalDurationText = "0m";
    private string _selectedDailyFocusRatioText = "0%";
    private string _selectedDailyEmptyText = "선택한 날짜의 작업 기록이 없습니다.";
    private RecordFilterOption? _selectedRecordFilter;

    public ObservableCollection<RecordFilterOption> RecordFilterOptions { get; } = [];

    public ObservableCollection<CalendarDayRow> CalendarRows { get; } = [];

    public ObservableCollection<DailyRecordItemRow> SelectedDailyRecordRows { get; } = [];

    public string DisplayedRecordMonthText
    {
        get => _displayedRecordMonthText;
        set => SetProperty(ref _displayedRecordMonthText, value);
    }

    public string MonthlyWorkedDayCountText
    {
        get => _monthlyWorkedDayCountText;
        set => SetProperty(ref _monthlyWorkedDayCountText, value);
    }

    public string MonthlyTotalWallClockDurationText
    {
        get => _monthlyTotalWallClockDurationText;
        set => SetProperty(ref _monthlyTotalWallClockDurationText, value);
    }

    public string MonthlyTotalFocusDurationText
    {
        get => _monthlyTotalFocusDurationText;
        set => SetProperty(ref _monthlyTotalFocusDurationText, value);
    }

    public string MonthlyAverageWallClockDurationText
    {
        get => _monthlyAverageWallClockDurationText;
        set => SetProperty(ref _monthlyAverageWallClockDurationText, value);
    }

    public string MonthlyAverageFocusDurationText
    {
        get => _monthlyAverageFocusDurationText;
        set => SetProperty(ref _monthlyAverageFocusDurationText, value);
    }

    public string SelectedDailyDateText
    {
        get => _selectedDailyDateText;
        set => SetProperty(ref _selectedDailyDateText, value);
    }

    public string SelectedDailyTotalDurationText
    {
        get => _selectedDailyTotalDurationText;
        set => SetProperty(ref _selectedDailyTotalDurationText, value);
    }

    public string SelectedDailyFocusRatioText
    {
        get => _selectedDailyFocusRatioText;
        set => SetProperty(ref _selectedDailyFocusRatioText, value);
    }

    public string SelectedDailyEmptyText
    {
        get => _selectedDailyEmptyText;
        set => SetProperty(ref _selectedDailyEmptyText, value);
    }

    public RecordFilterOption? SelectedRecordFilter
    {
        get => _selectedRecordFilter;
        set => SetProperty(ref _selectedRecordFilter, value);
    }
}
