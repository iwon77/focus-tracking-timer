namespace FocusTrackingTimer.Core.Tracking;

public sealed record RegisteredProgramInfo(
    TrackedApplication Program,
    DateTimeOffset RegisteredAt,
    string InitialDisplayName);
