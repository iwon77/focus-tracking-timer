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
    private string _selectedDailyRecordCountText = "0건";
    private string _selectedDailyTotalWallClockDurationText = "00:00:00";
    private string _selectedDailyFocusSummaryText = "00:00:00 (0%)";
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

    public string SelectedDailyRecordCountText
    {
        get => _selectedDailyRecordCountText;
        set => SetProperty(ref _selectedDailyRecordCountText, value);
    }

    public string SelectedDailyTotalWallClockDurationText
    {
        get => _selectedDailyTotalWallClockDurationText;
        set => SetProperty(ref _selectedDailyTotalWallClockDurationText, value);
    }

    public string SelectedDailyFocusSummaryText
    {
        get => _selectedDailyFocusSummaryText;
        set => SetProperty(ref _selectedDailyFocusSummaryText, value);
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
