namespace FocusTrackingTimer.App;

public sealed record RunningProcessRow(
    string DisplayName,
    string ProcessName,
    int ProcessId,
    bool IsFirst = false,
    bool IsLast = false);
