using System.Collections.ObjectModel;

namespace FocusTrackingTimer.Core.Tracking;

public sealed class ProjectTimerRecord
{
    public ProjectTimerRecord(
        Guid projectId,
        string projectName,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        IEnumerable<ProgramFocusSummary> programSummaries)
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

        ArgumentNullException.ThrowIfNull(programSummaries);

        ProjectId = projectId;
        ProjectName = projectName.Trim();
        StartedAt = startedAt;
        EndedAt = endedAt;
        ProgramSummaries = new ReadOnlyCollection<ProgramFocusSummary>(programSummaries.ToList());
    }

    public Guid ProjectId { get; }

    public string ProjectName { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset EndedAt { get; }

    public TimeSpan WallClockDuration => EndedAt - StartedAt;

    public TimeSpan TotalDuration => ProgramSummaries.Aggregate(
        TimeSpan.Zero,
        static (total, summary) => total + summary.FocusDuration);

    public ReadOnlyCollection<ProgramFocusSummary> ProgramSummaries { get; }
}
