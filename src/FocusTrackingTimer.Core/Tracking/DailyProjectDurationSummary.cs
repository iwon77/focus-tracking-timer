namespace FocusTrackingTimer.Core.Tracking;

public sealed record DailyProjectDurationSummary(
    DateOnly Date,
    Guid ProjectId,
    string ProjectName,
    TimeSpan TotalDuration);
