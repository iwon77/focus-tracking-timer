using System.Diagnostics;
using System.Runtime.InteropServices;
using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App;

internal static class ForegroundWindowTracker
{
    public static FocusObservation GetCurrentFocusedApplication(int currentProcessId)
    {
        IntPtr foregroundWindowHandle = GetForegroundWindow();

        if (foregroundWindowHandle == IntPtr.Zero)
        {
            return new FocusObservation(null, "활성 창을 확인하지 못해 포커스 추적을 대기 중입니다.");
        }

        _ = GetWindowThreadProcessId(foregroundWindowHandle, out uint processId);
        if (processId == 0)
        {
            return new FocusObservation(null, "활성 프로세스를 읽지 못해 포커스 추적을 대기 중입니다.");
        }

        if (processId == currentProcessId)
        {
            return new FocusObservation(null, "프로토타입 창이 활성화되어 프로그램 포커스 집계를 잠시 멈췄습니다.");
        }

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            string displayName = string.IsNullOrWhiteSpace(process.MainWindowTitle)
                ? process.ProcessName
                : process.MainWindowTitle.Trim();

            return new FocusObservation(
                new TrackedApplication(process.ProcessName, displayName),
                $"{displayName} 포커스를 감지했습니다.");
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return new FocusObservation(null, "프로세스 정보를 읽는 중 오류가 있어 이번 틱은 건너뜁니다.");
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);
}

internal sealed record FocusObservation(TrackedApplication? Application, string StatusMessage);
