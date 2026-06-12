namespace FocusTrackingTimer.App;

public sealed record ProjectSidebarRow(
    Guid ProjectId,
    string Name,
    bool IsPinned,
    bool IsSelected);
