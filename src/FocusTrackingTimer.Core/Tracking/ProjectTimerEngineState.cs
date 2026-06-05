using System.Collections.ObjectModel;

namespace FocusTrackingTimer.Core.Tracking;

public sealed class ProjectTimerEngineState
{
    public static ProjectTimerEngineState Empty { get; } = new([], []);

    public ProjectTimerEngineState(
        IEnumerable<ProjectState> projects,
        IEnumerable<ProjectTimerRecord> completedRecords)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(completedRecords);

        Projects = new ReadOnlyCollection<ProjectState>(projects.ToList());
        CompletedRecords = new ReadOnlyCollection<ProjectTimerRecord>(completedRecords.ToList());
    }

    public ReadOnlyCollection<ProjectState> Projects { get; }

    public ReadOnlyCollection<ProjectTimerRecord> CompletedRecords { get; }
}
