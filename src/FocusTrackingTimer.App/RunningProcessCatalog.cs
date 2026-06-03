using System.Diagnostics;
using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App;

internal static class RunningProcessCatalog
{
    public static IReadOnlyDictionary<string, ProcessRunState> GetProcessRunStates(int currentProcessId)
    {
        Dictionary<string, ProcessRunState> processStates = new(StringComparer.OrdinalIgnoreCase);

        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == currentProcessId)
                {
                    continue;
                }

                string processName = process.ProcessName;
                bool hasFocusableWindow = process.MainWindowHandle != IntPtr.Zero;

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
                continue;
            }
            finally
            {
                process.Dispose();
            }
        }

        return processStates;
    }

    public static IReadOnlyList<TrackedApplication> GetVisibleProcesses(int currentProcessId)
    {
        Dictionary<string, TrackedApplication> applications = new(StringComparer.OrdinalIgnoreCase);

        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == currentProcessId || process.MainWindowHandle == IntPtr.Zero)
                {
                    continue;
                }

                string displayName = string.IsNullOrWhiteSpace(process.MainWindowTitle)
                    ? process.ProcessName
                    : process.MainWindowTitle.Trim();

                TrackedApplication application = new(process.ProcessName, displayName);

                _ = applications.TryAdd(application.ProcessName, application);
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

        return [.. applications.Values
            .OrderBy(static item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }
}

internal sealed record ProcessRunState(
    string ProcessName,
    bool HasFocusableWindow);
