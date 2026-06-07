namespace FocusTrackingTimer.App;

public sealed record ProjectSidebarRow(
    Guid ProjectId,
    string Name,
    bool IsActive)
{
    public string BorderBrush => IsActive ? "#2EAD62" : "#D7D7D0";
}
