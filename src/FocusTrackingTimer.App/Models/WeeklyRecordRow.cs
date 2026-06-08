namespace FocusTrackingTimer.App;

public sealed record WeeklyRecordRow(
    Guid ProjectId,
    DateOnly RecordDate,
    string GroupDateText,
    string GroupTotalDurationText,
    string GroupFocusDurationText,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string DateText,
    string ProjectName,
    string FocusDurationText,
    string TotalDurationText,
    string PeriodText,
    string FocusRatioText,
    IReadOnlyList<ProgramDurationRow> ProgramRows);
