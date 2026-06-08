using System.Collections.ObjectModel;

using System.Text;

namespace FocusTrackingTimer.Core.Tracking;

public sealed class ProjectDefinition
{
    public const int MaxNameLength = 20;
    public const int MaxMemoBytes = 500;

    private readonly List<RegisteredProgramInfo> _registeredPrograms = [];

    public ProjectDefinition(
        Guid id,
        string name,
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

        DateTimeOffset resolvedCreatedAt = createdAt ?? DateTimeOffset.Now;
        Id = id;
        Name = NormalizeProjectName(name);
        IsDeleted = isDeleted;
        CreatedAt = resolvedCreatedAt;
        IsPinned = isPinned;
        Memo = NormalizeMemo(memo);
        MemoUpdatedAt = memoUpdatedAt is null || memoUpdatedAt.Value < resolvedCreatedAt
            ? resolvedCreatedAt
            : memoUpdatedAt.Value;
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public bool IsPinned { get; private set; }

    public string Memo { get; private set; }

    public DateTimeOffset MemoUpdatedAt { get; private set; }

    public ReadOnlyCollection<TrackedApplication> RegisteredPrograms => new(
        _registeredPrograms.Select(item => item.Program).ToList());

    public ReadOnlyCollection<RegisteredProgramInfo> RegisteredProgramInfos => _registeredPrograms.AsReadOnly();

    public bool TryRegisterProgram(TrackedApplication application)
    {
        return TryRegisterProgram(application, DateTimeOffset.Now);
    }

    public bool TryRegisterProgram(TrackedApplication application, DateTimeOffset registeredAt)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (ContainsProgram(application.ProcessName))
        {
            return false;
        }

        _registeredPrograms.Add(new RegisteredProgramInfo(application, registeredAt, application.DisplayName));
        return true;
    }

    public void ReplaceRegisteredPrograms(IEnumerable<RegisteredProgramInfo> registeredPrograms)
    {
        ArgumentNullException.ThrowIfNull(registeredPrograms);

        Dictionary<string, RegisteredProgramInfo> registrationsByProcessName = new(StringComparer.OrdinalIgnoreCase);
        foreach (RegisteredProgramInfo registration in registeredPrograms)
        {
            ArgumentNullException.ThrowIfNull(registration);
            ArgumentNullException.ThrowIfNull(registration.Program);

            if (!registrationsByProcessName.TryAdd(registration.Program.ProcessName, registration))
            {
                throw new InvalidOperationException("Registered program process names must be unique.");
            }
        }

        _registeredPrograms.Clear();
        _registeredPrograms.AddRange(registrationsByProcessName.Values);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        Name = NormalizeProjectName(name);
    }

    public void MarkDeleted()
    {
        IsDeleted = true;
    }

    public void SetPinned(bool isPinned)
    {
        IsPinned = isPinned;
    }

    public void UpdateMemo(string memo, DateTimeOffset updatedAt)
    {
        Memo = NormalizeMemo(memo);
        MemoUpdatedAt = updatedAt < CreatedAt ? CreatedAt : updatedAt;
    }

    public bool TryUpdateProgram(string processName, TrackedApplication updatedApplication)
    {
        ArgumentNullException.ThrowIfNull(updatedApplication);

        int index = FindProgramIndex(processName);
        if (index < 0)
        {
            return false;
        }

        bool processNameChanged = !string.Equals(
            _registeredPrograms[index].Program.ProcessName,
            updatedApplication.ProcessName,
            StringComparison.OrdinalIgnoreCase);

        if (processNameChanged && ContainsProgram(updatedApplication.ProcessName))
        {
            return false;
        }

        _registeredPrograms[index] = _registeredPrograms[index] with
        {
            Program = updatedApplication
        };
        return true;
    }

    public bool TryRemoveProgram(string processName)
    {
        int index = FindProgramIndex(processName);
        if (index < 0)
        {
            return false;
        }

        _registeredPrograms.RemoveAt(index);
        return true;
    }

    public bool TryMoveProgram(string processName, int offset)
    {
        int index = FindProgramIndex(processName);
        int targetIndex = index + offset;

        if (index < 0 || targetIndex < 0 || targetIndex >= _registeredPrograms.Count)
        {
            return false;
        }

        RegisteredProgramInfo program = _registeredPrograms[index];
        _registeredPrograms.RemoveAt(index);
        _registeredPrograms.Insert(targetIndex, program);
        return true;
    }

    public bool TrySetProgramPinned(string processName, bool isPinned)
    {
        int index = FindProgramIndex(processName);
        if (index < 0)
        {
            return false;
        }

        RegisteredProgramInfo registration = _registeredPrograms[index] with { IsPinned = isPinned };
        _registeredPrograms.RemoveAt(index);

        if (isPinned)
        {
            _registeredPrograms.Insert(0, registration);
        }
        else
        {
            int insertIndex = _registeredPrograms.FindLastIndex(static item => item.IsPinned) + 1;
            _registeredPrograms.Insert(insertIndex, registration);
        }

        return true;
    }

    public bool ContainsProgram(string processName)
    {
        return _registeredPrograms.Any(program =>
            string.Equals(program.Program.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
    }

    public TrackedApplication? FindProgram(string processName)
    {
        return _registeredPrograms.FirstOrDefault(program =>
            string.Equals(program.Program.ProcessName, processName, StringComparison.OrdinalIgnoreCase))?.Program;
    }

    private int FindProgramIndex(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return -1;
        }

        return _registeredPrograms.FindIndex(program =>
            string.Equals(program.Program.ProcessName, processName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeProjectName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        string normalizedName = name.Trim();
        if (normalizedName.Length > MaxNameLength)
        {
            throw new ArgumentException($"Project name cannot exceed {MaxNameLength} characters.", nameof(name));
        }

        return normalizedName;
    }

    private static string NormalizeMemo(string? memo)
    {
        string normalizedMemo = memo ?? string.Empty;
        if (Encoding.UTF8.GetByteCount(normalizedMemo) > MaxMemoBytes)
        {
            throw new ArgumentException($"Project memo cannot exceed {MaxMemoBytes} bytes.", nameof(memo));
        }

        return normalizedMemo;
    }
}
