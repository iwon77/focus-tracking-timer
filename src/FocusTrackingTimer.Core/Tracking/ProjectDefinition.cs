using System.Collections.ObjectModel;

namespace FocusTrackingTimer.Core.Tracking;

public sealed class ProjectDefinition
{
    private readonly List<RegisteredProgramInfo> _registeredPrograms = [];

    public ProjectDefinition(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Project id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        Id = id;
        Name = name.Trim();
    }

    public Guid Id { get; }

    public string Name { get; private set; }

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

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        Name = name.Trim();
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
}
