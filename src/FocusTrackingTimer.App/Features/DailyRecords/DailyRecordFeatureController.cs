using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FocusTrackingTimer.App.Infrastructure;
using FocusTrackingTimer.App.ViewModels;
using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App.Features.DailyRecords;

internal sealed class DailyRecordFeatureController
{
    private readonly ProjectTimerEngine _engine;
    private readonly DailyRecordViewModel _viewModel;
    private readonly Brush _calendarButtonBackground;
    private readonly Brush _recentButtonBackground;
    private DateOnly _displayedRecordMonth = new(DateTime.Now.Year, DateTime.Now.Month, 1);
    private DateOnly? _hoveredCalendarDate;
    private Dictionary<DateOnly, IReadOnlyList<string>> _calendarHoverLinesByDate = [];

    public DailyRecordFeatureController(
        ProjectTimerEngine engine,
        DailyRecordViewModel viewModel,
        Brush calendarButtonBackground,
        Brush recentButtonBackground)
    {
        _engine = engine;
        _viewModel = viewModel;
        _calendarButtonBackground = calendarButtonBackground;
        _recentButtonBackground = recentButtonBackground;
        _viewModel.DisplayedRecordMonthText = AppTimeFormatter.FormatRecordMonth(_displayedRecordMonth);
    }

    public void ShowCalendarRecord()
    {
        RefreshRecordViewState();
        RefreshRecordArea(DateTimeOffset.Now);
    }

    public void ShowRecentRecord()
    {
        RefreshRecordViewState();
        RefreshRecordArea(DateTimeOffset.Now);
    }

    public void MoveDisplayedRecordMonth(int monthOffset)
    {
        _displayedRecordMonth = _displayedRecordMonth.AddMonths(monthOffset);
        RefreshRecordArea(DateTimeOffset.Now);
    }

    public void MoveDisplayedRecordMonthToCurrent()
    {
        DateTime today = DateTime.Now;
        _displayedRecordMonth = new DateOnly(today.Year, today.Month, 1);
        RefreshRecordArea(DateTimeOffset.Now);
    }

    public void RefreshRecordFilters()
    {
        Guid? selectedFilterProjectId = _viewModel.SelectedRecordFilter?.ProjectId;

        _viewModel.RecordFilterOptions.Clear();
        _viewModel.RecordFilterOptions.Add(new RecordFilterOption(null, "<모든 프로젝트>"));
        foreach (ProjectDefinition project in _engine.Projects.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            _viewModel.RecordFilterOptions.Add(new RecordFilterOption(project.Id, project.Name));
        }

        _viewModel.SelectedRecordFilter = _viewModel.RecordFilterOptions.FirstOrDefault(option => option.ProjectId == selectedFilterProjectId)
            ?? _viewModel.RecordFilterOptions[0];
    }

    public void RefreshRecordArea(DateTimeOffset observedAt)
    {
        Guid? projectFilter = _viewModel.SelectedRecordFilter?.ProjectId;
        _viewModel.SelectedRecordFilterLabel = _viewModel.SelectedRecordFilter?.Label ?? "<모든 프로젝트>";
        _viewModel.DisplayedRecordMonthText = AppTimeFormatter.FormatRecordMonth(_displayedRecordMonth);

        DateOnly today = DateOnly.FromDateTime(observedAt.LocalDateTime.Date);
        _viewModel.TodayWorkedText = AppTimeFormatter.FormatDuration(_engine.GetTodayDuration(today, observedAt, projectFilter));
        _viewModel.RecordHeadlineText = _viewModel.TodayWorkedText == "00:00:00"
            ? "오늘은 아직 작업 기록이 없습니다."
            : $"오늘은 {_viewModel.TodayWorkedText} 작업했습니다.";

        RefreshCalendar(today, _displayedRecordMonth, observedAt, projectFilter);
        RefreshRecentRecords(projectFilter);
        RefreshRecordViewState();
    }

    public void RefreshRecordViewState()
    {
        _viewModel.CalendarRecordVisibility = Visibility.Visible;
        _viewModel.RecentRecordVisibility = Visibility.Collapsed;
        _viewModel.CalendarButtonBackground = _calendarButtonBackground;
        _viewModel.RecentButtonBackground = _recentButtonBackground;
    }

    public void ShowCalendarHover(CalendarDayRow? row)
    {
        if (row?.Date is null)
        {
            HideCalendarHoverCard();
            return;
        }

        if (!_calendarHoverLinesByDate.TryGetValue(row.Date.Value, out IReadOnlyList<string>? lines) ||
            lines.Count == 0)
        {
            HideCalendarHoverCard();
            return;
        }

        _hoveredCalendarDate = row.Date;
        _viewModel.CalendarHoverTitle = AppTimeFormatter.FormatCalendarHoverTitle(row.Date.Value);
        SetCalendarHoverLines(lines);
        _viewModel.CalendarHoverCardVisibility = Visibility.Visible;
    }

