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

    public static TrackedApplication FromExecutableInput(string executableInput, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(executableInput))
        {
            throw new ArgumentException("Executable input is required.", nameof(executableInput));
        }

        string normalizedInput = executableInput.Trim();
        string fileName = Path.GetFileName(normalizedInput);
        string normalizedProcessName = Path.GetFileNameWithoutExtension(fileName);

        if (string.IsNullOrWhiteSpace(normalizedProcessName))
        {
            normalizedProcessName = normalizedInput.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? normalizedInput[..^4]
                : normalizedInput;
        }

        string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? normalizedProcessName
            : displayName.Trim();

        return new TrackedApplication(normalizedProcessName, resolvedDisplayName);
    }
}
