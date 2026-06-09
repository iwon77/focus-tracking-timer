using System.Collections.ObjectModel;

namespace FocusTrackingTimer.Core.Tracking;

public sealed class ProjectTimerEngine
{
    private readonly List<ProjectDefinition> _projects = [];
    private readonly List<ProjectTimerRecord> _completedRecords = [];
    private ActiveProjectSession? _activeSession;

    public bool IsRunning => _activeSession is not null;

    public bool IsPaused => _activeSession?.IsPaused ?? false;

    public Guid? ActiveProjectId => _activeSession?.Project.Id;

    public string? ActiveProjectName => _activeSession?.Project.Name;

    public DateTimeOffset? ActiveStartedAt => _activeSession?.StartedAt;

    public string? ActiveFocusedProgramName => _activeSession?.FocusedProgram?.DisplayName;

    public string? ActiveFocusedProcessName => _activeSession?.FocusedProgram?.ProcessName;

    public ReadOnlyCollection<ProjectDefinition> Projects => new(_projects
        .Where(static project => !project.IsDeleted)
        .ToList());

    public ReadOnlyCollection<ProjectTimerRecord> CompletedRecords => _completedRecords.AsReadOnly();

    public ProjectTimerEngineState CreateStateSnapshot()
    {
        return new ProjectTimerEngineState(
            _projects.Select(project => new ProjectState(
                project.Id,
                project.Name,
                project.RegisteredProgramInfos,
                project.IsDeleted,
                project.CreatedAt,
                project.IsPinned,
                project.Memo,
                project.MemoUpdatedAt)),
            _completedRecords);
    }

    public void ReplaceState(ProjectTimerEngineState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (_activeSession is not null)
        {
            throw new InvalidOperationException("Cannot replace state while a project session is running.");
        }

        _projects.Clear();
        _completedRecords.Clear();

        HashSet<Guid> projectIds = [];
        foreach (ProjectState projectState in state.Projects)
        {
            if (!projectIds.Add(projectState.Id))
            {
                throw new InvalidOperationException("Project ids must be unique.");
            }

            ProjectDefinition project = new(
                projectState.Id,
                projectState.Name,
                projectState.IsDeleted,
                projectState.CreatedAt,
                projectState.IsPinned,
                projectState.Memo,
                projectState.MemoUpdatedAt);
            project.ReplaceRegisteredPrograms(projectState.RegisteredPrograms);
            _projects.Add(project);
        }

        foreach (ProjectTimerRecord record in state.CompletedRecords)
        {
            if (!projectIds.Contains(record.ProjectId))
            {
                throw new InvalidOperationException("Completed records must reference an existing project.");
            }

            _completedRecords.Add(record);
        }
    }

