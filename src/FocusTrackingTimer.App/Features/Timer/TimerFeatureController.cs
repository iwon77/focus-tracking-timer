using System.Windows;
using System.Windows.Media;
using FocusTrackingTimer.App.Infrastructure;
using FocusTrackingTimer.App.ViewModels;
using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App.Features.Timer;

internal sealed class TimerFeatureController
{
    private readonly Window _owner;
    private readonly ProjectTimerEngine _engine;
    private readonly TimerViewModel _viewModel;
    private readonly int _currentProcessId;
    private readonly Action _persistState;
    private readonly Action<DateTimeOffset, string> _refreshAll;
    private readonly Brush _startButtonBackground;
    private readonly Brush _stopButtonBackground;
    private readonly Brush _disabledButtonBackground;
    private readonly Brush _startButtonForeground;
    private readonly Brush _defaultButtonForeground;

    public TimerFeatureController(
        Window owner,
        ProjectTimerEngine engine,
        TimerViewModel viewModel,
        int currentProcessId,
        Action persistState,
        Action<DateTimeOffset, string> refreshAll,
        Brush startButtonBackground,
        Brush stopButtonBackground,
        Brush disabledButtonBackground,
        Brush startButtonForeground,
        Brush defaultButtonForeground)
    {
        _owner = owner;
        _engine = engine;
        _viewModel = viewModel;
        _currentProcessId = currentProcessId;
        _persistState = persistState;
        _refreshAll = refreshAll;
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
            _viewModel.TimerStatusText = "프로젝트를 추가하지 못했습니다.";
            return;
        }

        SelectedProject = project;
        _persistState();
        _refreshAll(DateTimeOffset.Now, $"'{project.Name}' 프로젝트를 추가했습니다.");
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

