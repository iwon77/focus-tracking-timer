using System.Windows.Media;
using FocusTrackingTimer.App.Infrastructure;
using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App.Features.Timer;

internal static class TimerProgramFocusStatus
{
    public static (Brush Brush, string Text) GetRuntimeStatus(
        string processName,
        IReadOnlyDictionary<string, ProcessRunState> processStates)
    {
        if (!processStates.TryGetValue(processName, out ProcessRunState? state))
        {
            return (ThemeBrushes.HintText, "현재 실행 중 아님");
        }

        return state.HasFocusableWindow
            ? (ThemeBrushes.Status, "집중 기록 가능")
            : (ThemeBrushes.Sunday, "집중 기록 불가");
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
