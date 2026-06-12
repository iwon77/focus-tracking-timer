using System.Globalization;
using FocusTrackingTimer.App.Infrastructure;
using FocusTrackingTimer.App.ViewModels;
using FocusTrackingTimer.Core.Persistence;
using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App.Features.DailyRecords;

internal sealed class DailyRecordFeatureController
{
    private readonly ProjectTimerEngine _engine;
    private readonly SqliteProjectTimerStore _store;
    private readonly DailyRecordViewModel _viewModel;
    private DateOnly _displayedRecordMonth = new(DateTime.Now.Year, DateTime.Now.Month, 1);
    private DateOnly? _selectedDate;

    public DailyRecordFeatureController(
        ProjectTimerEngine engine,
        SqliteProjectTimerStore store,
        DailyRecordViewModel viewModel)
    {
        _engine = engine;
        _store = store;
        _viewModel = viewModel;
        _viewModel.DisplayedRecordMonthText = AppTimeFormatter.FormatRecordMonth(_displayedRecordMonth);
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
        _viewModel.RecordFilterOptions.Add(new RecordFilterOption(null, "전체 작업"));
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
        _viewModel.DisplayedRecordMonthText = AppTimeFormatter.FormatRecordMonth(_displayedRecordMonth);

        DateOnly today = DateOnly.FromDateTime(observedAt.LocalDateTime.Date);
        DateOnly firstDay = new(_displayedRecordMonth.Year, _displayedRecordMonth.Month, 1);
        DateOnly lastDay = firstDay.AddMonths(1).AddDays(-1);
        IReadOnlyList<DailyDurationSummary> monthlyDailySummaries = LoadDailyDurationSummaries(firstDay, lastDay, observedAt, projectFilter);
        IReadOnlyList<ProjectTimerRecordSlice> monthlySlices = LoadRecordSlices(firstDay, lastDay, observedAt, projectFilter);

        RefreshMonthlySummary(monthlyDailySummaries, monthlySlices);
        EnsureSelectedDate(today);
        RefreshCalendar(firstDay, lastDay, today, monthlyDailySummaries);
        RefreshSelectedDateSummary(observedAt, projectFilter);
    }

    public void SelectDate(CalendarDayRow? row)
    {
        if (row?.Date is null || row.IsPlaceholder)
        {
            return;
        }

        _selectedDate = row.Date.Value;
        RefreshRecordArea(DateTimeOffset.Now);
    }

    private void RefreshMonthlySummary(
        IReadOnlyList<DailyDurationSummary> dailySummaries,
        IReadOnlyList<ProjectTimerRecordSlice> monthlySlices)
    {
        TimeSpan monthlyFocusDuration = dailySummaries.Aggregate(TimeSpan.Zero, static (total, summary) => total + summary.TotalDuration);
        TimeSpan monthlyWallClockDuration = monthlySlices.Aggregate(TimeSpan.Zero, static (total, slice) => total + slice.WallClockDuration);
        int workedDayCount = dailySummaries.Count(static summary => summary.TotalDuration > TimeSpan.Zero);

        _viewModel.MonthlyWorkedDayCountText = $"{workedDayCount}일";
        _viewModel.MonthlyTotalWallClockDurationText = AppTimeFormatter.FormatDuration(monthlyWallClockDuration);
        _viewModel.MonthlyTotalFocusDurationText = AppTimeFormatter.FormatDuration(monthlyFocusDuration);
        _viewModel.MonthlyAverageWallClockDurationText = workedDayCount == 0
            ? "00:00:00"
            : AppTimeFormatter.FormatDuration(TimeSpan.FromTicks(monthlyWallClockDuration.Ticks / workedDayCount));
        _viewModel.MonthlyAverageFocusDurationText = workedDayCount == 0
            ? "00:00:00"
            : AppTimeFormatter.FormatDuration(TimeSpan.FromTicks(monthlyFocusDuration.Ticks / workedDayCount));
    }

    private void EnsureSelectedDate(DateOnly today)
    {
        DateOnly monthStart = new(_displayedRecordMonth.Year, _displayedRecordMonth.Month, 1);
        DateOnly monthEnd = monthStart.AddMonths(1).AddDays(-1);

        if (_selectedDate is { } selectedDate &&
            selectedDate >= monthStart &&
            selectedDate <= monthEnd)
        {
            return;
        }

        _selectedDate = today >= monthStart && today <= monthEnd ? today : monthStart;
    }

