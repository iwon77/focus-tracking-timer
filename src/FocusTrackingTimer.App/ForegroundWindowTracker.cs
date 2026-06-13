using System.Runtime.InteropServices;
using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App;

internal static class ForegroundWindowTracker
{
    public static FocusObservation GetCurrentFocusedApplication(int currentProcessId)
    {
        return GetFocusedApplication(GetForegroundWindow(), currentProcessId);
    }

    public static FocusObservation GetFocusedApplication(IntPtr foregroundWindowHandle, int currentProcessId)
    {
        if (foregroundWindowHandle == IntPtr.Zero)
        {
            return new FocusObservation(null, "활성 창을 확인하지 못해 작업 추적이 대기 중입니다.");
        }

        _ = GetWindowThreadProcessId(foregroundWindowHandle, out uint processId);
        if (processId == 0)
        {
            return new FocusObservation(null, "활성 프로세스를 읽지 못해 작업 추적이 대기 중입니다.");
        }

        if (processId == currentProcessId)
        {
            return new FocusObservation(null, "프로그램 창이 활성화되어 프로그램 작업 집계를 잠시 멈춥니다.");
        }

        string? processName = ProcessIdentityResolver.TryGetProcessName((int)processId);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return new FocusObservation(null, "프로세스 정보를 읽는 중 오류가 있어 이번 집계는 건너뜁니다.");
        }

        string displayName = GetWindowTitle(foregroundWindowHandle) ?? processName;

        return new FocusObservation(
            new TrackedApplication(processName, displayName),
            $"{displayName} 작업을 감지했습니다.");
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
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextLengthW")]
    private static extern int GetWindowTextLength(IntPtr windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
    private static extern int GetWindowText(IntPtr windowHandle, [Out] char[] text, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);
}

internal sealed record FocusObservation(TrackedApplication? Application, string StatusMessage);
