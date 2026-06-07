using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FocusTrackingTimer.App;

internal static class WindowFocusService
{
    private const int RestoreWindow = 9;

    public static bool TryFocusProcessMainWindow(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        foreach (Process process in Process.GetProcessesByName(processName.Trim()))
        {
            try
            {
                IntPtr windowHandle = process.MainWindowHandle;
                if (windowHandle == IntPtr.Zero)
                {
                    continue;
                }

                _ = ShowWindow(windowHandle, RestoreWindow);
                return SetForegroundWindow(windowHandle);
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

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);
}
