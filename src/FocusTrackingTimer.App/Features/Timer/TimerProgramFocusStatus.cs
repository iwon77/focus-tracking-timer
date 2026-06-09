using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App.Features.Timer;

internal static class TimerProgramFocusStatus
{
    public static (string Brush, string Text) GetRuntimeStatus(
        string processName,
        IReadOnlyDictionary<string, ProcessRunState> processStates)
    {
        if (!processStates.TryGetValue(processName, out ProcessRunState? state))
        {
            return ("#A7A7A0", "등록됨 / 현재 실행 중 아님");
        }

        return state.HasFocusableWindow
            ? ("#2EAD62", "등록됨 / 실행 중 / 포커스 기록 가능")
            : ("#D14B4B", "등록됨 / 실행 중 / 포커스 기록 불가");
    }

    public static string? GetFocusableObservedProcessName(
        TrackedApplication? observedApplication,
        IReadOnlyDictionary<string, ProcessRunState> processStates)
    {
        if (observedApplication is null)
        {
            return null;
        }

        return processStates.TryGetValue(observedApplication.ProcessName, out ProcessRunState? state)
            ? (state.HasFocusableWindow ? observedApplication.ProcessName : null)
            : observedApplication.ProcessName;
    }
}
