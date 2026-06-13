using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App;

internal static class RunningProcessCatalog
{
    private const int ExtendedWindowStyleIndex = -20;
    private const int DwmWindowAttributeCloaked = 14;
    private const int SuccessHResult = 0;
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
        ProcessCatalogSnapshot snapshot = BuildProcessCatalogSnapshot(currentProcessId);
        Dictionary<string, ProcessRunState> processStates = snapshot.ProcessNames
            .ToDictionary(
                static processName => processName,
                static processName => new ProcessRunState(processName, false),
                StringComparer.OrdinalIgnoreCase);

        foreach (string processName in EnumerateRepresentativeWindows(
            snapshot.EligibleProcessesById,
            requireDisplayTitle: false).Keys)
        {
            processStates[processName] = new ProcessRunState(processName, true);
        }

        stopwatch.Stop();
        return new ProcessRunStateScanResult(processStates, stopwatch.Elapsed, snapshot.ExceptionCount);
    }

    public static IReadOnlyList<RunningProcessRow> GetVisibleProcesses(int currentProcessId)
    {
        ProcessCatalogSnapshot snapshot = BuildProcessCatalogSnapshot(currentProcessId);

        return [.. EnumerateRepresentativeWindows(snapshot.EligibleProcessesById, requireDisplayTitle: true)
            .Values
            .OrderBy(static item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(static item => item.ProcessId)
            .Select(static item => new RunningProcessRow(
                item.DisplayName,
                item.ProcessName,
                item.ProcessId,
                item.WindowHandle))];
    }

    public static bool TryGetRepresentativeWindowHandle(string processName, out IntPtr windowHandle)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            windowHandle = IntPtr.Zero;
            return false;
        }

        ProcessCatalogSnapshot snapshot = BuildProcessCatalogSnapshot(currentProcessId: null);
        bool exists = EnumerateRepresentativeWindows(snapshot.EligibleProcessesById, requireDisplayTitle: false)
            .TryGetValue(processName.Trim(), out FocusableWindowCandidate? candidate);

        windowHandle = exists ? candidate!.WindowHandle : IntPtr.Zero;
        return exists;
    }

    private static ProcessCatalogSnapshot BuildProcessCatalogSnapshot(int? currentProcessId)
    {
        HashSet<string> processNames = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<int, EligibleProcessInfo> eligibleProcessesById = [];
        int exceptionCount = 0;

        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                int processId = process.Id;
                if (currentProcessId.HasValue && processId == currentProcessId.Value)
                {
                    continue;
                }

                string? processName = ProcessIdentityResolver.TryGetProcessName(process);
                if (string.IsNullOrWhiteSpace(processName))
                {
                    exceptionCount++;
                    continue;
                }

                processNames.Add(processName);

                if (!IsFocusTrackingCandidate(processName))
                {
                    continue;
                }

                _ = eligibleProcessesById.TryAdd(
                    processId,
                    new EligibleProcessInfo(processId, processName));
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

        return new ProcessCatalogSnapshot(processNames, eligibleProcessesById, exceptionCount);
    }

    private static Dictionary<string, FocusableWindowCandidate> EnumerateRepresentativeWindows(
        IReadOnlyDictionary<int, EligibleProcessInfo> eligibleProcessesById,
        bool requireDisplayTitle)
    {
        Dictionary<string, FocusableWindowCandidate> windowsByProcessName = new(StringComparer.OrdinalIgnoreCase);

        _ = EnumWindows((windowHandle, _) =>
        {
            if (!TryCreateFocusableWindowCandidate(
                windowHandle,
                eligibleProcessesById,
                requireDisplayTitle,
                out FocusableWindowCandidate? candidate))
            {
                return true;
            }

            FocusableWindowCandidate windowCandidate = candidate!;
            if (!windowsByProcessName.TryGetValue(windowCandidate.ProcessName, out FocusableWindowCandidate? current) ||
                ShouldReplaceRepresentativeWindow(current, windowCandidate))
            {
                windowsByProcessName[windowCandidate.ProcessName] = windowCandidate;
            }

            return true;
        }, IntPtr.Zero);

        return windowsByProcessName;
    }

    private static bool TryCreateFocusableWindowCandidate(
        IntPtr windowHandle,
        IReadOnlyDictionary<int, EligibleProcessInfo> eligibleProcessesById,
        bool requireDisplayTitle,
        out FocusableWindowCandidate? candidate)
    {
        candidate = null;

        if (!HasMeasurableFocusWindow(windowHandle))
        {
            return false;
        }

        _ = GetWindowThreadProcessId(windowHandle, out uint processId);
        if (processId == 0 ||
            !eligibleProcessesById.TryGetValue((int)processId, out EligibleProcessInfo? processInfo))
        {
            return false;
        }

        string? displayName = GetWindowTitle(windowHandle);
        if (requireDisplayTitle && string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        candidate = new FocusableWindowCandidate(
            string.IsNullOrWhiteSpace(displayName) ? processInfo.ProcessName : displayName.Trim(),
            processInfo.ProcessName,
            processInfo.ProcessId,
            windowHandle,
            IsIconic(windowHandle));
        return true;
    }

    private static bool ShouldReplaceRepresentativeWindow(
        FocusableWindowCandidate current,
        FocusableWindowCandidate candidate)
    {
        if (current.IsMinimized != candidate.IsMinimized)
        {
            return current.IsMinimized && !candidate.IsMinimized;
        }

        bool currentHasTitle = !string.IsNullOrWhiteSpace(current.DisplayName);
        bool candidateHasTitle = !string.IsNullOrWhiteSpace(candidate.DisplayName);

        return currentHasTitle != candidateHasTitle && candidateHasTitle;
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

    private static bool IsFocusTrackingCandidate(string processName)
    {
        if (ExcludedVisibleProcessNames.Contains(processName))
        {
            return false;
        }

        return true;
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

    private static bool HasMeasurableFocusWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero ||
            !IsWindowVisible(windowHandle) ||
            !IsWindowEnabled(windowHandle) ||
            IsCloakedWindow(windowHandle))
        {
            return false;
        }

        long extendedStyle = GetWindowLongPtr(windowHandle, ExtendedWindowStyleIndex).ToInt64();

        return (extendedStyle & ExtendedToolWindowStyle) == 0 &&
            (extendedStyle & ExtendedNoActivateStyle) == 0;
    }

    private static bool IsCloakedWindow(IntPtr windowHandle)
    {
        return DwmGetWindowAttribute(
            windowHandle,
            DwmWindowAttributeCloaked,
            out int cloaked,
            Marshal.SizeOf<int>()) == SuccessHResult &&
            cloaked != 0;
    }

    private static string? GetWindowTitle(IntPtr windowHandle)
    {
        int textLength = GetWindowTextLength(windowHandle);
        if (textLength <= 0)
        {
            return null;
        }

        char[] title = new char[textLength + 1];
        int copiedLength = GetWindowText(windowHandle, title, title.Length);
        return copiedLength > 0
            ? new string(title, 0, copiedLength)
            : null;
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool IsWindowEnabled(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextLengthW")]
    private static extern int GetWindowTextLength(IntPtr windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
    private static extern int GetWindowText(IntPtr windowHandle, [Out] char[] text, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        out int attributeValue,
        int attributeSize);

    private delegate bool EnumWindowsProc(IntPtr windowHandle, IntPtr lParam);
}

internal sealed record EligibleProcessInfo(
    int ProcessId,
    string ProcessName);

internal sealed record FocusableWindowCandidate(
    string DisplayName,
    string ProcessName,
    int ProcessId,
    IntPtr WindowHandle,
    bool IsMinimized);

internal sealed record ProcessCatalogSnapshot(
    IReadOnlyCollection<string> ProcessNames,
    IReadOnlyDictionary<int, EligibleProcessInfo> EligibleProcessesById,
    int ExceptionCount);

internal sealed record ProcessRunState(
    string ProcessName,
    bool HasFocusableWindow);

internal sealed record ProcessRunStateScanResult(
    IReadOnlyDictionary<string, ProcessRunState> ProcessStates,
    TimeSpan Elapsed,
    int ExceptionCount);