        SelectedProject = _engine.Projects.FirstOrDefault(item => item.Id == project.Id);
        _persistState();
        _refreshAll(DateTimeOffset.Now, isPinned ? "프로젝트를 고정했습니다." : "프로젝트 고정을 해제했습니다.");
    }

    public void EditSelectedProjectMemo()
    {
        if (SelectedProject is null)
        {
            return;
        }

        ProjectMemoDialog dialog = new(SelectedProject.Name, SelectedProject.CreatedAt, SelectedProject.Memo)
        {
            Owner = _owner
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _engine.UpdateProjectMemo(SelectedProject.Id, dialog.MemoText);
        SelectedProject = _engine.Projects.FirstOrDefault(item => item.Id == SelectedProject.Id);
        _persistState();
        _refreshAll(DateTimeOffset.Now, "프로젝트 메모를 저장했습니다.");
    }

    public void DeleteSelectedProject()
    {
        if (SelectedProject is null)
        {
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            _owner,
            $"'{SelectedProject.Name}' 프로젝트를 삭제하시겠습니까?",
            "프로젝트 삭제",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        Guid projectId = SelectedProject.Id;
        if (!_engine.TryRemoveProject(projectId))
        {
            _viewModel.TimerStatusText = "실행 중인 프로젝트는 삭제할 수 없습니다.";
            return;
        }

        SelectedProject = _engine.Projects.FirstOrDefault();
        _persistState();
        _refreshAll(DateTimeOffset.Now, "프로젝트를 삭제했습니다.");
    }

    public void EditSelectedProject()
    {
        if (SelectedProject is null)
        {
            return;
        }

        Guid projectId = SelectedProject.Id;
        NameEditDialog dialog = new("프로젝트 이름 수정", SelectedProject.Name)
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
            MessageBox.Show(_owner, "프로젝트 이름은 비워둘 수 없습니다.", "프로젝트 이름 수정");
            return;
        }

        if (newName.Length > ProjectDefinition.MaxNameLength)
        {
            MessageBox.Show(_owner, $"프로젝트 이름은 {ProjectDefinition.MaxNameLength}자 이하로 입력해주세요.", "프로젝트 이름 수정");
            return;
        }

        if (!_engine.TryRenameProject(projectId, newName))
        {
            MessageBox.Show(_owner, "이미 사용 중인 프로젝트 이름입니다.", "프로젝트 이름 수정");
            return;
        }

        SelectedProject = _engine.Projects.FirstOrDefault(item => item.Id == projectId);
        _persistState();
        _refreshAll(DateTimeOffset.Now, "프로젝트 이름을 변경했습니다.");
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

        SelectedProject = project;
        RefreshSelectedProjectArea(DateTimeOffset.Now, "선택한 프로젝트를 표시합니다.");
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

        SelectedProject = project;
        _viewModel.SelectedProjectRow = _viewModel.ProjectRows.FirstOrDefault(item => item.ProjectId == project.Id);
        RefreshSelectedProjectArea(DateTimeOffset.Now, "실행 중인 프로젝트를 표시합니다.");
    }

    public void ToggleTimerOrPause()
    {
        if (SelectedProject is null)
        {
            _viewModel.SelectedProjectTitle = "먼저 프로젝트를 선택해주세요.";
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;

        if (!_engine.IsRunning)
        {
            _engine.StartProject(SelectedProject.Id, now);
            _refreshAll(now, $"'{SelectedProject.Name}' 타이머를 시작했습니다. 등록 프로그램 포커스 중에만 시간이 흐릅니다.");
            return;
        }

        if (_engine.ActiveProjectId == SelectedProject.Id)
        {
            if (_engine.IsPaused)
            {
                _engine.ResumeProject(now);
                _refreshAll(now, $"'{SelectedProject.Name}' 타이머를 재개했습니다.");
                return;
            }

            _engine.PauseProject(now);
            _refreshAll(now, $"'{SelectedProject.Name}' 타이머를 일시정지했습니다.");
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
        _persistState();
        _refreshAll(now, $"'{record.ProjectName}' 타이머를 종료했습니다.");
    }

    public void OpenProgramManager()
    {
        if (SelectedProject is null)
        {
            _viewModel.TimerStatusText = "프로그램을 추가하려면 먼저 프로젝트를 선택해주세요.";
            return;
        }

        ProgramManagerWindow manager = new(_engine, SelectedProject, _currentProcessId, _persistState)
        {
            Owner = _owner
        };
        _ = manager.ShowDialog();
        _persistState();
        _refreshAll(DateTimeOffset.Now, "등록 프로그램 변경사항을 반영했습니다.");
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
        _persistState();

        if (_engine.IsRunning && _engine.ActiveProjectId == SelectedProject.Id)
        {
            FocusObservation observation = ForegroundWindowTracker.GetCurrentFocusedApplication(_currentProcessId);
            _engine.ObserveFocusedProgram(observation.Application?.ProcessName, now);
        }

        _refreshAll(now, "프로그램 표시 이름을 변경했습니다.");
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
        _persistState();
        _refreshAll(now, "등록 프로그램을 삭제했습니다.");
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

        _persistState();
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

    public string RefreshFocusTracking(DateTimeOffset observedAt)
    {
        if (!_engine.IsRunning)
        {
            return "타이머 대기 중입니다. 시작 후 등록 프로그램이 포커스된 시간만 기록합니다.";
        }

        if (_engine.IsPaused)
        {
            return "타이머가 일시정지 중입니다.";
        }

        FocusObservation observation = ForegroundWindowTracker.GetCurrentFocusedApplication(_currentProcessId);
        IReadOnlyDictionary<string, ProcessRunState> processStates = RunningProcessCatalog.GetProcessRunStates(_currentProcessId);
        string? focusableProcessName = TimerProgramFocusStatus.GetFocusableObservedProcessName(observation.Application, processStates);
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
        Guid? selectedProjectId = SelectedProject?.Id;
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

        _viewModel.ProjectRows.Clear();
        ProjectSortMode sortMode = _viewModel.SelectedProjectSortOption?.Mode ?? ProjectSortMode.Created;
        foreach (ProjectDefinition project in TimerProjectDisplayService.GetSortedProjects(_engine.Projects, sortMode))
        {
            _viewModel.ProjectRows.Add(new ProjectSidebarRow(
                project.Id,
                project.Name,
                project.IsPinned));
        }

        if (selectedProjectId.HasValue)
        {
            _viewModel.SelectedProjectRow = _viewModel.ProjectRows.FirstOrDefault(item => item.ProjectId == selectedProjectId.Value);
            SelectedProject = _engine.Projects.FirstOrDefault(item => item.Id == selectedProjectId.Value);
        }
        else if (_engine.Projects.Count > 0)
        {
            SelectedProject = _engine.Projects[0];
            _viewModel.SelectedProjectRow = _viewModel.ProjectRows.FirstOrDefault(item => item.ProjectId == SelectedProject.Id);
        }
        else
        {
            _viewModel.SelectedProjectRow = null;
            SelectedProject = null;
        }
    }

    public void RefreshSelectedProjectArea(DateTimeOffset observedAt, string message)
    {
        _viewModel.IsProjectEditEnabled = SelectedProject is not null;
        _viewModel.IsProjectDeleteEnabled = SelectedProject is not null && _engine.ActiveProjectId != SelectedProject.Id;
        _viewModel.IsProjectMemoEnabled = SelectedProject is not null;

        if (SelectedProject is null)
        {
            _viewModel.SelectedProjectTitle = "프로젝트를 추가해보세요";
            _viewModel.ActiveSessionPeriodText = "최근 작업 일시: -";
            _viewModel.TimerStatusText = message;
            _viewModel.FocusStatusText = string.Empty;
            _viewModel.ActiveProjectWallClockText = "00:00:00";
            _viewModel.ActiveProjectElapsedText = "00:00:00";
            _viewModel.SelectedProjectTodayText = "00:00:00";
            _viewModel.IsTimerActionEnabled = false;
            _viewModel.IsTimerStopEnabled = false;
            _viewModel.TimerActionButtonText = "시작";
            _viewModel.TimerActionButtonBackground = _disabledButtonBackground;
            _viewModel.TimerActionButtonForeground = _defaultButtonForeground;
            _viewModel.RegisteredProgramRows.Clear();
            return;
        }

        bool isActiveProject = _engine.ActiveProjectId == SelectedProject.Id;
        ProgramSortMode sortMode = _viewModel.SelectedProgramSortOption?.Mode ?? ProgramSortMode.MostUsed;
        Dictionary<string, string> initialDisplayNameByProcessName = _engine
            .GetRegisteredProgramInfos(SelectedProject.Id)
            .ToDictionary(
                info => info.Program.ProcessName,
                info => info.InitialDisplayName,
                StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, ProcessRunState> processStates = RunningProcessCatalog.GetProcessRunStates(_currentProcessId);
        Dictionary<string, RegisteredProgramInfo> registrationByProcessName = _engine
            .GetRegisteredProgramInfos(SelectedProject.Id)
            .ToDictionary(
                info => info.Program.ProcessName,
                StringComparer.OrdinalIgnoreCase);

        _viewModel.RegisteredProgramRows.Clear();
        IReadOnlyList<ProgramFocusSummary> programSummaries = _engine.GetCurrentSessionProgramSummaries(
            SelectedProject.Id,
            observedAt,
            sortMode);
        bool hasPinnedPrograms = programSummaries.Any(summary =>
            registrationByProcessName.GetValueOrDefault(summary.Program.ProcessName)?.IsPinned == true);
        bool showedPinnedDivider = false;

        foreach (ProgramFocusSummary summary in programSummaries)
        {
            (string statusBrush, string statusText) = TimerProgramFocusStatus.GetRuntimeStatus(summary.Program.ProcessName, processStates);
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

        bool anotherProjectIsRunning = _engine.IsRunning && !isActiveProject;

        _viewModel.SelectedProjectTitle = SelectedProject.Name;
        _viewModel.ActiveSessionPeriodText = TimerProjectDisplayService.GetProjectPeriodText(_engine, SelectedProject, isActiveProject);
        _viewModel.ActiveProjectElapsedText = isActiveProject
            ? AppTimeFormatter.FormatDuration(_engine.GetCurrentRunDuration(SelectedProject.Id, observedAt))
            : "00:00:00";
        _viewModel.ActiveProjectWallClockText = isActiveProject
            ? AppTimeFormatter.FormatDuration(_engine.GetCurrentWallClockDuration(SelectedProject.Id, observedAt))
            : "00:00:00";
        _viewModel.SelectedProjectTodayText = AppTimeFormatter.FormatDuration(_engine.GetTodayDuration(
            DateOnly.FromDateTime(observedAt.LocalDateTime.Date),
            observedAt,
            SelectedProject.Id));

        _viewModel.FocusStatusText = isActiveProject
            ? (_engine.IsPaused
                ? "타이머가 일시정지 중입니다."
                : _engine.ActiveFocusedProgramName is null
                ? "등록 프로그램이 포커스될 때까지 프로젝트 타이머는 멈춰 있습니다."
                : $"현재 포커스 프로그램: {_engine.ActiveFocusedProgramName}")
            : string.Empty;

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

    private string GetNextDefaultProjectName()
    {
        int index = _engine.Projects.Count + 1;

        while (_engine.Projects.Any(project =>
            string.Equals(project.Name, $"프로젝트 {index}", StringComparison.OrdinalIgnoreCase)))
        {
            index++;
        }

        return $"프로젝트 {index}";
    }

}
