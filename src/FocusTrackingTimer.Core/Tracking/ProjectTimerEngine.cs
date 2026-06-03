using System.Collections.ObjectModel;

namespace FocusTrackingTimer.Core.Tracking;

public sealed class ProjectTimerEngine
{
    private readonly List<ProjectDefinition> _projects = [];
    private readonly List<ProjectTimerRecord> _completedRecords = [];
    private ActiveProjectSession? _activeSession;

    public bool IsRunning => _activeSession is not null;

    public Guid? ActiveProjectId => _activeSession?.Project.Id;

    public string? ActiveProjectName => _activeSession?.Project.Name;

    public DateTimeOffset? ActiveStartedAt => _activeSession?.StartedAt;

    public string? ActiveFocusedProgramName => _activeSession?.FocusedProgram?.DisplayName;

    public string? ActiveFocusedProcessName => _activeSession?.FocusedProgram?.ProcessName;

    public ReadOnlyCollection<ProjectDefinition> Projects => _projects.AsReadOnly();

    public ReadOnlyCollection<ProjectTimerRecord> CompletedRecords => _completedRecords.AsReadOnly();

    public bool TryAddProject(string name, out ProjectDefinition project)
    {
        string normalizedName = NormalizeRequiredValue(name, nameof(name));
        ProjectDefinition? existingProject = _projects.FirstOrDefault(item =>
            string.Equals(item.Name, normalizedName, StringComparison.OrdinalIgnoreCase));

        if (existingProject is not null)
        {
            project = existingProject;
            return false;
        }

        project = new ProjectDefinition(Guid.NewGuid(), normalizedName);
        _projects.Add(project);
        return true;
    }

