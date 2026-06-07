using System.Collections.ObjectModel;
using System.Windows;
using FocusTrackingTimer.App.Infrastructure;

namespace FocusTrackingTimer.App.ViewModels;

public sealed class WeeklyRecordViewModel : ObservableObject
{
    private string _displayedWeekRangeText = string.Empty;
    private string _displayedWeekLabelText = string.Empty;
    private string _weekTotalFocusDurationText = "00:00:00";
    private string _weekTotalWallClockDurationText = "00:00:00";
    private string _weeklyRecordCountText = "0건";
    private string _averageDailyWallClockDurationText = "00:00:00";
    private string _averageDailyFocusDurationText = "00:00:00";
    private string _selectedRecordTitle = "선택한 작업이 없습니다.";
    private string _selectedRecordSubtitle = string.Empty;
    private string _selectedRecordTotalDurationText = "00:00:00";
    private string _selectedRecordFocusDurationText = "00:00:00";
    private string _selectedRecordFocusRatioText = "0%";
    private string _selectedRecordEmptyText = "선택한 작업의 프로그램별 세부 시간이 여기에 표시됩니다.";
    private Visibility _selectedRecordEmptyVisibility = Visibility.Visible;
    private Visibility _selectedRecordDetailVisibility = Visibility.Collapsed;
    private RecordFilterOption? _selectedRecordFilter;
    private WeeklyRecordRow? _selectedWeeklyRecordRow;

    public ObservableCollection<RecordFilterOption> RecordFilterOptions { get; } = [];

    public ObservableCollection<WeeklyDayBubbleRow> WeeklyDayBubbleRows { get; } = [];

    public ObservableCollection<WeeklyRecordRow> WeeklyRecordRows { get; } = [];

    public ObservableCollection<ProgramDurationRow> SelectedRecordProgramRows { get; } = [];

    public string DisplayedWeekRangeText
    {
        get => _displayedWeekRangeText;
        set => SetProperty(ref _displayedWeekRangeText, value);
    }

    public string DisplayedWeekLabelText
    {
        get => _displayedWeekLabelText;
        set => SetProperty(ref _displayedWeekLabelText, value);
    }

    public string WeekTotalFocusDurationText
    {
        get => _weekTotalFocusDurationText;
        set => SetProperty(ref _weekTotalFocusDurationText, value);
    }

    public string WeekTotalWallClockDurationText
    {
        get => _weekTotalWallClockDurationText;
        set => SetProperty(ref _weekTotalWallClockDurationText, value);
    }

    public string WeeklyRecordCountText
    {
        get => _weeklyRecordCountText;
        set => SetProperty(ref _weeklyRecordCountText, value);
    }

    public string AverageDailyWallClockDurationText
    {
        get => _averageDailyWallClockDurationText;
        set => SetProperty(ref _averageDailyWallClockDurationText, value);
    }

    public string AverageDailyFocusDurationText
    {
        get => _averageDailyFocusDurationText;
        set => SetProperty(ref _averageDailyFocusDurationText, value);
    }

    public string SelectedRecordTitle
    {
        get => _selectedRecordTitle;
        set => SetProperty(ref _selectedRecordTitle, value);
    }

    public string SelectedRecordSubtitle
    {
        get => _selectedRecordSubtitle;
        set => SetProperty(ref _selectedRecordSubtitle, value);
    }

    public string SelectedRecordTotalDurationText
    {
        get => _selectedRecordTotalDurationText;
        set => SetProperty(ref _selectedRecordTotalDurationText, value);
    }

    public string SelectedRecordFocusDurationText
    {
        get => _selectedRecordFocusDurationText;
        set => SetProperty(ref _selectedRecordFocusDurationText, value);
    }

    public string SelectedRecordFocusRatioText
    {
        get => _selectedRecordFocusRatioText;
        set => SetProperty(ref _selectedRecordFocusRatioText, value);
    }

    public string SelectedRecordEmptyText
    {
        get => _selectedRecordEmptyText;
        set => SetProperty(ref _selectedRecordEmptyText, value);
    }

    public Visibility SelectedRecordEmptyVisibility
    {
        get => _selectedRecordEmptyVisibility;
        set => SetProperty(ref _selectedRecordEmptyVisibility, value);
    }

    public Visibility SelectedRecordDetailVisibility
    {
        get => _selectedRecordDetailVisibility;
        set => SetProperty(ref _selectedRecordDetailVisibility, value);
    }

    public RecordFilterOption? SelectedRecordFilter
    {
        get => _selectedRecordFilter;
        set => SetProperty(ref _selectedRecordFilter, value);
    }

    public WeeklyRecordRow? SelectedWeeklyRecordRow
    {
        get => _selectedWeeklyRecordRow;
        set => SetProperty(ref _selectedWeeklyRecordRow, value);
    }
}
