using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace FocusTrackingTimer.App;

internal static class ProcessIdentityResolver
{
    private const uint QueryLimitedInformationAccess = 0x1000;
    private const int QueryImageNameBufferLength = 1024;

    public static string? TryGetProcessName(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        try
        {
            string? processName = process.ProcessName;
            return string.IsNullOrWhiteSpace(processName)
                ? TryGetProcessName(process.Id)
                : processName.Trim();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            return TryGetProcessName(process.Id);
        }
    }

    public static string? TryGetProcessName(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        try
        {
            using Process process = Process.GetProcessById(processId);
            string? processName = process.ProcessName;
            if (!string.IsNullOrWhiteSpace(processName))
            {
                return processName.Trim();
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
        }

        return TryGetProcessNameFromImagePath(processId);
    }

    private static string? TryGetProcessNameFromImagePath(int processId)
    {
        IntPtr processHandle = OpenProcess(QueryLimitedInformationAccess, false, processId);
        if (processHandle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            char[] imagePathBuffer = new char[QueryImageNameBufferLength];
            uint imagePathLength = (uint)imagePathBuffer.Length;
            if (!QueryFullProcessImageName(processHandle, 0, imagePathBuffer, ref imagePathLength) ||
                imagePathLength == 0)
            {
                return null;
            }

            string fullPath = new(imagePathBuffer, 0, (int)imagePathLength);
            string processName = Path.GetFileNameWithoutExtension(fullPath);
            return string.IsNullOrWhiteSpace(processName)
                ? null
                : processName.Trim();
        }
        finally
        {
            _ = CloseHandle(processHandle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryFullProcessImageNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(
        IntPtr processHandle,
        uint flags,
        [Out] char[] executablePath,
        ref uint size);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
