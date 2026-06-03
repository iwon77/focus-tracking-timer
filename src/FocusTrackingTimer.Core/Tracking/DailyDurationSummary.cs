namespace FocusTrackingTimer.Core.Tracking;

public sealed record DailyDurationSummary(
    DateOnly Date,
    TimeSpan TotalDuration);
