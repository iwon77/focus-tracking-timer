namespace FocusTrackingTimer.Core.Tracking;

public sealed record TrackedApplication
{
    public TrackedApplication(string processName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            throw new ArgumentException("Process name is required.", nameof(processName));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        ProcessName = processName.Trim();
        DisplayName = displayName.Trim();
    }

    public string ProcessName { get; }

    public string DisplayName { get; }
}
