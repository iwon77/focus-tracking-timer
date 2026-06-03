using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App;

public sealed record ProjectSidebarRow(
    Guid ProjectId,
    string Name,
    string TotalDurationText,
    string StatusText);

public sealed record RegisteredProgramRow(
    string DisplayName,
    string ProcessName,
    string FocusDurationText,
    string RegisteredAtText = "",
    string InitialDisplayName = "",
    string StatusBrush = "#A7A7A0",
    string StatusText = "실행 중 아님");

public sealed record ProgramSortOption(
    ProgramSortMode Mode,
    string Label);

public sealed record RunningProcessRow(
    string DisplayName,
    string ProcessName);

public sealed record RecordFilterOption(
    Guid? ProjectId,
    string Label);

public sealed record CalendarDayRow(
    string DayText,
    string DurationText,
    bool HasDuration,
    bool IsToday,
    bool IsPlaceholder);

public sealed record RecentRecordRow(
    string ProjectName,
    string PeriodText,
    string TotalDurationText,
    string ProgramSummaryText);
