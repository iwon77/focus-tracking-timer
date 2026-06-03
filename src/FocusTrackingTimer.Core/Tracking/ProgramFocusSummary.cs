namespace FocusTrackingTimer.Core.Tracking;

public sealed record ProgramFocusSummary(
    TrackedApplication Program,
    TimeSpan FocusDuration);
