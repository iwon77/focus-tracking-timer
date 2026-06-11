using System.Windows;
using System.Windows.Media;
using FocusTrackingTimer.App.Infrastructure;
using FocusTrackingTimer.App.ViewModels;
using FocusTrackingTimer.Core.Persistence;
using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App.Features.Timer;

internal sealed class TimerFeatureController
{
    private static readonly IReadOnlyDictionary<string, ProcessRunState> EmptyProcessStates =
        new Dictionary<string, ProcessRunState>(StringComparer.OrdinalIgnoreCase);

    private readonly Window _owner;
    private readonly ProjectTimerEngine _engine;
    private readonly SqliteProjectTimerStore _store;
    private readonly TimerViewModel _viewModel;
    private readonly int _currentProcessId;
    private readonly Func<IReadOnlyDictionary<string, ProcessRunState>> _getLatestProcessStates;
    private readonly Action _persistProjectCatalog;
    private readonly Action<ProjectTimerRecord> _appendCompletedRecord;
    private readonly Action<DateTimeOffset, string, bool, bool> _refreshUiAfterCommand;
    private readonly Brush _startButtonBackground;
    private readonly Brush _stopButtonBackground;
    private readonly Brush _disabledButtonBackground;
    private readonly Brush _startButtonForeground;
    private readonly Brush _defaultButtonForeground;
    private readonly Dictionary<Guid, TimeSpan> _persistedTodayDurationByProjectId = [];
    private readonly Dictionary<Guid, (DateTimeOffset StartedAt, DateTimeOffset EndedAt)?> _recentRecordPeriodByProjectId = [];
    private DateOnly _cachedTodayDate = DateOnly.FromDateTime(DateTime.Now.Date);
    private bool _projectRowsDirty = true;
    private ProjectSortMode? _projectRowsSortMode;
    private bool _registeredProgramRowsDirty = true;
    private Guid? _registeredProgramRowsProjectId;
    private ProgramSortMode? _registeredProgramRowsSortMode;

    public TimerFeatureController(
        Window owner,
        ProjectTimerEngine engine,
        SqliteProjectTimerStore store,
        TimerViewModel viewModel,
        int currentProcessId,
        Func<IReadOnlyDictionary<string, ProcessRunState>> getLatestProcessStates,
        Action persistProjectCatalog,
        Action<ProjectTimerRecord> appendCompletedRecord,
        Action<DateTimeOffset, string, bool, bool> refreshUiAfterCommand,
        Brush startButtonBackground,
        Brush stopButtonBackground,
        Brush disabledButtonBackground,
        Brush startButtonForeground,
        Brush defaultButtonForeground)
    {
        _owner = owner;
        _engine = engine;
        _store = store;
        _viewModel = viewModel;
        _currentProcessId = currentProcessId;
        _getLatestProcessStates = getLatestProcessStates;
        _persistProjectCatalog = persistProjectCatalog;
        _appendCompletedRecord = appendCompletedRecord;
        _refreshUiAfterCommand = refreshUiAfterCommand;
        _startButtonBackground = startButtonBackground;
        _stopButtonBackground = stopButtonBackground;
        _disabledButtonBackground = disabledButtonBackground;
        _startButtonForeground = startButtonForeground;
        _defaultButtonForeground = defaultButtonForeground;
    }

    public ProjectDefinition? SelectedProject { get; private set; }

    public void AddProject()
    {
        string projectName = GetNextDefaultProjectName();

        if (!_engine.TryAddProject(projectName, out ProjectDefinition project))
        {
            _viewModel.TimerStatusText = "작업을 추가하지 못했습니다.";
            return;
        }

        SetSelectedProject(project);
        MarkProjectRowsDirty();
        InvalidateProjectCaches(project.Id);
        _persistProjectCatalog();
        RefreshProjectFiltersAndVisibleRecord(DateTimeOffset.Now, $"'{project.Name}' 작업을 추가했습니다.");
    }

