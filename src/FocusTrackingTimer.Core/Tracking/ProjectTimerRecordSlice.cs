using System.Collections.ObjectModel;

namespace FocusTrackingTimer.Core.Tracking;

public sealed class ProjectTimerRecordSlice
{
    public ProjectTimerRecordSlice(
        Guid projectId,
        string projectName,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        IEnumerable<ProgramFocusSegment> focusSegments)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project id is required.", nameof(projectId));
        }

        if (string.IsNullOrWhiteSpace(projectName))
        {
            throw new ArgumentException("Project name is required.", nameof(projectName));
        }

        if (endedAt < startedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(endedAt), "End time must be later than start time.");
        }

        ArgumentNullException.ThrowIfNull(focusSegments);

        List<ProgramFocusSegment> materializedSegments = focusSegments.ToList();

        ProjectId = projectId;
        ProjectName = projectName.Trim();
        StartedAt = startedAt;
        EndedAt = endedAt;
        FocusSegments = new ReadOnlyCollection<ProgramFocusSegment>(materializedSegments);
        ProgramSummaries = new ReadOnlyCollection<ProgramFocusSummary>(BuildProgramSummaries(materializedSegments));
    }

    public Guid ProjectId { get; }

    public string ProjectName { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset EndedAt { get; }

    public TimeSpan WallClockDuration => EndedAt - StartedAt;

    public ReadOnlyCollection<ProgramFocusSegment> FocusSegments { get; }

    public TimeSpan TotalDuration => ProgramSummaries.Aggregate(
        TimeSpan.Zero,
        static (total, summary) => total + summary.FocusDuration);

    public ReadOnlyCollection<ProgramFocusSummary> ProgramSummaries { get; }

    private static List<ProgramFocusSummary> BuildProgramSummaries(IEnumerable<ProgramFocusSegment> focusSegments)
    {
        Dictionary<string, ProgramFocusSummary> summaries = new(StringComparer.OrdinalIgnoreCase);

        foreach (ProgramFocusSegment segment in focusSegments)
        {
            if (summaries.TryGetValue(segment.Program.ProcessName, out ProgramFocusSummary? current))
            {
                summaries[segment.Program.ProcessName] = current with
                {
                    FocusDuration = current.FocusDuration + segment.FocusDuration
                };
                continue;
            }

            summaries[segment.Program.ProcessName] = new ProgramFocusSummary(segment.Program, segment.FocusDuration);
        }

        return [.. summaries.Values
            .OrderByDescending(static summary => summary.FocusDuration)
            .ThenBy(static summary => summary.Program.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }
}
