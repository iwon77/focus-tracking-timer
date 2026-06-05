using System.Collections.ObjectModel;

namespace FocusTrackingTimer.Core.Tracking;

public sealed class ProjectState
{
    public ProjectState(Guid id, string name, IEnumerable<RegisteredProgramInfo> registeredPrograms, bool isDeleted = false)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Project id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(registeredPrograms);

        Id = id;
        Name = name.Trim();
        IsDeleted = isDeleted;
        RegisteredPrograms = new ReadOnlyCollection<RegisteredProgramInfo>(registeredPrograms.ToList());
    }

    public Guid Id { get; }

    public string Name { get; }

    public bool IsDeleted { get; }

    public ReadOnlyCollection<RegisteredProgramInfo> RegisteredPrograms { get; }
}
