using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App.Features.Timer;

internal static class TimerProjectDisplayService
{
    public static IEnumerable<ProjectDefinition> GetSortedProjects(
        IEnumerable<ProjectDefinition> projects,
        ProjectSortMode sortMode)
    {
        IEnumerable<ProjectDefinition> pinnedProjects = projects.Where(static project => project.IsPinned);
        IEnumerable<ProjectDefinition> unpinnedProjects = projects.Where(static project => !project.IsPinned);

        unpinnedProjects = sortMode switch
        {
            ProjectSortMode.Name => unpinnedProjects.OrderBy(static project => project.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => unpinnedProjects
        };

        return pinnedProjects.Concat(unpinnedProjects);
    }

    public static string GetProjectPeriodText(
        ProjectTimerEngine engine,
        ProjectDefinition project,
        bool isActiveProject)
    {
        if (engine.ActiveStartedAt.HasValue && isActiveProject)
        {
            return $"최근 작업 일시: {FormatRecentWorkDateTime(engine.ActiveStartedAt.Value)}";
        }

        IReadOnlyList<ProjectTimerRecord> recentRecords = engine.GetRecentRecords(1, project.Id);
        return recentRecords.Count == 0
            ? "최근 작업 일시: -"
            : $"최근 작업 일시: {FormatRecentWorkDateTime(recentRecords[0].StartedAt)} ~ {FormatRecentWorkDateTime(recentRecords[0].EndedAt)}";
    }

    private static string FormatRecentWorkDateTime(DateTimeOffset value)
    {
        return value.LocalDateTime.ToString("yy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture);
    }
}