    public bool TryAddProject(string name, out ProjectDefinition project)
    {
        string normalizedName = NormalizeRequiredValue(name, nameof(name));
        if (normalizedName.Length > ProjectDefinition.MaxNameLength)
        {
            project = null!;
            return false;
        }

        ProjectDefinition? existingProject = _projects.FirstOrDefault(item =>
            !item.IsDeleted &&
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
        if (normalizedName.Length > ProjectDefinition.MaxNameLength)
        {
            return false;
        }

        ProjectDefinition project = GetRequiredProject(projectId);

        if (_projects.Any(item =>
                item.Id != projectId &&
                !item.IsDeleted &&
                string.Equals(item.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        project.Rename(normalizedName);
        return true;
    }

    public bool TrySetProjectPinned(Guid projectId, bool isPinned)
    {
        int index = _projects.FindIndex(item => item.Id == projectId && !item.IsDeleted);
        if (index < 0)
        {
            return false;
        }

        ProjectDefinition project = _projects[index];
        project.SetPinned(isPinned);
        _projects.RemoveAt(index);

        if (isPinned)
        {
            _projects.Insert(0, project);
        }
        else
        {
            int insertIndex = _projects.FindLastIndex(static item => item.IsPinned) + 1;
            _projects.Insert(insertIndex, project);
        }

        return true;
    }

    public void UpdateProjectMemo(Guid projectId, string memo, DateTimeOffset? updatedAt = null)
    {
        ProjectDefinition project = GetRequiredProject(projectId);
        project.UpdateMemo(memo, updatedAt ?? DateTimeOffset.Now);
    }

    public bool TryRemoveProject(Guid projectId)
    {
        if (ActiveProjectId == projectId)
        {
            return false;
        }

        ProjectDefinition? project = _projects.FirstOrDefault(item => item.Id == projectId && !item.IsDeleted);
        if (project is null)
        {
            return false;
        }

        project.MarkDeleted();
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

    public bool TrySetProgramPinned(Guid projectId, string processName, bool isPinned)
    {
        ProjectDefinition project = GetRequiredProject(projectId);
        return project.TrySetProgramPinned(processName, isPinned);
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

    public void PauseProject(DateTimeOffset pausedAt)
    {
        ActiveProjectSession session = _activeSession ?? throw new InvalidOperationException("No running project session.");
        session.Pause(pausedAt);
    }

    public void ResumeProject(DateTimeOffset resumedAt)
    {
        ActiveProjectSession session = _activeSession ?? throw new InvalidOperationException("No running project session.");
        session.Resume(resumedAt);
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
            session.GetFocusSegments());

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

        if (_activeSession.IsPaused)
        {
            _activeSession.CloseFocusedProgram(observedAt);
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

        return _activeSession.GetWallClockDuration(observedAt);
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

        return BuildSortedProgramSummaries(project, summaryByProcessName, sortMode);
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

        return BuildSortedProgramSummaries(project, summaryByProcessName, sortMode);
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

    public IReadOnlyList<ProjectTimerRecordSlice> GetRecordSlices(
        DateOnly fromDate,
        DateOnly toDate,
        DateTimeOffset observedAt,
        Guid? projectId = null)
    {
        return ProjectTimerRecordSummaryBuilder.BuildRecordSlices(
            GetRecordsForSummary(projectId, observedAt),
            fromDate,
            toDate);
    }

    public IReadOnlyList<ProjectTimerRecordSlice> GetActiveRecordSlices(
        DateOnly fromDate,
        DateOnly toDate,
        DateTimeOffset observedAt,
        Guid? projectId = null)
    {
        ProjectTimerRecord? activeRecord = GetActiveRecordForSummary(observedAt, projectId);
        return ProjectTimerRecordSummaryBuilder.BuildRecordSlices(
            activeRecord is null ? [] : [activeRecord],
            fromDate,
            toDate);
    }

    public IReadOnlyList<DailyDurationSummary> GetDailyDurationSummaries(
        DateOnly fromDate,
        DateOnly toDate,
        DateTimeOffset observedAt,
        Guid? projectId = null)
    {
        return ProjectTimerRecordSummaryBuilder.BuildDailyDurationSummaries(
            GetRecordsForSummary(projectId, observedAt),
            fromDate,
            toDate);
    }

    public IReadOnlyList<DailyDurationSummary> GetActiveDailyDurationSummaries(
        DateOnly fromDate,
        DateOnly toDate,
        DateTimeOffset observedAt,
        Guid? projectId = null)
    {
        ProjectTimerRecord? activeRecord = GetActiveRecordForSummary(observedAt, projectId);
        return ProjectTimerRecordSummaryBuilder.BuildDailyDurationSummaries(
            activeRecord is null ? [] : [activeRecord],
            fromDate,
            toDate);
    }

    public IReadOnlyList<DailyProjectDurationSummary> GetDailyProjectDurationSummaries(
        DateOnly fromDate,
        DateOnly toDate,
        DateTimeOffset observedAt,
        Guid? projectId = null)
    {
        return ProjectTimerRecordSummaryBuilder.BuildDailyProjectDurationSummaries(
            GetRecordsForSummary(projectId, observedAt),
            fromDate,
            toDate);
    }

    public TimeSpan GetTodayDuration(DateOnly today, DateTimeOffset observedAt, Guid? projectId = null)
    {
        return GetDailyDurationSummaries(today, today, observedAt, projectId)[0].TotalDuration;
    }

    private ProjectTimerRecord? GetActiveRecordForSummary(DateTimeOffset observedAt, Guid? projectId)
    {
        if (_activeSession is null ||
            (projectId.HasValue && _activeSession.Project.Id != projectId.Value))
        {
            return null;
        }

        DateTimeOffset summaryEndedAt = _activeSession.GetSummaryEndedAt(observedAt);
        return new ProjectTimerRecord(
            _activeSession.Project.Id,
            _activeSession.Project.Name,
            _activeSession.StartedAt,
            summaryEndedAt,
            _activeSession.GetFocusSegments(summaryEndedAt));
    }

    private IEnumerable<ProjectTimerRecord> GetRecordsForSummary(Guid? projectId, DateTimeOffset observedAt)
    {
        IEnumerable<ProjectTimerRecord> records = projectId.HasValue
            ? _completedRecords.Where(record => record.ProjectId == projectId.Value)
            : _completedRecords;
        ProjectTimerRecord? activeRecord = GetActiveRecordForSummary(observedAt, projectId);
        return activeRecord is null ? records : records.Append(activeRecord);
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

    private static IReadOnlyList<ProgramFocusSummary> BuildSortedProgramSummaries(
        ProjectDefinition project,
        Dictionary<string, ProgramFocusSummary> summaryByProcessName,
        ProgramSortMode sortMode)
    {
        List<RegisteredProgramInfo> pinnedRegistrations = [.. project.RegisteredProgramInfos.Where(static item => item.IsPinned)];
        List<RegisteredProgramInfo> unpinnedRegistrations = [.. project.RegisteredProgramInfos.Where(static item => !item.IsPinned)];

        IEnumerable<RegisteredProgramInfo> sortedUnpinnedRegistrations = sortMode switch
        {
            ProgramSortMode.RegisteredDescending => unpinnedRegistrations.AsEnumerable().Reverse(),
            ProgramSortMode.DisplayName => unpinnedRegistrations
                .OrderBy(static item => item.Program.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            ProgramSortMode.MostUsed => unpinnedRegistrations
                .OrderByDescending(item => summaryByProcessName[item.Program.ProcessName].FocusDuration)
                .ThenBy(static item => item.Program.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            _ => unpinnedRegistrations
        };

        return [.. pinnedRegistrations
            .Concat(sortedUnpinnedRegistrations)
            .Select(registration => summaryByProcessName[registration.Program.ProcessName])];
    }

    private static TimeSpan SumProgramFocusDuration(IEnumerable<ProgramFocusSummary> summaries)
    {
        return summaries.Aggregate(TimeSpan.Zero, static (total, summary) => total + summary.FocusDuration);
    }

    private ProjectDefinition GetRequiredProject(Guid projectId)
    {
        ProjectDefinition? project = _projects.FirstOrDefault(item => item.Id == projectId && !item.IsDeleted);
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
        private readonly List<ProgramFocusSegment> _completedFocusSegments = [];

        public ActiveProjectSession(ProjectDefinition project, DateTimeOffset startedAt)
        {
            Project = project;
            StartedAt = startedAt;
        }

        public ProjectDefinition Project { get; }

        public DateTimeOffset StartedAt { get; }

        public bool IsPaused => PauseStartedAt is not null;

        public TrackedApplication? FocusedProgram { get; private set; }

        public DateTimeOffset? FocusStartedAt { get; private set; }

        private DateTimeOffset? PauseStartedAt { get; set; }

        private TimeSpan PausedDuration { get; set; }

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
                _completedFocusSegments.Add(new ProgramFocusSegment(FocusedProgram, FocusStartedAt.Value, observedAt));
            }

            FocusedProgram = null;
            FocusStartedAt = null;
        }

        public void Pause(DateTimeOffset pausedAt)
        {
            if (pausedAt < StartedAt)
            {
                throw new ArgumentOutOfRangeException(nameof(pausedAt), "Pause time cannot be before start time.");
            }

            if (IsPaused)
            {
                return;
            }

            CloseFocusedProgram(pausedAt);
            PauseStartedAt = pausedAt;
        }

        public void Resume(DateTimeOffset resumedAt)
        {
            if (PauseStartedAt is null)
            {
                return;
            }

            if (resumedAt < PauseStartedAt.Value)
            {
                throw new ArgumentOutOfRangeException(nameof(resumedAt), "Resume time cannot be before pause time.");
            }

            PausedDuration += resumedAt - PauseStartedAt.Value;
            PauseStartedAt = null;
        }

        public TimeSpan GetWallClockDuration(DateTimeOffset observedAt)
        {
            if (observedAt < StartedAt)
            {
                return TimeSpan.Zero;
            }

            TimeSpan currentPauseDuration = PausedDuration;
            if (PauseStartedAt is not null && observedAt > PauseStartedAt.Value)
            {
                currentPauseDuration += observedAt - PauseStartedAt.Value;
            }

            TimeSpan duration = observedAt - StartedAt - currentPauseDuration;
            return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        }

        public DateTimeOffset GetSummaryEndedAt(DateTimeOffset observedAt)
        {
            if (observedAt < StartedAt)
            {
                return StartedAt;
            }

            if (PauseStartedAt is not null && observedAt > PauseStartedAt.Value)
            {
                return PauseStartedAt.Value;
            }

            return observedAt;
        }

        public List<ProgramFocusSegment> GetFocusSegments()
        {
            return [.. _completedFocusSegments];
        }

        public List<ProgramFocusSegment> GetFocusSegments(DateTimeOffset observedAt)
        {
            List<ProgramFocusSegment> segments = [.. _completedFocusSegments];

            if (FocusedProgram is not null &&
                FocusStartedAt is not null &&
                observedAt >= FocusStartedAt.Value)
            {
                segments.Add(new ProgramFocusSegment(FocusedProgram, FocusStartedAt.Value, observedAt));
            }

            return segments;
        }

        public IReadOnlyList<ProgramFocusSummary> GetProgramSummaries()
        {
            return GetProgramSummaries(DateTimeOffset.MinValue);
        }

        public IReadOnlyList<ProgramFocusSummary> GetProgramSummaries(DateTimeOffset observedAt)
        {
            Dictionary<string, ProgramFocusSummary> summaries = new(StringComparer.OrdinalIgnoreCase);

            foreach (ProgramFocusSegment segment in GetFocusSegments(observedAt))
            {
                AddOrUpdateSummary(summaries, segment.Program, segment.FocusDuration);
            }

            return [.. summaries.Values
                .OrderByDescending(static summary => summary.FocusDuration)
                .ThenBy(static summary => summary.Program.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
        }
    }
}
