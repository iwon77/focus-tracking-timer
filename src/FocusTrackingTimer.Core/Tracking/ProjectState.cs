using System.Collections.ObjectModel;

namespace FocusTrackingTimer.Core.Tracking;

public sealed class ProjectState
{
    public ProjectState(
        Guid id,
        string name,
        IEnumerable<RegisteredProgramInfo> registeredPrograms,
        bool isDeleted = false,
        DateTimeOffset? createdAt = null,
        bool isPinned = false,
        string memo = "",
        DateTimeOffset? memoUpdatedAt = null)
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
        CreatedAt = createdAt ?? DateTimeOffset.Now;
        IsPinned = isPinned;
        Memo = memo ?? string.Empty;
        MemoUpdatedAt = memoUpdatedAt ?? CreatedAt;
        RegisteredPrograms = new ReadOnlyCollection<RegisteredProgramInfo>(registeredPrograms.ToList());
    }

    public Guid Id { get; }

    public string Name { get; }

    public bool IsDeleted { get; }

    public DateTimeOffset CreatedAt { get; }

    public bool IsPinned { get; }

    public string Memo { get; }

    public DateTimeOffset MemoUpdatedAt { get; }

    public ReadOnlyCollection<RegisteredProgramInfo> RegisteredPrograms { get; }
}
