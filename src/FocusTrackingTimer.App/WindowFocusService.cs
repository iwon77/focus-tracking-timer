using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace FocusTrackingTimer.App;

internal static class WindowFocusService
{
    private const int RestoreWindow = 9;
    private const int ForegroundCheckRetryCount = 5;
    private const int ForegroundCheckDelayMilliseconds = 40;

    public static bool TryFocusWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        _ = ShowWindow(windowHandle, RestoreWindow);
        _ = SetForegroundWindow(windowHandle);
        _ = GetWindowThreadProcessId(windowHandle, out uint processId);

        return processId != 0 && DidProcessBecomeForeground((int)processId);
    }

    public static bool TryFocusProcessMainWindow(int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

        try
        {
            using Process process = Process.GetProcessById(processId);
            return TryFocusProcessMainWindow(process);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public static bool TryFocusProcessMainWindow(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        if (RunningProcessCatalog.TryGetRepresentativeWindowHandle(processName.Trim(), out IntPtr windowHandle))
        {
            return TryFocusWindow(windowHandle);
        }

        foreach (Process process in Process.GetProcessesByName(processName.Trim()))
        {
            try
            {
                if (TryFocusProcessMainWindow(process))
                {
                    return true;
                }
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

        return false;
    }

    private static bool TryFocusProcessMainWindow(Process process)
    {
        return TryFocusWindow(process.MainWindowHandle);
    }

    private static bool DidProcessBecomeForeground(int processId)
    {
        for (int attempt = 0; attempt < ForegroundCheckRetryCount; attempt++)
        {
            IntPtr foregroundWindowHandle = GetForegroundWindow();
            if (foregroundWindowHandle != IntPtr.Zero)
            {
                _ = GetWindowThreadProcessId(foregroundWindowHandle, out uint foregroundProcessId);
                if (foregroundProcessId == (uint)processId)
                {
                    return true;
                }
            }

            Thread.Sleep(ForegroundCheckDelayMilliseconds);
        }

        return false;
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);
}
