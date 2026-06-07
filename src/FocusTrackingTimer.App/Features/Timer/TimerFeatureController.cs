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

    public void ToggleTimer()
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
            ProjectTimerRecord record = _engine.StopProject(now);
            _persistState();
            _refreshAll(now, $"'{record.ProjectName}' 타이머를 종료했습니다.");
        }
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

    public string RefreshFocusTracking(DateTimeOffset observedAt)
    {
        if (!_engine.IsRunning)
        {
            return "타이머 대기 중입니다. 시작 후 등록 프로그램이 포커스된 시간만 기록합니다.";
        }

        FocusObservation observation = ForegroundWindowTracker.GetCurrentFocusedApplication(_currentProcessId);
        IReadOnlyDictionary<string, ProcessRunState> processStates = RunningProcessCatalog.GetProcessRunStates(_currentProcessId);
        string? focusableProcessName = GetFocusableObservedProcessName(observation.Application, processStates);
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

        _viewModel.ProjectRows.Clear();
        foreach (ProjectDefinition project in _engine.Projects.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            bool isActiveProject = _engine.ActiveProjectId == project.Id;
            string timerText = isActiveProject ? AppTimeFormatter.FormatDuration(_engine.GetCurrentRunDuration(project.Id, observedAt)) : string.Empty;
            string statusText = isActiveProject ? $"{timerText} 실행 중" : string.Empty;

            _viewModel.ProjectRows.Add(new ProjectSidebarRow(
                project.Id,
                project.Name,
                string.Empty,
                statusText));
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

        if (SelectedProject is null)
        {
            _viewModel.SelectedProjectTitle = "프로젝트를 추가해보세요";
            _viewModel.ActiveSessionPeriodText = string.Empty;
            _viewModel.TimerStatusText = message;
            _viewModel.FocusStatusText = string.Empty;
            _viewModel.ActiveProjectWallClockText = "00:00:00";
            _viewModel.ActiveProjectElapsedText = "00:00:00";
            _viewModel.SelectedProjectTodayText = "00:00:00";
            _viewModel.IsTimerActionEnabled = false;
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

        _viewModel.RegisteredProgramRows.Clear();
        foreach (ProgramFocusSummary summary in _engine.GetCurrentSessionProgramSummaries(SelectedProject.Id, observedAt, sortMode))
        {
            (string statusBrush, string statusText) = GetProgramRuntimeStatus(summary.Program.ProcessName, processStates);
            _viewModel.RegisteredProgramRows.Add(new RegisteredProgramRow(
                summary.Program.DisplayName,
                summary.Program.ProcessName,
                isActiveProject ? AppTimeFormatter.FormatDuration(summary.FocusDuration) : "00:00:00",
                InitialDisplayName: initialDisplayNameByProcessName.GetValueOrDefault(
                    summary.Program.ProcessName,
                    summary.Program.DisplayName),
                StatusBrush: statusBrush,
                StatusText: statusText));
        }

        bool anotherProjectIsRunning = _engine.IsRunning && !isActiveProject;

        _viewModel.SelectedProjectTitle = SelectedProject.Name;
        _viewModel.ActiveSessionPeriodText = _engine.ActiveStartedAt.HasValue && isActiveProject
            ? $"시작 {AppTimeFormatter.FormatDateTime(_engine.ActiveStartedAt.Value)} / 종료 대기 중"
            : string.Empty;
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
            ? (_engine.ActiveFocusedProgramName is null
                ? "등록 프로그램이 포커스될 때까지 프로젝트 타이머는 멈춰 있습니다."
                : $"현재 포커스 프로그램: {_engine.ActiveFocusedProgramName}")
            : string.Empty;

        _viewModel.TimerStatusText = message;
        _viewModel.IsTimerActionEnabled = !anotherProjectIsRunning;
        _viewModel.TimerActionButtonText = !_engine.IsRunning
            ? "시작"
            : isActiveProject
                ? "종료"
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

    private static (string Brush, string Text) GetProgramRuntimeStatus(
        string processName,
        IReadOnlyDictionary<string, ProcessRunState> processStates)
    {
        if (!processStates.TryGetValue(processName, out ProcessRunState? state))
        {
            return ("#A7A7A0", "등록됨 / 현재 실행 중 아님");
        }

        return state.HasFocusableWindow
            ? ("#2EAD62", "등록됨 / 실행 중 / 포커스 기록 가능")
            : ("#D14B4B", "등록됨 / 실행 중 / 포커스 기록 불가");
    }

    private static string? GetFocusableObservedProcessName(
        TrackedApplication? observedApplication,
        IReadOnlyDictionary<string, ProcessRunState> processStates)
    {
        if (observedApplication is null)
        {
            return null;
        }

        return processStates.TryGetValue(observedApplication.ProcessName, out ProcessRunState? state) &&
            state.HasFocusableWindow
                ? observedApplication.ProcessName
                : null;
    }
}