    public bool TryRenameProject(Guid projectId, string name)
    {
        string normalizedName = NormalizeRequiredValue(name, nameof(name));
        ProjectDefinition project = GetRequiredProject(projectId);

        if (_projects.Any(item =>
                item.Id != projectId &&
                string.Equals(item.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        project.Rename(normalizedName);
        return true;
    }

    public bool TryRemoveProject(Guid projectId)
    {
        if (ActiveProjectId == projectId)
        {
            return false;
        }

        ProjectDefinition? project = _projects.FirstOrDefault(item => item.Id == projectId);
        if (project is null)
        {
            return false;
        }

        _ = _projects.Remove(project);
        _completedRecords.RemoveAll(record => record.ProjectId == projectId);
        return true;
    }

    public bool TryRegisterProgram(Guid projectId, TrackedApplication application)
    {
        return TryRegisterProgram(projectId, application, DateTimeOffset.Now);
    }

    public bool TryRegisterProgram(Guid projectId, TrackedApplication application, DateTimeOffset registeredAt)
    {
        ArgumentNullException.ThrowIfNull(application);

        ProjectDefinition project = GetRequiredProject(projectId);
        return project.TryRegisterProgram(application, registeredAt);
    }

    public IReadOnlyList<RegisteredProgramInfo> GetRegisteredProgramInfos(Guid projectId)
    {
        ProjectDefinition project = GetRequiredProject(projectId);
        return project.RegisteredProgramInfos;
    }

    public bool TryUpdateProgram(Guid projectId, string processName, TrackedApplication updatedApplication)
    {
        ProjectDefinition project = GetRequiredProject(projectId);
        return project.TryUpdateProgram(processName, updatedApplication);
    }

    public bool TryRemoveProgram(Guid projectId, string processName)
    {
        ProjectDefinition project = GetRequiredProject(projectId);
        return project.TryRemoveProgram(processName);
    }

    public bool TryMoveProgram(Guid projectId, string processName, int offset)
    {
        ProjectDefinition project = GetRequiredProject(projectId);
        return project.TryMoveProgram(processName, offset);
    }

    public void StartProject(Guid projectId, DateTimeOffset startedAt)
    {
        if (_activeSession is not null)
        {
            throw new InvalidOperationException("Another project session is already running.");
        }

        ProjectDefinition project = GetRequiredProject(projectId);
        _activeSession = new ActiveProjectSession(project, startedAt);
    }

    public ProjectTimerRecord StopProject(DateTimeOffset endedAt)
    {
        ActiveProjectSession session = _activeSession ?? throw new InvalidOperationException("No running project session.");

        if (endedAt < session.StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(endedAt), "End time must be later than start time.");
        }

        session.CloseFocusedProgram(endedAt);

        ProjectTimerRecord record = new(
            session.Project.Id,
            session.Project.Name,
            session.StartedAt,
            endedAt,
            session.GetProgramSummaries());

        _completedRecords.Add(record);
        _activeSession = null;
        return record;
    }

    public void ObserveFocusedProgram(string? processName, DateTimeOffset observedAt)
    {
        if (_activeSession is null)
        {
            return;
        }

        if (observedAt < _activeSession.StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(observedAt), "Observation time cannot move backwards.");
        }

        string? normalizedProcessName = NormalizeProcessName(processName);
        TrackedApplication? registeredProgram = normalizedProcessName is null
            ? null
            : _activeSession.Project.FindProgram(normalizedProcessName);

        if (registeredProgram is not null &&
            _activeSession.FocusedProgram is not null &&
            string.Equals(registeredProgram.ProcessName, _activeSession.FocusedProgram.ProcessName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _activeSession.CloseFocusedProgram(observedAt);
        _activeSession.SetFocusedProgram(registeredProgram, observedAt);
    }

    public TimeSpan GetProjectTotalDuration(Guid projectId, DateTimeOffset observedAt)
    {
        TimeSpan completedDuration = _completedRecords
            .Where(record => record.ProjectId == projectId)
            .Aggregate(TimeSpan.Zero, static (total, record) => total + record.TotalDuration);

        if (_activeSession is not null && _activeSession.Project.Id == projectId)
        {
            completedDuration += SumProgramFocusDuration(_activeSession.GetProgramSummaries(observedAt));
        }

        return completedDuration < TimeSpan.Zero ? TimeSpan.Zero : completedDuration;
    }

    public TimeSpan GetCurrentRunDuration(Guid projectId, DateTimeOffset observedAt)
    {
        if (_activeSession is null || _activeSession.Project.Id != projectId)
        {
            return TimeSpan.Zero;
        }

        return SumProgramFocusDuration(_activeSession.GetProgramSummaries(observedAt));
    }

    public TimeSpan GetCurrentWallClockDuration(Guid projectId, DateTimeOffset observedAt)
    {
        if (_activeSession is null || _activeSession.Project.Id != projectId)
        {
            return TimeSpan.Zero;
        }

        return observedAt < _activeSession.StartedAt
            ? TimeSpan.Zero
            : observedAt - _activeSession.StartedAt;
    }

    public IReadOnlyList<ProgramFocusSummary> GetProgramSummaries(
        Guid projectId,
        DateTimeOffset observedAt,
        ProgramSortMode sortMode = ProgramSortMode.MostUsed)
    {
        ProjectDefinition project = GetRequiredProject(projectId);
        Dictionary<string, ProgramFocusSummary> summaryByProcessName = new(StringComparer.OrdinalIgnoreCase);

        foreach (ProjectTimerRecord record in _completedRecords.Where(item => item.ProjectId == projectId))
        {
            foreach (ProgramFocusSummary summary in record.ProgramSummaries)
            {
                AddOrUpdateSummary(summaryByProcessName, summary.Program, summary.FocusDuration);
            }
        }

        if (_activeSession is not null && _activeSession.Project.Id == projectId)
        {
            foreach (ProgramFocusSummary summary in _activeSession.GetProgramSummaries(observedAt))
            {
                AddOrUpdateSummary(summaryByProcessName, summary.Program, summary.FocusDuration);
            }
        }

        foreach (TrackedApplication registeredProgram in project.RegisteredPrograms)
        {
            AddOrUpdateSummary(summaryByProcessName, registeredProgram, TimeSpan.Zero);
        }

        return sortMode switch
        {
            ProgramSortMode.Registered or ProgramSortMode.Manual => [.. project.RegisteredPrograms
                .Select(program => summaryByProcessName[program.ProcessName])],
            _ => [.. project.RegisteredPrograms
                .Select(program => summaryByProcessName[program.ProcessName])
                .OrderByDescending(static summary => summary.FocusDuration)
                .ThenBy(static summary => summary.Program.DisplayName, StringComparer.CurrentCultureIgnoreCase)]
        };
    }

    public IReadOnlyList<ProgramFocusSummary> GetCurrentSessionProgramSummaries(
        Guid projectId,
        DateTimeOffset observedAt,
        ProgramSortMode sortMode = ProgramSortMode.MostUsed)
    {
        ProjectDefinition project = GetRequiredProject(projectId);
        Dictionary<string, ProgramFocusSummary> summaryByProcessName = new(StringComparer.OrdinalIgnoreCase);

        if (_activeSession is not null && _activeSession.Project.Id == projectId)
        {
            foreach (ProgramFocusSummary summary in _activeSession.GetProgramSummaries(observedAt))
            {
                AddOrUpdateSummary(summaryByProcessName, summary.Program, summary.FocusDuration);
            }
        }

        foreach (TrackedApplication registeredProgram in project.RegisteredPrograms)
        {
            AddOrUpdateSummary(summaryByProcessName, registeredProgram, TimeSpan.Zero);
        }

        return sortMode switch
        {
            ProgramSortMode.Registered or ProgramSortMode.Manual => [.. project.RegisteredPrograms
                .Select(program => summaryByProcessName[program.ProcessName])],
            _ => [.. project.RegisteredPrograms
                .Select(program => summaryByProcessName[program.ProcessName])
                .OrderByDescending(static summary => summary.FocusDuration)
                .ThenBy(static summary => summary.Program.DisplayName, StringComparer.CurrentCultureIgnoreCase)]
        };
    }

    public IReadOnlyList<ProjectTimerRecord> GetRecentRecords(int count, Guid? projectId = null)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero.");
        }

        IEnumerable<ProjectTimerRecord> records = _completedRecords;
        if (projectId.HasValue)
        {
            records = records.Where(record => record.ProjectId == projectId.Value);
        }

        return [.. records
            .OrderByDescending(static record => record.EndedAt)
            .Take(count)];
    }

    public IReadOnlyList<DailyDurationSummary> GetDailyDurationSummaries(
        DateOnly fromDate,
        DateOnly toDate,
        DateTimeOffset observedAt,
        Guid? projectId = null)
    {
        if (toDate < fromDate)
        {
            throw new ArgumentOutOfRangeException(nameof(toDate), "The end date must be on or after the start date.");
        }

        Dictionary<DateOnly, TimeSpan> totalsByDate = [];

        foreach (ProjectTimerRecord record in GetRecordsForSummary(projectId, observedAt))
        {
            DateOnly recordDate = DateOnly.FromDateTime(record.StartedAt.LocalDateTime.Date);
            if (recordDate < fromDate || recordDate > toDate)
            {
                continue;
            }

            totalsByDate[recordDate] = totalsByDate.GetValueOrDefault(recordDate, TimeSpan.Zero) + record.TotalDuration;
        }

        List<DailyDurationSummary> summaries = [];
        for (DateOnly date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            summaries.Add(new DailyDurationSummary(date, totalsByDate.GetValueOrDefault(date, TimeSpan.Zero)));
        }

        return summaries;
    }

    public TimeSpan GetTodayDuration(DateOnly today, DateTimeOffset observedAt, Guid? projectId = null)
    {
        return GetDailyDurationSummaries(today, today, observedAt, projectId)[0].TotalDuration;
    }

    private IEnumerable<ProjectTimerRecord> GetRecordsForSummary(Guid? projectId, DateTimeOffset observedAt)
    {
        List<ProjectTimerRecord> records = [.. _completedRecords];

        if (_activeSession is not null &&
            (!projectId.HasValue || _activeSession.Project.Id == projectId.Value))
        {
            records.Add(new ProjectTimerRecord(
                _activeSession.Project.Id,
                _activeSession.Project.Name,
                _activeSession.StartedAt,
                observedAt,
                _activeSession.GetProgramSummaries(observedAt)));
        }

        return projectId.HasValue
            ? records.Where(record => record.ProjectId == projectId.Value)
            : records;
    }

    private static void AddOrUpdateSummary(
        Dictionary<string, ProgramFocusSummary> summaries,
        TrackedApplication program,
        TimeSpan duration)
    {
        if (summaries.TryGetValue(program.ProcessName, out ProgramFocusSummary? current))
        {
            summaries[program.ProcessName] = current with
            {
                Program = current.Program,
                FocusDuration = current.FocusDuration + duration
            };
            return;
        }

        summaries[program.ProcessName] = new ProgramFocusSummary(program, duration);
    }

    private static TimeSpan SumProgramFocusDuration(IEnumerable<ProgramFocusSummary> summaries)
    {
        return summaries.Aggregate(TimeSpan.Zero, static (total, summary) => total + summary.FocusDuration);
    }

    private ProjectDefinition GetRequiredProject(Guid projectId)
    {
        ProjectDefinition? project = _projects.FirstOrDefault(item => item.Id == projectId);
        return project ?? throw new InvalidOperationException("The selected project does not exist.");
    }

    private static string NormalizeRequiredValue(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", paramName);
        }

        return value.Trim();
    }

