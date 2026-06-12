using System.Globalization;
using System.Windows;
using FocusTrackingTimer.App.Infrastructure;
using FocusTrackingTimer.App.ViewModels;
using FocusTrackingTimer.Core.Persistence;
using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App.Features.WeeklyRecords;

internal sealed class WeeklyRecordFeatureController
{
    private readonly ProjectTimerEngine _engine;
    private readonly SqliteProjectTimerStore _store;
    private readonly WeeklyRecordViewModel _viewModel;
    private DateOnly _displayedWeekStart = GetWeekStart(DateOnly.FromDateTime(DateTime.Now.Date));
    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Now.Date);
    private (Guid ProjectId, DateTimeOffset StartedAt, DateTimeOffset EndedAt)? _selectedRecordKey;
    private bool _isRefreshingRows;

    public WeeklyRecordFeatureController(
        ProjectTimerEngine engine,
        SqliteProjectTimerStore store,
        WeeklyRecordViewModel viewModel)
    {
        _engine = engine;
        _store = store;
        _viewModel = viewModel;
    }

    public void MoveDisplayedWeek(int weekOffset)
    {
        DateOnly targetWeekStart = _displayedWeekStart.AddDays(weekOffset * 7);
        DateOnly targetWeekEnd = targetWeekStart.AddDays(6);
        Guid? projectFilter = _viewModel.SelectedRecordFilter?.ProjectId;
        if (!HasAnyRecordsInRange(targetWeekStart, targetWeekEnd, DateTimeOffset.Now, projectFilter))
        {
            return;
        }

        _displayedWeekStart = targetWeekStart;
        AlignSelectedDateToDisplayedWeek();
        RefreshWeeklyRecordArea(DateTimeOffset.Now);
    }

    public void MoveDisplayedWeekToCurrent()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Now.Date);
        _displayedWeekStart = GetWeekStart(today);
        _selectedDate = today;
        _selectedRecordKey = null;
        RefreshWeeklyRecordArea(DateTimeOffset.Now);
    }

    public void RefreshRecordFilters()
    {
        Guid? selectedFilterProjectId = _viewModel.SelectedRecordFilter?.ProjectId;

        _viewModel.RecordFilterOptions.Clear();
        _viewModel.RecordFilterOptions.Add(new RecordFilterOption(null, "?꾩껜 ?묒뾽"));
        foreach (ProjectDefinition project in _engine.Projects.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            _viewModel.RecordFilterOptions.Add(new RecordFilterOption(project.Id, project.Name));
        }

        _viewModel.SelectedRecordFilter = _viewModel.RecordFilterOptions.FirstOrDefault(option => option.ProjectId == selectedFilterProjectId)
            ?? _viewModel.RecordFilterOptions[0];
    }

    public void RefreshWeeklyRecordArea(DateTimeOffset observedAt)
    {
        Guid? projectFilter = _viewModel.SelectedRecordFilter?.ProjectId;
        DateOnly weekEnd = _displayedWeekStart.AddDays(6);
        AlignSelectedDateToDisplayedWeek();

        IReadOnlyList<ProjectTimerRecordSlice> weeklySlices = LoadRecordSlices(_displayedWeekStart, weekEnd, observedAt, projectFilter);
        List<ProjectTimerRecordSlice> orderedWeeklySlices = [.. weeklySlices
            .OrderBy(static slice => slice.StartedAt)
            .ThenBy(static slice => slice.EndedAt)];
        Dictionary<DateOnly, (TimeSpan WallClock, TimeSpan Focus)> totalsByDate = orderedWeeklySlices
            .GroupBy(static slice => DateOnly.FromDateTime(slice.StartedAt.LocalDateTime.Date))
            .ToDictionary(
                static group => group.Key,
                static group => (
                    group.Aggregate(TimeSpan.Zero, static (total, slice) => total + slice.WallClockDuration),
                    group.Aggregate(TimeSpan.Zero, static (total, slice) => total + slice.TotalDuration)));

        EnsureSelectedDateHasRecords(orderedWeeklySlices);

        _viewModel.DisplayedWeekRangeText = AppTimeFormatter.FormatWeekRange(_displayedWeekStart, weekEnd);
        _viewModel.DisplayedWeekLabelText = AppTimeFormatter.FormatWeekOfMonthLabel(DateOnly.FromDateTime(DateTime.Now.Date));

        TimeSpan totalFocusDuration = orderedWeeklySlices.Aggregate(TimeSpan.Zero, static (total, slice) => total + slice.TotalDuration);
        TimeSpan totalWallClockDuration = orderedWeeklySlices.Aggregate(TimeSpan.Zero, static (total, slice) => total + slice.WallClockDuration);

        _viewModel.WeekTotalFocusDurationText = AppTimeFormatter.FormatDuration(totalFocusDuration);
        _viewModel.WeekTotalWallClockDurationText = AppTimeFormatter.FormatDuration(totalWallClockDuration);
        _viewModel.WeeklyRecordCountText = $"{orderedWeeklySlices.Count}건";
        _viewModel.AverageDailyWallClockDurationText = AppTimeFormatter.FormatDuration(TimeSpan.FromTicks(totalWallClockDuration.Ticks / 7));
        _viewModel.AverageDailyFocusDurationText = AppTimeFormatter.FormatDuration(TimeSpan.FromTicks(totalFocusDuration.Ticks / 7));

        _isRefreshingRows = true;
        try
        {
            _viewModel.WeeklyRecordRows.Clear();
            foreach (ProjectTimerRecordSlice slice in orderedWeeklySlices)
            {
                TimeSpan wallClock = slice.WallClockDuration;
                double focusRatio = wallClock <= TimeSpan.Zero ? 0 : slice.TotalDuration.TotalSeconds / wallClock.TotalSeconds;
                DateOnly recordDate = DateOnly.FromDateTime(slice.StartedAt.LocalDateTime.Date);
                (TimeSpan groupWallClock, TimeSpan groupFocus) = totalsByDate[recordDate];

                _viewModel.WeeklyRecordRows.Add(new WeeklyRecordRow(
                    slice.ProjectId,
                    recordDate,
                    AppTimeFormatter.FormatGroupDate(recordDate),
                    AppTimeFormatter.FormatDuration(groupWallClock),
                    AppTimeFormatter.FormatDuration(groupFocus),
                    slice.StartedAt,
                    slice.EndedAt,
                    AppTimeFormatter.FormatDayLabel(recordDate),
                    slice.ProjectName,
                    AppTimeFormatter.FormatDuration(slice.TotalDuration),
                    AppTimeFormatter.FormatDuration(slice.WallClockDuration),
                    AppTimeFormatter.FormatTimeRange(slice.StartedAt, slice.EndedAt),
                    AppTimeFormatter.FormatPercentage(focusRatio),
                    [.. slice.ProgramSummaries.Select(summary => new ProgramDurationRow(
                        summary.Program.DisplayName,
                        summary.Program.ProcessName,
                        AppTimeFormatter.FormatDuration(summary.FocusDuration)))]));
            }
        }
        finally
        {
            _isRefreshingRows = false;
        }

        WeeklyRecordRow? selectedRow = ResolveSelectedRow();
        _viewModel.SelectedWeeklyRecordRow = selectedRow;
        RefreshSelectedRecordDetails(selectedRow);
        RefreshWeeklyDayBubbles(observedAt, projectFilter);
    }

    public void SelectRecord(WeeklyRecordRow? row)
    {
        if (_isRefreshingRows && row is null)
        {
            return;
        }

        _viewModel.SelectedWeeklyRecordRow = row;
        _selectedRecordKey = row is null ? null : (row.ProjectId, row.StartedAt, row.EndedAt);
        if (row is not null)
        {
            _selectedDate = row.RecordDate;
        }

        RefreshSelectedRecordDetails(row);
        RefreshWeeklyDayBubbles(DateTimeOffset.Now, _viewModel.SelectedRecordFilter?.ProjectId);
    }

    public void SelectSummaryDay(WeeklyDayBubbleRow? row)
    {
        if (row is null || !row.HasBubble)
        {
            return;
        }

        _selectedDate = row.Date;
        _selectedRecordKey = null;
        RefreshWeeklyRecordArea(DateTimeOffset.Now);
    }

    private WeeklyRecordRow? ResolveSelectedRow()
    {
        if (_selectedRecordKey is { } key)
        {
            WeeklyRecordRow? matched = _viewModel.WeeklyRecordRows.FirstOrDefault(row =>
                row.ProjectId == key.ProjectId &&
                row.StartedAt == key.StartedAt &&
                row.EndedAt == key.EndedAt);

            if (matched is not null)
            {
                return matched;
            }
        }

        WeeklyRecordRow? selectedDateRow = _viewModel.WeeklyRecordRows.FirstOrDefault(row => row.RecordDate == _selectedDate);
        if (selectedDateRow is not null)
        {
            _selectedRecordKey = (selectedDateRow.ProjectId, selectedDateRow.StartedAt, selectedDateRow.EndedAt);
            return selectedDateRow;
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.Now.Date);
        DateOnly displayedWeekEnd = _displayedWeekStart.AddDays(6);
        if (_selectedDate == today &&
            today >= _displayedWeekStart &&
            today <= displayedWeekEnd)
        {
            _selectedRecordKey = null;
            return null;
        }

        WeeklyRecordRow? firstRow = _viewModel.WeeklyRecordRows.FirstOrDefault();
        _selectedRecordKey = firstRow is null ? null : (firstRow.ProjectId, firstRow.StartedAt, firstRow.EndedAt);
        return firstRow;
    }

    private void RefreshSelectedRecordDetails(WeeklyRecordRow? row)
    {
        _viewModel.SelectedRecordProgramRows.Clear();

        if (row is null)
        {
            _viewModel.SelectedRecordTitle = "?좏깮???묒뾽???놁뒿?덈떎.";
            _viewModel.SelectedRecordSubtitle = AppTimeFormatter.FormatGroupDate(_selectedDate);
            _viewModel.SelectedRecordTotalDurationText = "00:00:00";
            _viewModel.SelectedRecordFocusDurationText = "00:00:00";
            _viewModel.SelectedRecordFocusRatioText = "0%";
            _viewModel.SelectedRecordEmptyText = "?좏깮???좎쭨???묒뾽 湲곕줉???놁뒿?덈떎.";
            _viewModel.SelectedRecordEmptyVisibility = Visibility.Visible;
            _viewModel.SelectedRecordDetailVisibility = Visibility.Collapsed;
            return;
        }

        _viewModel.SelectedRecordTitle = row.ProjectName;
        _viewModel.SelectedRecordSubtitle = $"{row.DateText} {row.PeriodText}";
        _viewModel.SelectedRecordTotalDurationText = row.TotalDurationText;
        _viewModel.SelectedRecordFocusDurationText = row.FocusDurationText;
        _viewModel.SelectedRecordFocusRatioText = row.FocusRatioText;

        foreach (ProgramDurationRow program in row.ProgramRows)
        {
            _viewModel.SelectedRecordProgramRows.Add(program);
        }

        _viewModel.SelectedRecordEmptyText = row.ProgramRows.Count == 0
            ? "기록된 집중 시간이 없습니다."
            : string.Empty;
        _viewModel.SelectedRecordEmptyVisibility = row.ProgramRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _viewModel.SelectedRecordDetailVisibility = row.ProgramRows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RefreshWeeklyDayBubbles(DateTimeOffset observedAt, Guid? projectFilter)
    {
        DateOnly weekEnd = _displayedWeekStart.AddDays(6);
        IReadOnlyList<DailyDurationSummary> summaries = LoadDailyDurationSummaries(_displayedWeekStart, weekEnd, observedAt, projectFilter);
        Dictionary<DateOnly, TimeSpan> durationsByDate = summaries.ToDictionary(summary => summary.Date, summary => summary.TotalDuration);

        _viewModel.WeeklyDayBubbleRows.Clear();
        for (DateOnly date = _displayedWeekStart; date <= weekEnd; date = date.AddDays(1))
        {
            TimeSpan duration = durationsByDate.GetValueOrDefault(date, TimeSpan.Zero);
            bool hasBubble = duration > TimeSpan.Zero;
            bool isSunday = date.DayOfWeek == DayOfWeek.Sunday;

            _viewModel.WeeklyDayBubbleRows.Add(new WeeklyDayBubbleRow(
                date,
                AppTimeFormatter.FormatWeekDayName(date),
                isSunday || _selectedDate == date
                    ? AppTimeFormatter.FormatWeeklyBubbleDate(date)
                    : date.Day.ToString(CultureInfo.CurrentCulture),
                GetBubbleDiameter(duration),
                hasBubble,
                _selectedDate == date,
                isSunday,
                hasBubble ? Visibility.Visible : Visibility.Collapsed));
        }
    }

    private void AlignSelectedDateToDisplayedWeek()
    {
        DateOnly weekEnd = _displayedWeekStart.AddDays(6);
        if (_selectedDate >= _displayedWeekStart && _selectedDate <= weekEnd)
        {
            return;
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.Now.Date);
        _selectedDate = today >= _displayedWeekStart && today <= weekEnd
            ? today
            : _displayedWeekStart;
        _selectedRecordKey = null;
    }

    private void EnsureSelectedDateHasRecords(IEnumerable<ProjectTimerRecordSlice> weeklySlices)
    {
        List<DateOnly> availableDates = [.. weeklySlices
            .Select(slice => DateOnly.FromDateTime(slice.StartedAt.LocalDateTime.Date))
            .Distinct()
            .OrderBy(static date => date)];

        if (availableDates.Count == 0)
        {
            _selectedRecordKey = null;
            return;
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.Now.Date);
        DateOnly weekEnd = _displayedWeekStart.AddDays(6);
        if (_selectedDate == today &&
            today >= _displayedWeekStart &&
            today <= weekEnd)
        {
            return;
        }

        if (availableDates.Contains(_selectedDate))
        {
            return;
        }

        _selectedDate = availableDates[0];
        _selectedRecordKey = null;
    }

    private static double GetBubbleDiameter(TimeSpan duration)
    {
        double minutes = duration.TotalMinutes;
        if (minutes <= 0)
        {
            return 0;
        }

        if (minutes <= 30)
        {
            return 8;
        }

        if (minutes <= 60)
        {
            return 11;
        }

        if (minutes <= 120)
        {
            return 14;
        }

        if (minutes <= 240)
        {
            return 17;
        }

        return 20;
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        int offset = (7 + (date.DayOfWeek - DayOfWeek.Sunday)) % 7;
        return date.AddDays(-offset);
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

    private bool HasAnyRecordsInRange(
        DateOnly fromDate,
        DateOnly toDate,
        DateTimeOffset observedAt,
        Guid? projectFilter)
    {
        return LoadRecordSlices(fromDate, toDate, observedAt, projectFilter).Count > 0;
    }
}
