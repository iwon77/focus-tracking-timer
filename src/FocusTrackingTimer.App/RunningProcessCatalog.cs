using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App;

internal static class RunningProcessCatalog
{
    private const int ExtendedWindowStyleIndex = -20;
    private const long ExtendedToolWindowStyle = 0x00000080L;
    private const long ExtendedNoActivateStyle = 0x08000000L;

    private static readonly HashSet<string> ExcludedVisibleProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApplicationFrameHost",
        "explorer",
        "GameBar",
        "GameBarFTServer",
        "NVIDIA Overlay",
        "NVIDIA Share",
        "nvcontainer",
        "RuntimeBroker",
        "SearchHost",
        "ShellExperienceHost",
        "StartMenuExperienceHost",
        "SystemSettings",
        "TextInputHost"
    };

    public static IReadOnlyDictionary<string, ProcessRunState> GetProcessRunStates(int currentProcessId)
    {
        return MeasureProcessRunStates(currentProcessId).ProcessStates;
    }

    public static ProcessRunStateScanResult MeasureProcessRunStates(int currentProcessId)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Dictionary<string, ProcessRunState> processStates = new(StringComparer.OrdinalIgnoreCase);
        int exceptionCount = 0;

        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == currentProcessId)
                {
                    continue;
                }

                string processName = process.ProcessName;
                bool hasFocusableWindow = IsFocusTrackingCandidate(process);

                if (processStates.TryGetValue(processName, out ProcessRunState? current))
                {
                    processStates[processName] = current with
                    {
                        HasFocusableWindow = current.HasFocusableWindow || hasFocusableWindow
                    };
                    continue;
                }

                processStates[processName] = new ProcessRunState(processName, hasFocusableWindow);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
            {
                exceptionCount++;
                continue;
            }
            finally
            {
                process.Dispose();
            }
        }

        stopwatch.Stop();
        return new ProcessRunStateScanResult(processStates, stopwatch.Elapsed, exceptionCount);
    }

    public static IReadOnlyList<RunningProcessRow> GetVisibleProcesses(int currentProcessId)
    {
        List<RunningProcessRow> applications = [];

        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == currentProcessId ||
                    !IsFocusTrackingCandidate(process) ||
                    string.IsNullOrWhiteSpace(process.MainWindowTitle))
                {
                    continue;
                }

                applications.Add(new RunningProcessRow(
                    process.MainWindowTitle.Trim(),
                    process.ProcessName,
                    process.Id));
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
            {
                continue;
            }
            finally
            {
                process.Dispose();
            }
        }

        return [.. applications
            .OrderBy(static item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(static item => item.ProcessId)];
    }

    private static string? GetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static bool IsFocusTrackingCandidate(Process process)
    {
        if (ExcludedVisibleProcessNames.Contains(process.ProcessName) ||
            !HasMeasurableFocusWindow(process))
        {
            return false;
        }

        string? executablePath = GetExecutablePath(process);

        return !IsSystemProcessPath(executablePath);
    }

    private static bool IsSystemProcessPath(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(executablePath);
        string windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        return IsPathUnder(fullPath, windowsPath);
    }

    private static bool IsPathUnder(string path, string parentPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            return false;
        }

        string normalizedPath = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedParentPath = Path.GetFullPath(parentPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalizedPath.Equals(normalizedParentPath, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(
                normalizedParentPath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasMeasurableFocusWindow(Process process)
    {
        IntPtr windowHandle = process.MainWindowHandle;

        if (windowHandle == IntPtr.Zero ||
            !IsWindowVisible(windowHandle) ||
            !IsWindowEnabled(windowHandle))
        {
            return false;
        }

        long extendedStyle = GetWindowLongPtr(windowHandle, ExtendedWindowStyleIndex).ToInt64();

        return (extendedStyle & ExtendedToolWindowStyle) == 0 &&
            (extendedStyle & ExtendedNoActivateStyle) == 0;
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool IsWindowEnabled(IntPtr windowHandle);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);
}

internal sealed record ProcessRunState(
    string ProcessName,
    bool HasFocusableWindow);

internal sealed record ProcessRunStateScanResult(
    IReadOnlyDictionary<string, ProcessRunState> ProcessStates,
    TimeSpan Elapsed,
    int ExceptionCount);