    private static string? NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        string trimmed = processName.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }

    private sealed class ActiveProjectSession
    {
        private readonly Dictionary<string, TimeSpan> _completedFocusDurations = new(StringComparer.OrdinalIgnoreCase);

        public ActiveProjectSession(ProjectDefinition project, DateTimeOffset startedAt)
        {
            Project = project;
            StartedAt = startedAt;
        }

        public ProjectDefinition Project { get; }

        public DateTimeOffset StartedAt { get; }

        public TrackedApplication? FocusedProgram { get; private set; }

        public DateTimeOffset? FocusStartedAt { get; private set; }

        public void SetFocusedProgram(TrackedApplication? program, DateTimeOffset observedAt)
        {
            FocusedProgram = program;
            FocusStartedAt = program is null ? null : observedAt;
        }

        public void CloseFocusedProgram(DateTimeOffset observedAt)
        {
            if (FocusedProgram is null || FocusStartedAt is null)
            {
                return;
            }

            if (observedAt > FocusStartedAt.Value)
            {
                _completedFocusDurations[FocusedProgram.ProcessName] =
                    _completedFocusDurations.GetValueOrDefault(FocusedProgram.ProcessName, TimeSpan.Zero) +
                    (observedAt - FocusStartedAt.Value);
            }

            FocusedProgram = null;
            FocusStartedAt = null;
        }

        public IReadOnlyList<ProgramFocusSummary> GetProgramSummaries()
        {
            return GetProgramSummaries(DateTimeOffset.MinValue);
        }

        public IReadOnlyList<ProgramFocusSummary> GetProgramSummaries(DateTimeOffset observedAt)
        {
            Dictionary<string, TimeSpan> durations = new(_completedFocusDurations, StringComparer.OrdinalIgnoreCase);

            if (FocusedProgram is not null &&
                FocusStartedAt is not null &&
                observedAt >= FocusStartedAt.Value)
            {
                durations[FocusedProgram.ProcessName] =
                    durations.GetValueOrDefault(FocusedProgram.ProcessName, TimeSpan.Zero) +
                    (observedAt - FocusStartedAt.Value);
            }

            return [.. durations
                .Select(pair =>
                {
                    TrackedApplication program = Project.FindProgram(pair.Key)
                        ?? new TrackedApplication(pair.Key, pair.Key);

                    return new ProgramFocusSummary(program, pair.Value);
                })
                .OrderByDescending(static summary => summary.FocusDuration)
                .ThenBy(static summary => summary.Program.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
        }
    }
}
