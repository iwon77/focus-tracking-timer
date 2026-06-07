namespace FocusTrackingTimer.App;

public sealed record RegisteredProgramRow(
    string DisplayName,
    string ProcessName,
    string FocusDurationText,
    string RegisteredAtText = "",
    string InitialDisplayName = "",
    string StatusBrush = "#A7A7A0",
    string StatusText = "실행 중 아님");
