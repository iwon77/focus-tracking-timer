namespace FocusTrackingTimer.App;

public sealed record RunningProcessRow(
    string DisplayName,
    string ProcessName,
    int ProcessId,
    nint WindowHandle = 0,
    bool IsFirst = false,
    bool IsLast = false);
