namespace FocusTrackingTimer.App;

public sealed record ProjectSidebarRow(
    Guid ProjectId,
    string Name,
    string TotalDurationText,
    string StatusText);