    public void HideCalendarHover(CalendarDayRow? row)
    {
        if (row?.Date is null || _hoveredCalendarDate != row.Date)
        {
            return;
        }

        HideCalendarHoverCard();
    }

    private void RefreshCalendar(
        DateOnly today,
        DateOnly displayedRecordMonth,
        DateTimeOffset observedAt,
        Guid? projectFilter)
    {
        _viewModel.CalendarRows.Clear();

        DateOnly firstDay = new(displayedRecordMonth.Year, displayedRecordMonth.Month, 1);
        DateOnly lastDay = firstDay.AddMonths(1).AddDays(-1);
        int leadingBlankCount = (int)firstDay.DayOfWeek;

        IReadOnlyList<DailyDurationSummary> summaries = _engine.GetDailyDurationSummaries(firstDay, lastDay, observedAt, projectFilter);
        Dictionary<DateOnly, DailyDurationSummary> summaryByDate = summaries.ToDictionary(summary => summary.Date);
        Dictionary<DateOnly, IReadOnlyList<string>> detailByDate = BuildCalendarHoverLinesByDate(
            firstDay,
            lastDay,
            observedAt,
            projectFilter);

        for (int index = 0; index < leadingBlankCount; index++)
        {
            _viewModel.CalendarRows.Add(new CalendarDayRow(null, string.Empty, string.Empty, false, false, true));
        }

        for (DateOnly date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            TimeSpan duration = summaryByDate.GetValueOrDefault(date)?.TotalDuration ?? TimeSpan.Zero;
            _viewModel.CalendarRows.Add(new CalendarDayRow(
                date,
                date.Day.ToString(CultureInfo.CurrentCulture),
                duration == TimeSpan.Zero ? string.Empty : AppTimeFormatter.FormatDurationShort(duration),
                duration > TimeSpan.Zero,
                date == today,
                false));
        }

        _calendarHoverLinesByDate = detailByDate;
        RefreshCalendarHoverCard(detailByDate);
    }

    private void RefreshRecentRecords(Guid? projectFilter)
    {
        _viewModel.RecentRecordRows.Clear();

        foreach (ProjectTimerRecord record in _engine.GetRecentRecords(12, projectFilter))
        {
            string programSummaryText = record.ProgramSummaries.Count == 0
                ? "등록 프로그램 포커스 기록 없음"
                : string.Join(" / ", record.ProgramSummaries.Select(summary =>
                    $"{summary.Program.DisplayName} {AppTimeFormatter.FormatDuration(summary.FocusDuration)}"));

            _viewModel.RecentRecordRows.Add(new RecentRecordRow(
                record.ProjectName,
                $"{AppTimeFormatter.FormatDateTime(record.StartedAt)} ~ {AppTimeFormatter.FormatDateTime(record.EndedAt)}",
                AppTimeFormatter.FormatDuration(record.TotalDuration),
                programSummaryText));
        }
    }

    private Dictionary<DateOnly, IReadOnlyList<string>> BuildCalendarHoverLinesByDate(
        DateOnly firstDay,
        DateOnly lastDay,
        DateTimeOffset observedAt,
        Guid? projectFilter)
    {
        if (projectFilter.HasValue)
        {
            return [];
        }

        return _engine.GetDailyProjectDurationSummaries(firstDay, lastDay, observedAt)
            .GroupBy(summary => summary.Date)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)[.. group
                    .OrderByDescending(static summary => summary.TotalDuration)
                    .ThenBy(static summary => summary.ProjectName, StringComparer.CurrentCultureIgnoreCase)
                    .Select(summary => $"{summary.ProjectName} {AppTimeFormatter.FormatDuration(summary.TotalDuration)}")]);
    }

    private void RefreshCalendarHoverCard(Dictionary<DateOnly, IReadOnlyList<string>> detailByDate)
    {
        if (_hoveredCalendarDate is not { } hoveredDate)
        {
            _viewModel.CalendarHoverCardVisibility = Visibility.Collapsed;
            return;
        }

        if (!detailByDate.TryGetValue(hoveredDate, out IReadOnlyList<string>? lines) ||
            lines.Count == 0)
        {
            HideCalendarHoverCard();
            return;
        }

        _viewModel.CalendarHoverTitle = AppTimeFormatter.FormatCalendarHoverTitle(hoveredDate);
        SetCalendarHoverLines(lines);
        _viewModel.CalendarHoverCardVisibility = Visibility.Visible;
    }

    private void HideCalendarHoverCard()
    {
        _hoveredCalendarDate = null;
        _viewModel.CalendarHoverTitle = string.Empty;
        _viewModel.CalendarHoverLines.Clear();
        _viewModel.CalendarHoverCardVisibility = Visibility.Collapsed;
    }

    private void SetCalendarHoverLines(IEnumerable<string> lines)
    {
        _viewModel.CalendarHoverLines.Clear();
        foreach (string line in lines)
        {
            _viewModel.CalendarHoverLines.Add(line);
        }
    }
}
