namespace FocusTrackingTimer.App;

public sealed record WeeklyRecordRow(
    Guid ProjectId,
    DateOnly RecordDate,
    string GroupDateText,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string DateText,
    string ProjectName,
    string FocusDurationText,
    string TotalDurationText,
    string PeriodText,
    string FocusRatioText,
    IReadOnlyList<ProgramDurationRow> ProgramRows);