    private void RefreshCalendar(
        DateOnly firstDay,
        DateOnly lastDay,
        DateOnly today,
        IReadOnlyList<DailyDurationSummary> summaries)
    {
        _viewModel.CalendarRows.Clear();

        int leadingBlankCount = (int)firstDay.DayOfWeek;
        Dictionary<DateOnly, DailyDurationSummary> summaryByDate = summaries.ToDictionary(summary => summary.Date);

        for (int index = 0; index < leadingBlankCount; index++)
        {
            _viewModel.CalendarRows.Add(new CalendarDayRow(null, string.Empty, string.Empty, false, false, false, true, false, 0));
        }

        for (DateOnly date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            TimeSpan duration = summaryByDate.GetValueOrDefault(date)?.TotalDuration ?? TimeSpan.Zero;
            bool isSelected = _selectedDate == date;
            _viewModel.CalendarRows.Add(new CalendarDayRow(
                date,
                date.Day.ToString(CultureInfo.CurrentCulture),
                duration == TimeSpan.Zero ? string.Empty : AppTimeFormatter.FormatDuration(duration),
                duration > TimeSpan.Zero,
                date == today,
                date.DayOfWeek == DayOfWeek.Sunday,
                false,
                isSelected,
                GetDotSize(duration)));
        }

        while (_viewModel.CalendarRows.Count < 42)
        {
            _viewModel.CalendarRows.Add(new CalendarDayRow(null, string.Empty, string.Empty, false, false, false, true, false, 0));
        }
    }

    private static double GetDotSize(TimeSpan duration)
    {
        double minutes = duration.TotalMinutes;
        if (minutes <= 0)
        {
            return 0;
        }

        if (minutes <= 30)
        {
            return 12;
        }

        if (minutes <= 60)
        {
            return 15;
        }

        if (minutes <= 120)
        {
            return 18;
        }

        if (minutes <= 240)
        {
            return 21;
        }

        return 24;
    }

    private void RefreshSelectedDateSummary(DateTimeOffset observedAt, Guid? projectFilter)
    {
        DateOnly selectedDate = _selectedDate ?? DateOnly.FromDateTime(observedAt.LocalDateTime.Date);
        List<ProjectTimerRecordSlice> slices = LoadRecordSlices(selectedDate, selectedDate, observedAt, projectFilter);
        TimeSpan totalWallClock = slices.Aggregate(TimeSpan.Zero, static (total, slice) => total + slice.WallClockDuration);
        TimeSpan totalFocusDuration = slices.Aggregate(TimeSpan.Zero, static (total, slice) => total + slice.TotalDuration);
        double focusRatio = totalWallClock <= TimeSpan.Zero
            ? 0
            : totalFocusDuration.TotalSeconds / totalWallClock.TotalSeconds;

        _viewModel.SelectedDailyDateText = AppTimeFormatter.FormatCalendarDate(selectedDate);
        _viewModel.SelectedDailyRecordCountText = $"{slices.Count}건";
        _viewModel.SelectedDailyTotalWallClockDurationText = AppTimeFormatter.FormatDuration(totalWallClock);
        _viewModel.SelectedDailyFocusSummaryText =
            $"{AppTimeFormatter.FormatDuration(totalFocusDuration)} ({AppTimeFormatter.FormatPercentage(focusRatio)})";

        _viewModel.SelectedDailyRecordRows.Clear();
        foreach (ProjectTimerRecordSlice slice in slices.OrderBy(static item => item.StartedAt))
        {
            _viewModel.SelectedDailyRecordRows.Add(new DailyRecordItemRow(
                slice.ProjectName,
                AppTimeFormatter.FormatDuration(slice.TotalDuration),
                AppTimeFormatter.FormatTimeRange(slice.StartedAt, slice.EndedAt)));
        }

        _viewModel.SelectedDailyEmptyText = slices.Count == 0
            ? "선택한 날짜의 작업 기록이 없습니다."
            : string.Empty;
    }

    private List<ProjectTimerRecordSlice> LoadRecordSlices(
        DateOnly fromDate,
        DateOnly toDate,
        DateTimeOffset observedAt,
        Guid? projectFilter)
    {
        List<ProjectTimerRecordSlice> slices =
        [
            .. _store.LoadRecordSlices(fromDate, toDate, projectFilter),
            .. _engine.GetActiveRecordSlices(fromDate, toDate, observedAt, projectFilter)
        ];
        return slices;
    }

    private List<DailyDurationSummary> LoadDailyDurationSummaries(
        DateOnly fromDate,
        DateOnly toDate,
        DateTimeOffset observedAt,
        Guid? projectFilter)
    {
        Dictionary<DateOnly, TimeSpan> totalsByDate = _store
            .LoadDailyDurationSummaries(fromDate, toDate, projectFilter)
            .ToDictionary(summary => summary.Date, summary => summary.TotalDuration);

        foreach (DailyDurationSummary summary in _engine.GetActiveDailyDurationSummaries(fromDate, toDate, observedAt, projectFilter))
        {
            totalsByDate[summary.Date] = totalsByDate.GetValueOrDefault(summary.Date, TimeSpan.Zero) + summary.TotalDuration;
        }

        List<DailyDurationSummary> summaries = [];
        for (DateOnly date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            summaries.Add(new DailyDurationSummary(date, totalsByDate.GetValueOrDefault(date, TimeSpan.Zero)));
        }

        return summaries;
    }
}