    public void ToggleProjectPin(ProjectSidebarRow? row)
    {
        if (row is null)
        {
            return;
        }

        ToggleProjectPin(row.ProjectId);
    }

    private void ToggleProjectPin(Guid projectId)
    {
        ProjectDefinition? project = _engine.Projects.FirstOrDefault(item => item.Id == projectId);
        if (project is null)
        {
            return;
        }

        bool isPinned = !project.IsPinned;
        if (!_engine.TrySetProjectPinned(project.Id, isPinned))
        {
            return;
        }

        SetSelectedProject(_engine.Projects.FirstOrDefault(item => item.Id == project.Id));
        MarkProjectRowsDirty();
        _persistProjectCatalog();
        _refreshAll(DateTimeOffset.Now, isPinned ? "작업을 고정했습니다." : "작업 고정을 해제했습니다.");
    }

    public void EditSelectedProjectMemo()
    {
        if (SelectedProject is null)
        {
            return;
        }

        ProjectMemoDialog dialog = new(
            SelectedProject.Name,
            SelectedProject.CreatedAt,
            SelectedProject.MemoUpdatedAt,
            SelectedProject.Memo)
        {
            Owner = _owner
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _engine.UpdateProjectMemo(SelectedProject.Id, dialog.MemoText, DateTimeOffset.Now);
        SetSelectedProject(_engine.Projects.FirstOrDefault(item => item.Id == SelectedProject.Id));
        _persistProjectCatalog();
        _refreshAll(DateTimeOffset.Now, "작업 메모를 저장했습니다.");
    }

    public void DeleteSelectedProject()
    {
        if (SelectedProject is null)
        {
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            _owner,
            $"'{SelectedProject.Name}' 작업을 삭제하시겠습니까?",
            "작업 삭제",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        Guid projectId = SelectedProject.Id;
        if (!_engine.TryRemoveProject(projectId))
        {
            _viewModel.TimerStatusText = "실행 중인 작업은 삭제할 수 없습니다.";
            return;
        }

        InvalidateProjectCaches(projectId);
        SetSelectedProject(_engine.Projects.FirstOrDefault());
        MarkProjectRowsDirty();
        _persistProjectCatalog();
        RefreshProjectFiltersAndVisibleRecord(DateTimeOffset.Now, "작업을 삭제했습니다.");
    }

    public void EditSelectedProject()
    {
        if (SelectedProject is null)
        {
            return;
        }

        Guid projectId = SelectedProject.Id;
        NameEditDialog dialog = new("작업 이름 수정", SelectedProject.Name, maxNameLength: ProjectDefinition.MaxNameLength)
        {
            Owner = _owner
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string newName = dialog.NameValue.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            MessageBox.Show(_owner, "작업 이름은 비워둘 수 없습니다.", "작업 이름 수정");
            return;
        }

        if (newName.Length > ProjectDefinition.MaxNameLength)
        {
            MessageBox.Show(_owner, $"작업 이름은 {ProjectDefinition.MaxNameLength}자 이하로 입력해주세요.", "작업 이름 수정");
            return;
        }

        if (!_engine.TryRenameProject(projectId, newName))
        {
            MessageBox.Show(_owner, "이미 사용 중인 작업 이름입니다.", "작업 이름 수정");
            return;
        }

        SetSelectedProject(_engine.Projects.FirstOrDefault(item => item.Id == projectId));
        MarkProjectRowsDirty();
        _persistProjectCatalog();
        RefreshProjectFiltersAndVisibleRecord(DateTimeOffset.Now, "작업 이름을 변경했습니다.");
    }

    public void SelectProject(ProjectSidebarRow? row)
    {
        if (row is null)
        {
            return;
        }

        ProjectDefinition? project = _engine.Projects.FirstOrDefault(item => item.Id == row.ProjectId);
        if (project is null)
        {
            return;
        }

        SetSelectedProject(project);
        RefreshSelectedProjectArea(DateTimeOffset.Now, "선택한 작업을 표시합니다.", allowPersistentReload: true);
    }

    public void SelectActiveProject()
    {
        if (!_engine.ActiveProjectId.HasValue)
        {
            return;
        }

        ProjectDefinition? project = _engine.Projects.FirstOrDefault(item => item.Id == _engine.ActiveProjectId.Value);
        if (project is null)
        {
            return;
        }

        SetSelectedProject(project);
        _viewModel.SelectedProjectRow = FindProjectRow(project.Id);
        RefreshSelectedProjectArea(DateTimeOffset.Now, "실행 중인 작업을 표시합니다.", allowPersistentReload: true);
    }

    public void ToggleTimerOrPause()
    {
        if (SelectedProject is null)
        {
            _viewModel.SelectedProjectTitle = "먼저 작업을 선택해주세요.";
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;

        if (!_engine.IsRunning)
        {
            _engine.StartProject(SelectedProject.Id, now);
            RefreshVisibleRecord(now, $"'{SelectedProject.Name}' 타이머를 시작했습니다. 등록 프로그램 포커스 중에만 시간이 흐릅니다.");
            return;
        }

        if (_engine.ActiveProjectId == SelectedProject.Id)
        {
            if (_engine.IsPaused)
            {
                _engine.ResumeProject(now);
                RefreshVisibleRecord(now, $"'{SelectedProject.Name}' 타이머를 재개했습니다.");
                return;
            }

            _engine.PauseProject(now);
            RefreshVisibleRecord(now, $"'{SelectedProject.Name}' 타이머를 일시정지했습니다.");
        }
    }

    public void StopTimer()
    {
        if (SelectedProject is null ||
            !_engine.IsRunning ||
            _engine.ActiveProjectId != SelectedProject.Id)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        ProjectTimerRecord record = _engine.StopProject(now);
        _persistProjectCatalog();
        _appendCompletedRecord(record);
        InvalidateProjectCaches(record.ProjectId);
        RefreshVisibleRecord(now, $"'{record.ProjectName}' 타이머를 종료했습니다.");
    }

    public void OpenProgramManager()
    {
        if (SelectedProject is null)
        {
            _viewModel.TimerStatusText = "프로그램을 추가하려면 먼저 작업을 선택해주세요.";
            return;
        }

        ProgramManagerWindow manager = new(_engine, SelectedProject, _currentProcessId, () =>
        {
            _persistProjectCatalog();
            MarkRegisteredProgramRowsDirty();
            _refreshAll(DateTimeOffset.Now, "등록 프로그램 변경사항을 반영했습니다.");
        })
        {
            Owner = _owner
        };
        manager.Show();
    }

    public void EditProgram(RegisteredProgramRow? row)
    {
        if (SelectedProject is null || row is null)
        {
            return;
        }

        string placeholderName = string.IsNullOrWhiteSpace(row.InitialDisplayName)
            ? row.ProcessName
            : row.InitialDisplayName;
        string currentName = string.IsNullOrWhiteSpace(row.DisplayName)
            ? string.Empty
            : row.DisplayName;
        NameEditDialog dialog = new("프로그램 표시 이름 수정", currentName, placeholderName)
        {
            Owner = _owner
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string displayName = string.IsNullOrWhiteSpace(dialog.NameValue)
            ? placeholderName
            : dialog.NameValue.Trim();

        DateTimeOffset now = DateTimeOffset.Now;
        if (string.Equals(_engine.ActiveFocusedProcessName, row.ProcessName, StringComparison.OrdinalIgnoreCase))
        {
            _engine.ObserveFocusedProgram(null, now);
        }

        _ = _engine.TryUpdateProgram(SelectedProject.Id, row.ProcessName, new TrackedApplication(row.ProcessName, displayName));
        _persistProjectCatalog();
        MarkRegisteredProgramRowsDirty();

        if (_engine.IsRunning && _engine.ActiveProjectId == SelectedProject.Id)
        {
            FocusObservation observation = ForegroundWindowTracker.GetCurrentFocusedApplication(_currentProcessId);
            _engine.ObserveFocusedProgram(observation.Application?.ProcessName, now);
        }

        RefreshVisibleRecord(now, "프로그램 표시 이름을 변경했습니다.");
    }

    public void DeleteProgram(RegisteredProgramRow? row)
    {
        if (SelectedProject is null || row is null)
        {
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            _owner,
            $"'{row.DisplayName}' 프로그램을 삭제하시겠습니까?",
            "등록 프로그램 삭제",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        if (string.Equals(_engine.ActiveFocusedProcessName, row.ProcessName, StringComparison.OrdinalIgnoreCase))
        {
            _engine.ObserveFocusedProgram(null, now);
        }

        _ = _engine.TryRemoveProgram(SelectedProject.Id, row.ProcessName);
        _persistProjectCatalog();
        MarkRegisteredProgramRowsDirty();
        RefreshVisibleRecord(now, "등록 프로그램을 삭제했습니다.");
    }

    public void ToggleProgramPin(RegisteredProgramRow? row)
    {
        if (SelectedProject is null || row is null)
        {
            return;
        }

        bool isPinned = !row.IsPinned;
        if (!_engine.TrySetProgramPinned(SelectedProject.Id, row.ProcessName, isPinned))
        {
            return;
        }

        _persistProjectCatalog();
        MarkRegisteredProgramRowsDirty();
        _refreshAll(DateTimeOffset.Now, isPinned ? "등록 프로그램을 고정했습니다." : "등록 프로그램 고정을 해제했습니다.");
    }

    public void FocusRegisteredProgram(RegisteredProgramRow? row)
    {
        if (row is null)
        {
            return;
        }

        if (!WindowFocusService.TryFocusProcessMainWindow(row.ProcessName))
        {
            MessageBox.Show(_owner, "프로그램 창을 앞으로 가져오지 못했습니다.", "프로그램 보기");
        }
    }

    public string RefreshFocusTracking(
        DateTimeOffset observedAt,
        IReadOnlyDictionary<string, ProcessRunState>? processStates = null)
    {
        FocusObservation observation = ForegroundWindowTracker.GetCurrentFocusedApplication(_currentProcessId);
        return RefreshFocusTracking(observedAt, observation, processStates);
    }

    public string RefreshFocusTracking(
        DateTimeOffset observedAt,
        IntPtr foregroundWindowHandle,
        IReadOnlyDictionary<string, ProcessRunState>? processStates = null)
    {
        FocusObservation observation = ForegroundWindowTracker.GetFocusedApplication(foregroundWindowHandle, _currentProcessId);
        return RefreshFocusTracking(observedAt, observation, processStates);
    }

    private string RefreshFocusTracking(
        DateTimeOffset observedAt,
        FocusObservation observation,
        IReadOnlyDictionary<string, ProcessRunState>? processStates)
    {
        if (!_engine.IsRunning)
        {
            return "타이머 대기 중입니다. 시작 후 등록 프로그램이 포커스된 시간만 기록합니다.";
        }

        if (_engine.IsPaused)
        {
            return "타이머가 일시정지 중입니다.";
        }

        IReadOnlyDictionary<string, ProcessRunState> runtimeProcessStates = ResolveProcessStates(processStates);
        string? focusableProcessName = TimerProgramFocusStatus.GetFocusableObservedProcessName(observation.Application, runtimeProcessStates);
        _engine.ObserveFocusedProgram(focusableProcessName, observedAt);

        if (observation.Application is null)
        {
            return observation.StatusMessage;
        }

        if (focusableProcessName is null)
        {
            return "현재 포커스된 프로그램은 포커스 기록 가능한 창 상태가 아닙니다.";
        }

        return _engine.ActiveFocusedProgramName is null
            ? "현재 포커스된 프로그램은 등록되어 있지 않습니다."
            : observation.StatusMessage;
    }

    public void RefreshProjectSidebar(DateTimeOffset observedAt)
    {
        ProjectDefinition? activeProject = _engine.ActiveProjectId.HasValue
            ? _engine.Projects.FirstOrDefault(item => item.Id == _engine.ActiveProjectId.Value)
            : null;

        if (activeProject is null)
        {
            _viewModel.RunningProjectNameText = "-";
            _viewModel.RunningProjectWallClockText = "-";
            _viewModel.RunningProjectFocusText = "-";
        }
        else
        {
            _viewModel.RunningProjectNameText = activeProject.Name;
            _viewModel.RunningProjectWallClockText = AppTimeFormatter.FormatDuration(
                _engine.GetCurrentWallClockDuration(activeProject.Id, observedAt));
            _viewModel.RunningProjectFocusText = AppTimeFormatter.FormatDuration(
                _engine.GetCurrentRunDuration(activeProject.Id, observedAt));
        }

        ProjectSortMode sortMode = _viewModel.SelectedProjectSortOption?.Mode ?? ProjectSortMode.Created;
        if (_projectRowsDirty || _projectRowsSortMode != sortMode)
        {
            RebuildProjectRows(sortMode);
        }

        SynchronizeSelectedProjectRow();
    }

    public void RefreshSelectedProjectArea(
        DateTimeOffset observedAt,
        string message,
        IReadOnlyDictionary<string, ProcessRunState>? processStates = null,
        bool allowPersistentReload = false)
    {
        _viewModel.IsProjectEditEnabled = SelectedProject is not null;
        _viewModel.IsProjectDeleteEnabled = SelectedProject is not null && _engine.ActiveProjectId != SelectedProject.Id;
        _viewModel.IsProjectMemoEnabled = SelectedProject is not null;

        if (SelectedProject is null)
        {
            _viewModel.SelectedProjectTitle = "작업을 추가해보세요";
            _viewModel.ActiveSessionPeriodText = "최근 작업 일시: -";
            _viewModel.TimerStatusText = message;
            _viewModel.FocusStatusText = "-";
            _viewModel.ActiveProjectWallClockText = "00:00:00";
            _viewModel.ActiveProjectElapsedText = "00:00:00";
            _viewModel.SelectedProjectTodayText = "00:00:00";
            _viewModel.IsTimerActionEnabled = false;
            _viewModel.IsTimerStopEnabled = false;
            _viewModel.TimerActionButtonText = "시작";
            _viewModel.TimerActionButtonBackground = _disabledButtonBackground;
            _viewModel.TimerActionButtonForeground = _defaultButtonForeground;
            if (_viewModel.RegisteredProgramRows.Count > 0)
            {
                _viewModel.RegisteredProgramRows.Clear();
            }

            _registeredProgramRowsDirty = true;
            _registeredProgramRowsProjectId = null;
            _registeredProgramRowsSortMode = null;
            return;
        }

        bool isActiveProject = _engine.ActiveProjectId == SelectedProject.Id;
        ProgramSortMode sortMode = _viewModel.SelectedProgramSortOption?.Mode ?? ProgramSortMode.MostUsed;
        IReadOnlyDictionary<string, ProcessRunState> runtimeProcessStates = ResolveProcessStates(processStates);
        IReadOnlyList<RegisteredProgramInfo> registeredProgramInfos = _engine.GetRegisteredProgramInfos(SelectedProject.Id);
        Dictionary<string, string> initialDisplayNameByProcessName = registeredProgramInfos
            .ToDictionary(
                info => info.Program.ProcessName,
                info => info.InitialDisplayName,
                StringComparer.OrdinalIgnoreCase);
        Dictionary<string, RegisteredProgramInfo> registrationByProcessName = registeredProgramInfos
            .ToDictionary(
                info => info.Program.ProcessName,
                StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<ProgramFocusSummary> programSummaries = _engine.GetCurrentSessionProgramSummaries(
            SelectedProject.Id,
            observedAt,
            sortMode);
        if (ShouldRebuildRegisteredProgramRows(programSummaries, sortMode))
        {
            _viewModel.RegisteredProgramRows.Clear();
        bool hasPinnedPrograms = programSummaries.Any(summary =>
            registrationByProcessName.GetValueOrDefault(summary.Program.ProcessName)?.IsPinned == true);
        bool showedPinnedDivider = false;

        foreach (ProgramFocusSummary summary in programSummaries)
        {
            (Brush statusBrush, string statusText) = TimerProgramFocusStatus.GetRuntimeStatus(summary.Program.ProcessName, runtimeProcessStates);
            bool isPinned = registrationByProcessName.GetValueOrDefault(summary.Program.ProcessName)?.IsPinned ?? false;
            bool showsPinnedDivider = hasPinnedPrograms && !isPinned && !showedPinnedDivider;
            showedPinnedDivider |= showsPinnedDivider;
            _viewModel.RegisteredProgramRows.Add(new RegisteredProgramRow(
                summary.Program.DisplayName,
                summary.Program.ProcessName,
                isActiveProject ? AppTimeFormatter.FormatDuration(summary.FocusDuration) : "00:00:00",
                InitialDisplayName: initialDisplayNameByProcessName.GetValueOrDefault(
                    summary.Program.ProcessName,
                    summary.Program.DisplayName),
                StatusBrush: statusBrush,
                StatusText: statusText,
                IsPinned: isPinned,
                PinButtonText: isPinned ? "해제" : "고정",
                ShowsPinnedDivider: showsPinnedDivider));
        }

            _registeredProgramRowsDirty = false;
            _registeredProgramRowsProjectId = SelectedProject.Id;
            _registeredProgramRowsSortMode = sortMode;
        }
        else
        {
            UpdateRegisteredProgramRows(programSummaries, runtimeProcessStates, isActiveProject);
        }

        bool anotherProjectIsRunning = _engine.IsRunning && !isActiveProject;

        _viewModel.SelectedProjectTitle = SelectedProject.Name;
        _viewModel.ActiveSessionPeriodText = TimerProjectDisplayService.GetProjectPeriodText(
            _engine,
            SelectedProject,
            isActiveProject,
            GetRecentRecordPeriod(SelectedProject.Id, allowPersistentReload));
        _viewModel.ActiveProjectElapsedText = isActiveProject
            ? AppTimeFormatter.FormatDuration(_engine.GetCurrentRunDuration(SelectedProject.Id, observedAt))
            : "00:00:00";
        _viewModel.ActiveProjectWallClockText = isActiveProject
            ? AppTimeFormatter.FormatDuration(_engine.GetCurrentWallClockDuration(SelectedProject.Id, observedAt))
            : "00:00:00";
        _viewModel.SelectedProjectTodayText = AppTimeFormatter.FormatDuration(
            GetSelectedProjectTodayDuration(observedAt, allowPersistentReload));

        _viewModel.FocusStatusText = isActiveProject && !_engine.IsPaused
            ? _engine.ActiveFocusedProgramName ?? "-"
            : "-";

        _viewModel.TimerStatusText = message;
        _viewModel.IsTimerActionEnabled = !anotherProjectIsRunning;
        _viewModel.IsTimerStopEnabled = isActiveProject && _engine.IsRunning;
        _viewModel.TimerActionButtonText = !_engine.IsRunning
            ? "시작"
            : isActiveProject
                ? _engine.IsPaused ? "재개" : "일시정지"
                : "실행 중";
        _viewModel.TimerActionButtonBackground = !_engine.IsRunning
            ? _startButtonBackground
            : isActiveProject
                ? _stopButtonBackground
                : _disabledButtonBackground;
        _viewModel.TimerActionButtonForeground = !_engine.IsRunning
            ? _startButtonForeground
            : _defaultButtonForeground;
    }

    private TimeSpan GetSelectedProjectTodayDuration(DateTimeOffset observedAt, bool allowPersistentReload)
    {
        if (SelectedProject is null)
        {
            return TimeSpan.Zero;
        }

        DateOnly today = DateOnly.FromDateTime(observedAt.LocalDateTime.Date);
        if (_cachedTodayDate != today)
        {
            _cachedTodayDate = today;
            _persistedTodayDurationByProjectId.Clear();
        }

        return GetPersistedTodayDuration(today, SelectedProject.Id, allowPersistentReload);
    }

    private IReadOnlyDictionary<string, ProcessRunState> ResolveProcessStates(
        IReadOnlyDictionary<string, ProcessRunState>? processStates)
    {
        IReadOnlyDictionary<string, ProcessRunState> runtimeProcessStates = processStates ?? _getLatestProcessStates();
        return runtimeProcessStates.Count == 0 ? EmptyProcessStates : runtimeProcessStates;
    }

    private void SetSelectedProject(ProjectDefinition? project)
    {
        Guid? previousProjectId = SelectedProject?.Id;
        Guid? nextProjectId = project?.Id;
        SelectedProject = project;

        if (previousProjectId != nextProjectId)
        {
            _registeredProgramRowsDirty = true;
            _registeredProgramRowsProjectId = null;
            _registeredProgramRowsSortMode = null;
        }
    }

    private void MarkProjectRowsDirty()
    {
        _projectRowsDirty = true;
    }

    private void MarkRegisteredProgramRowsDirty()
    {
        _registeredProgramRowsDirty = true;
        _registeredProgramRowsProjectId = null;
        _registeredProgramRowsSortMode = null;
    }

    private void InvalidateProjectCaches(Guid projectId)
    {
        _persistedTodayDurationByProjectId.Remove(projectId);
        _recentRecordPeriodByProjectId.Remove(projectId);
    }

    private void RebuildProjectRows(ProjectSortMode sortMode)
    {
        Guid? selectedProjectId = SelectedProject?.Id;

        _viewModel.PinnedProjectRows.Clear();
        _viewModel.ProjectRows.Clear();

        foreach (ProjectDefinition project in TimerProjectDisplayService.GetSortedProjects(_engine.Projects, sortMode))
        {
            ProjectSidebarRow row = new(
                project.Id,
                project.Name,
                project.IsPinned);

            if (project.IsPinned)
            {
                _viewModel.PinnedProjectRows.Add(row);
            }
            else
            {
                _viewModel.ProjectRows.Add(row);
            }
        }

        _projectRowsDirty = false;
        _projectRowsSortMode = sortMode;

        if (selectedProjectId.HasValue)
        {
            SetSelectedProject(_engine.Projects.FirstOrDefault(item => item.Id == selectedProjectId.Value));
        }
        else if (SelectedProject is null && _engine.Projects.Count > 0)
        {
            SetSelectedProject(_engine.Projects[0]);
        }
        else if (_engine.Projects.Count == 0)
        {
            SetSelectedProject(null);
        }
    }

    private void SynchronizeSelectedProjectRow()
    {
        if (SelectedProject is null)
        {
            if (_engine.Projects.Count == 0)
            {
                _viewModel.SelectedProjectRow = null;
                return;
            }

            SetSelectedProject(_engine.Projects[0]);
        }

        _viewModel.SelectedProjectRow = SelectedProject is null
            ? null
            : FindProjectRow(SelectedProject.Id);
    }

    private ProjectSidebarRow? FindProjectRow(Guid projectId)
    {
        return _viewModel.PinnedProjectRows.FirstOrDefault(item => item.ProjectId == projectId)
            ?? _viewModel.ProjectRows.FirstOrDefault(item => item.ProjectId == projectId);
    }

    private bool ShouldRebuildRegisteredProgramRows(
        IReadOnlyList<ProgramFocusSummary> programSummaries,
        ProgramSortMode sortMode)
    {
        if (_registeredProgramRowsDirty ||
            _registeredProgramRowsProjectId != SelectedProject?.Id ||
            _registeredProgramRowsSortMode != sortMode ||
            _viewModel.RegisteredProgramRows.Count != programSummaries.Count)
        {
            return true;
        }

        for (int index = 0; index < programSummaries.Count; index++)
        {
            if (!string.Equals(
                _viewModel.RegisteredProgramRows[index].ProcessName,
                programSummaries[index].Program.ProcessName,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateRegisteredProgramRows(
        IReadOnlyList<ProgramFocusSummary> programSummaries,
        IReadOnlyDictionary<string, ProcessRunState> runtimeProcessStates,
        bool isActiveProject)
    {
        for (int index = 0; index < programSummaries.Count; index++)
        {
            ProgramFocusSummary summary = programSummaries[index];
            RegisteredProgramRow row = _viewModel.RegisteredProgramRows[index];
            (Brush statusBrush, string statusText) = TimerProgramFocusStatus.GetRuntimeStatus(summary.Program.ProcessName, runtimeProcessStates);

            _viewModel.RegisteredProgramRows[index] = row with
            {
                FocusDurationText = isActiveProject
                    ? AppTimeFormatter.FormatDuration(summary.FocusDuration)
                    : "00:00:00",
                StatusBrush = statusBrush,
                StatusText = statusText
            };
        }
    }

    private TimeSpan GetPersistedTodayDuration(
        DateOnly today,
        Guid projectId,
        bool allowPersistentReload)
    {
        if (_persistedTodayDurationByProjectId.TryGetValue(projectId, out TimeSpan persistedDuration))
        {
            return persistedDuration;
        }

        if (!allowPersistentReload)
        {
            return TimeSpan.Zero;
        }

        persistedDuration = _store.LoadDailyDurationSummaries(today, today, projectId)[0].TotalDuration;
        _persistedTodayDurationByProjectId[projectId] = persistedDuration;
        return persistedDuration;
    }

    private (DateTimeOffset StartedAt, DateTimeOffset EndedAt)? GetRecentRecordPeriod(
        Guid projectId,
        bool allowPersistentReload)
    {
        if (_recentRecordPeriodByProjectId.TryGetValue(projectId, out (DateTimeOffset StartedAt, DateTimeOffset EndedAt)? recentRecordPeriod))
        {
            return recentRecordPeriod;
        }

        if (!allowPersistentReload)
        {
            return null;
        }

        recentRecordPeriod = _store.LoadRecentRecordPeriod(projectId);
        _recentRecordPeriodByProjectId[projectId] = recentRecordPeriod;
        return recentRecordPeriod;
    }

    private string GetNextDefaultProjectName()
    {
        int index = _engine.Projects.Count + 1;

        while (_engine.Projects.Any(project =>
            string.Equals(project.Name, $"작업 {index}", StringComparison.OrdinalIgnoreCase)))
        {
            index++;
        }

        return $"작업 {index}";
    }

    private void _refreshAll(DateTimeOffset observedAt, string message)
    {
        _refreshUiAfterCommand(observedAt, message, false, false);
    }

    private void RefreshProjectFiltersAndVisibleRecord(DateTimeOffset observedAt, string message)
    {
        _refreshUiAfterCommand(observedAt, message, true, false);
    }

    private void RefreshVisibleRecord(DateTimeOffset observedAt, string message)
    {
        _refreshUiAfterCommand(observedAt, message, false, true);
    }
}
