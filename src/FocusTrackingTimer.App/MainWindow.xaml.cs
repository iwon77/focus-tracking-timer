using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private static readonly Brush SelectedTabBackground = new SolidColorBrush(Color.FromRgb(31, 31, 31));
    private static readonly Brush SelectedTabForeground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
    private static readonly Brush UnselectedTabBackground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
    private static readonly Brush UnselectedTabForeground = new SolidColorBrush(Color.FromRgb(24, 24, 24));
    private static readonly Brush StartButtonBackground = new SolidColorBrush(Color.FromRgb(31, 31, 31));
    private static readonly Brush StopButtonBackground = new SolidColorBrush(Color.FromRgb(245, 245, 242));
    private static readonly Brush DisabledButtonBackground = new SolidColorBrush(Color.FromRgb(225, 225, 225));
    private static readonly Brush StartButtonForeground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
    private static readonly Brush DefaultButtonForeground = new SolidColorBrush(Color.FromRgb(24, 24, 24));
    private static readonly Brush RecordSelectedButtonBackground = new SolidColorBrush(Color.FromRgb(237, 237, 234));
    private static readonly Brush RecordUnselectedButtonBackground = new SolidColorBrush(Color.FromRgb(255, 255, 255));

    private readonly ProjectTimerEngine _engine = new();
    private readonly DispatcherTimer _uiTimer;
    private readonly int _currentProcessId = Environment.ProcessId;

    private PrototypeTab _selectedTab = PrototypeTab.Project;
    private RecordSubView _selectedRecordSubView = RecordSubView.Calendar;
    private ProjectDefinition? _selectedProject;
    private ProjectSidebarRow? _selectedProjectRow;
    private RecordFilterOption? _selectedRecordFilter;
    private ProgramSortOption? _selectedProgramSortOption;
    private string _selectedProjectTitle = "프로젝트를 추가해보세요";
    private string _activeSessionPeriodText = "작업 시작/종료 시간이 여기에 표시됩니다.";
    private string _timerStatusText = "시작 버튼을 누르면 등록 프로그램 포커스 시간만 기록합니다.";
    private string _focusStatusText = "등록 프로그램 포커스 상태가 여기에 표시됩니다.";
    private string _activeProjectWallClockText = "00:00:00";
    private string _activeProjectElapsedText = "00:00:00";
    private string _selectedProjectTodayText = "00:00:00";
    private string _recordHeadlineText = "오늘은 아직 작업 기록이 없습니다.";
    private string _todayWorkedText = "00:00:00";
    private string _selectedRecordFilterLabel = "<모든 프로젝트>";
    private bool _isTimerActionEnabled;
    private bool _isProjectEditEnabled;
    private bool _isProjectDeleteEnabled;
    private string _timerActionButtonText = "시작";
    private Brush _timerActionButtonBackground = StartButtonBackground;
    private Brush _timerActionButtonForeground = StartButtonForeground;
    private Brush _projectTabBackground = SelectedTabBackground;
    private Brush _projectTabForeground = SelectedTabForeground;
    private Brush _recordTabBackground = UnselectedTabBackground;
    private Brush _recordTabForeground = UnselectedTabForeground;
    private Brush _calendarButtonBackground = RecordSelectedButtonBackground;
    private Brush _recentButtonBackground = RecordUnselectedButtonBackground;
    private Visibility _projectViewVisibility = Visibility.Visible;
    private Visibility _recordViewVisibility = Visibility.Collapsed;
    private Visibility _calendarRecordVisibility = Visibility.Visible;
    private Visibility _recentRecordVisibility = Visibility.Collapsed;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        ProgramSortOptions.Add(new ProgramSortOption(ProgramSortMode.MostUsed, "많이 사용한 순"));
        ProgramSortOptions.Add(new ProgramSortOption(ProgramSortMode.Registered, "등록 순서"));
        ProgramSortOptions.Add(new ProgramSortOption(ProgramSortMode.Manual, "사용자 지정"));
        SelectedProgramSortOption = ProgramSortOptions[0];

        _uiTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uiTimer.Tick += UiTimer_Tick;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProjectSidebarRow> ProjectRows { get; } = [];

    public ObservableCollection<RegisteredProgramRow> RegisteredProgramRows { get; } = [];

    public ObservableCollection<RecordFilterOption> RecordFilterOptions { get; } = [];

    public ObservableCollection<ProgramSortOption> ProgramSortOptions { get; } = [];

    public ObservableCollection<CalendarDayRow> CalendarRows { get; } = [];

    public ObservableCollection<RecentRecordRow> RecentRecordRows { get; } = [];

    public string SelectedProjectTitle
    {
        get => _selectedProjectTitle;
        private set => SetProperty(ref _selectedProjectTitle, value);
    }

    public string ActiveSessionPeriodText
    {
        get => _activeSessionPeriodText;
        private set => SetProperty(ref _activeSessionPeriodText, value);
    }

    public string TimerStatusText
    {
        get => _timerStatusText;
        private set => SetProperty(ref _timerStatusText, value);
    }

    public string FocusStatusText
    {
        get => _focusStatusText;
        private set => SetProperty(ref _focusStatusText, value);
    }

    public string ActiveProjectElapsedText
    {
        get => _activeProjectElapsedText;
        private set => SetProperty(ref _activeProjectElapsedText, value);
    }

    public string ActiveProjectWallClockText
    {
        get => _activeProjectWallClockText;
        private set => SetProperty(ref _activeProjectWallClockText, value);
    }

    public string SelectedProjectTodayText
    {
        get => _selectedProjectTodayText;
        private set => SetProperty(ref _selectedProjectTodayText, value);
    }

    public string RecordHeadlineText
    {
        get => _recordHeadlineText;
        private set => SetProperty(ref _recordHeadlineText, value);
    }

    public string TodayWorkedText
    {
        get => _todayWorkedText;
        private set => SetProperty(ref _todayWorkedText, value);
    }

    public string SelectedRecordFilterLabel
    {
        get => _selectedRecordFilterLabel;
        private set => SetProperty(ref _selectedRecordFilterLabel, value);
    }

    public bool IsTimerActionEnabled
    {
        get => _isTimerActionEnabled;
        private set => SetProperty(ref _isTimerActionEnabled, value);
    }

    public bool IsProjectEditEnabled
    {
        get => _isProjectEditEnabled;
        private set => SetProperty(ref _isProjectEditEnabled, value);
    }

    public bool IsProjectDeleteEnabled
    {
        get => _isProjectDeleteEnabled;
        private set => SetProperty(ref _isProjectDeleteEnabled, value);
    }

    public string TimerActionButtonText
    {
        get => _timerActionButtonText;
        private set => SetProperty(ref _timerActionButtonText, value);
    }

    public Brush TimerActionButtonBackground
    {
        get => _timerActionButtonBackground;
        private set => SetProperty(ref _timerActionButtonBackground, value);
    }

    public Brush TimerActionButtonForeground
    {
        get => _timerActionButtonForeground;
        private set => SetProperty(ref _timerActionButtonForeground, value);
    }

    public Brush ProjectTabBackground
    {
        get => _projectTabBackground;
        private set => SetProperty(ref _projectTabBackground, value);
    }

    public Brush ProjectTabForeground
    {
        get => _projectTabForeground;
        private set => SetProperty(ref _projectTabForeground, value);
    }

    public Brush RecordTabBackground
    {
        get => _recordTabBackground;
        private set => SetProperty(ref _recordTabBackground, value);
    }

    public Brush RecordTabForeground
    {
        get => _recordTabForeground;
        private set => SetProperty(ref _recordTabForeground, value);
    }

    public Brush CalendarButtonBackground
    {
        get => _calendarButtonBackground;
        private set => SetProperty(ref _calendarButtonBackground, value);
    }

    public Brush RecentButtonBackground
    {
        get => _recentButtonBackground;
        private set => SetProperty(ref _recentButtonBackground, value);
    }

    public Visibility ProjectViewVisibility
    {
        get => _projectViewVisibility;
        private set => SetProperty(ref _projectViewVisibility, value);
    }

    public Visibility RecordViewVisibility
    {
        get => _recordViewVisibility;
        private set => SetProperty(ref _recordViewVisibility, value);
    }

    public Visibility CalendarRecordVisibility
    {
        get => _calendarRecordVisibility;
        private set => SetProperty(ref _calendarRecordVisibility, value);
    }

    public Visibility RecentRecordVisibility
    {
        get => _recentRecordVisibility;
        private set => SetProperty(ref _recentRecordVisibility, value);
    }

    public ProjectSidebarRow? SelectedProjectRow
    {
        get => _selectedProjectRow;
        set => SetProperty(ref _selectedProjectRow, value);
    }

    public RecordFilterOption? SelectedRecordFilter
    {
        get => _selectedRecordFilter;
        set => SetProperty(ref _selectedRecordFilter, value);
    }

    public ProgramSortOption? SelectedProgramSortOption
    {
        get => _selectedProgramSortOption;
        set => SetProperty(ref _selectedProgramSortOption, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshAll(DateTimeOffset.Now, "프로젝트를 추가하고 등록 프로그램을 관리해보세요.");
        _uiTimer.Start();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _uiTimer.Stop();

        if (_engine.IsRunning)
        {
            _engine.StopProject(DateTimeOffset.Now);
        }
    }

    private void UiTimer_Tick(object? sender, EventArgs e)
    {
        DateTimeOffset observedAt = DateTimeOffset.Now;
        string focusMessage = RefreshFocusTracking(observedAt);
        RefreshAll(observedAt, focusMessage);
    }

    private void ProjectTabButton_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedTab(PrototypeTab.Project);
    }

    private void RecordTabButton_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedTab(PrototypeTab.Record);
    }

    private void CalendarRecordButton_Click(object sender, RoutedEventArgs e)
    {
        _selectedRecordSubView = RecordSubView.Calendar;
        RefreshRecordViewState();
    }

    private void RecentRecordButton_Click(object sender, RoutedEventArgs e)
    {
        _selectedRecordSubView = RecordSubView.Recent;
        RefreshRecordViewState();
    }

    private void AddProjectButton_Click(object sender, RoutedEventArgs e)
    {
        string projectName = GetNextDefaultProjectName();

        if (!_engine.TryAddProject(projectName, out ProjectDefinition project))
        {
            TimerStatusText = "프로젝트를 추가하지 못했습니다.";
            return;
        }

        _selectedProject = project;
        RefreshAll(DateTimeOffset.Now, $"'{project.Name}' 프로젝트를 추가했습니다.");
    }

    private void DeleteProjectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            this,
            $"'{_selectedProject.Name}' 프로젝트를 삭제하시겠습니까?",
            "프로젝트 삭제",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        Guid projectId = _selectedProject.Id;
        if (!_engine.TryRemoveProject(projectId))
        {
            TimerStatusText = "실행 중인 프로젝트는 삭제할 수 없습니다.";
            return;
        }

        _selectedProject = _engine.Projects.FirstOrDefault();
        RefreshAll(DateTimeOffset.Now, "프로젝트를 삭제했습니다.");
    }

    private void EditSelectedProjectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            return;
        }

        Guid projectId = _selectedProject.Id;
        NameEditDialog dialog = new("프로젝트 이름 수정", _selectedProject.Name)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string newName = dialog.NameValue.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            MessageBox.Show(this, "프로젝트 이름은 비워둘 수 없습니다.", "프로젝트 이름 수정");
            return;
        }

        if (!_engine.TryRenameProject(projectId, newName))
        {
            MessageBox.Show(this, "이미 사용 중인 프로젝트 이름입니다.", "프로젝트 이름 수정");
            return;
        }

        _selectedProject = _engine.Projects.FirstOrDefault(item => item.Id == projectId);
        RefreshAll(DateTimeOffset.Now, "프로젝트 이름을 변경했습니다.");
    }

    private void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedProjectRow is null)
        {
            return;
        }

        ProjectDefinition? project = _engine.Projects.FirstOrDefault(item => item.Id == SelectedProjectRow.ProjectId);
        if (project is null)
        {
            return;
        }

        _selectedProject = project;
        RefreshSelectedProjectArea(DateTimeOffset.Now, "선택한 프로젝트를 표시합니다.");
    }

    private void TimerActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            SelectedProjectTitle = "먼저 프로젝트를 선택해주세요.";
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;

        if (!_engine.IsRunning)
        {
            _engine.StartProject(_selectedProject.Id, now);
            RefreshAll(now, $"'{_selectedProject.Name}' 타이머를 시작했습니다. 등록 프로그램 포커스 중에만 시간이 흐릅니다.");
            return;
        }

        if (_engine.ActiveProjectId == _selectedProject.Id)
        {
            ProjectTimerRecord record = _engine.StopProject(now);
            RefreshAll(now, $"'{record.ProjectName}' 타이머를 종료했습니다.");
        }
    }

    private void OpenProgramManagerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            TimerStatusText = "프로그램을 추가하려면 먼저 프로젝트를 선택해주세요.";
            return;
        }

        ProgramManagerWindow manager = new(_engine, _selectedProject, _currentProcessId)
        {
            Owner = this
        };
        _ = manager.ShowDialog();
        RefreshAll(DateTimeOffset.Now, "등록 프로그램 변경사항을 반영했습니다.");
    }

    private void EditProgramButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject is null ||
            sender is not FrameworkElement { DataContext: RegisteredProgramRow row })
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
            Owner = this
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

        _ = _engine.TryUpdateProgram(_selectedProject.Id, row.ProcessName, new TrackedApplication(row.ProcessName, displayName));

        if (_engine.IsRunning && _engine.ActiveProjectId == _selectedProject.Id)
        {
            FocusObservation observation = ForegroundWindowTracker.GetCurrentFocusedApplication(_currentProcessId);
            _engine.ObserveFocusedProgram(observation.Application?.ProcessName, now);
        }

        RefreshAll(now, "프로그램 표시 이름을 변경했습니다.");
    }

    private void DeleteProgramButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject is null ||
            sender is not FrameworkElement { DataContext: RegisteredProgramRow row })
        {
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            this,
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

        _ = _engine.TryRemoveProgram(_selectedProject.Id, row.ProcessName);
        RefreshAll(now, "등록 프로그램을 삭제했습니다.");
    }

    private void ProgramSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshSelectedProjectArea(DateTimeOffset.Now, TimerStatusText);
    }

    private void RecordFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshRecordArea(DateTimeOffset.Now);
    }

    private void SetSelectedTab(PrototypeTab tab)
    {
        _selectedTab = tab;

        bool isProject = tab == PrototypeTab.Project;
        ProjectViewVisibility = isProject ? Visibility.Visible : Visibility.Collapsed;
        RecordViewVisibility = isProject ? Visibility.Collapsed : Visibility.Visible;
        ProjectTabBackground = isProject ? SelectedTabBackground : UnselectedTabBackground;
        ProjectTabForeground = isProject ? SelectedTabForeground : UnselectedTabForeground;
        RecordTabBackground = isProject ? UnselectedTabBackground : SelectedTabBackground;
        RecordTabForeground = isProject ? UnselectedTabForeground : SelectedTabForeground;

        if (!isProject)
        {
            RefreshRecordViewState();
            RefreshRecordArea(DateTimeOffset.Now);
        }
    }

    private void RefreshRecordViewState()
    {
        bool isCalendar = _selectedRecordSubView == RecordSubView.Calendar;
        CalendarRecordVisibility = isCalendar ? Visibility.Visible : Visibility.Collapsed;
        RecentRecordVisibility = isCalendar ? Visibility.Collapsed : Visibility.Visible;
        CalendarButtonBackground = isCalendar ? RecordSelectedButtonBackground : RecordUnselectedButtonBackground;
        RecentButtonBackground = isCalendar ? RecordUnselectedButtonBackground : RecordSelectedButtonBackground;
    }

    private string RefreshFocusTracking(DateTimeOffset observedAt)
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

    private void RefreshAll(DateTimeOffset observedAt, string message)
    {
        RefreshProjectSidebar(observedAt);
        RefreshSelectedProjectArea(observedAt, message);
        RefreshRecordFilters();
        RefreshRecordArea(observedAt);
    }

    private void RefreshProjectSidebar(DateTimeOffset observedAt)
    {
        Guid? selectedProjectId = _selectedProject?.Id;

        ProjectRows.Clear();
        foreach (ProjectDefinition project in _engine.Projects.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            bool isActiveProject = _engine.ActiveProjectId == project.Id;
            string timerText = isActiveProject ? FormatDuration(_engine.GetCurrentRunDuration(project.Id, observedAt)) : string.Empty;
            string statusText = isActiveProject ? $"{timerText} 실행 중" : string.Empty;

            ProjectRows.Add(new ProjectSidebarRow(
                project.Id,
                project.Name,
                string.Empty,
                statusText));
        }

        if (selectedProjectId.HasValue)
        {
            SelectedProjectRow = ProjectRows.FirstOrDefault(item => item.ProjectId == selectedProjectId.Value);
            _selectedProject = _engine.Projects.FirstOrDefault(item => item.Id == selectedProjectId.Value);
        }
        else if (_engine.Projects.Count > 0)
        {
            _selectedProject = _engine.Projects[0];
            SelectedProjectRow = ProjectRows.FirstOrDefault(item => item.ProjectId == _selectedProject.Id);
        }
        else
        {
            SelectedProjectRow = null;
            _selectedProject = null;
        }
    }

    private void RefreshSelectedProjectArea(DateTimeOffset observedAt, string message)
    {
        IsProjectEditEnabled = _selectedProject is not null;
        IsProjectDeleteEnabled = _selectedProject is not null && _engine.ActiveProjectId != _selectedProject.Id;

        if (_selectedProject is null)
        {
            SelectedProjectTitle = "프로젝트를 추가해보세요";
            ActiveSessionPeriodText = string.Empty;
            TimerStatusText = message;
            FocusStatusText = string.Empty;
            ActiveProjectWallClockText = "00:00:00";
            ActiveProjectElapsedText = "00:00:00";
            SelectedProjectTodayText = "00:00:00";
            IsTimerActionEnabled = false;
            TimerActionButtonText = "시작";
            TimerActionButtonBackground = DisabledButtonBackground;
            TimerActionButtonForeground = DefaultButtonForeground;
            RegisteredProgramRows.Clear();
            return;
        }

        bool isActiveProject = _engine.ActiveProjectId == _selectedProject.Id;
        ProgramSortMode sortMode = SelectedProgramSortOption?.Mode ?? ProgramSortMode.MostUsed;
        Dictionary<string, string> initialDisplayNameByProcessName = _engine
            .GetRegisteredProgramInfos(_selectedProject.Id)
            .ToDictionary(
                info => info.Program.ProcessName,
                info => info.InitialDisplayName,
                StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, ProcessRunState> processStates = RunningProcessCatalog.GetProcessRunStates(_currentProcessId);

        RegisteredProgramRows.Clear();
        foreach (ProgramFocusSummary summary in _engine.GetCurrentSessionProgramSummaries(_selectedProject.Id, observedAt, sortMode))
        {
            (string statusBrush, string statusText) = GetProgramRuntimeStatus(summary.Program.ProcessName, processStates);
            RegisteredProgramRows.Add(new RegisteredProgramRow(
                summary.Program.DisplayName,
                summary.Program.ProcessName,
                isActiveProject ? FormatDuration(summary.FocusDuration) : "00:00:00",
                InitialDisplayName: initialDisplayNameByProcessName.GetValueOrDefault(
                    summary.Program.ProcessName,
                    summary.Program.DisplayName),
                StatusBrush: statusBrush,
                StatusText: statusText));
        }

        bool anotherProjectIsRunning = _engine.IsRunning && !isActiveProject;

        SelectedProjectTitle = _selectedProject.Name;
        ActiveSessionPeriodText = _engine.ActiveStartedAt.HasValue && isActiveProject
            ? $"시작 {FormatDateTime(_engine.ActiveStartedAt.Value)} / 종료 대기 중"
            : string.Empty;
        ActiveProjectElapsedText = isActiveProject
            ? FormatDuration(_engine.GetCurrentRunDuration(_selectedProject.Id, observedAt))
            : "00:00:00";
        ActiveProjectWallClockText = isActiveProject
            ? FormatDuration(_engine.GetCurrentWallClockDuration(_selectedProject.Id, observedAt))
            : "00:00:00";
        SelectedProjectTodayText = FormatDuration(_engine.GetTodayDuration(
            DateOnly.FromDateTime(observedAt.LocalDateTime.Date),
            observedAt,
            _selectedProject.Id));

        FocusStatusText = isActiveProject
            ? (_engine.ActiveFocusedProgramName is null
                ? "등록 프로그램이 포커스될 때까지 프로젝트 타이머는 멈춰 있습니다."
                : $"현재 포커스 프로그램: {_engine.ActiveFocusedProgramName}")
            : string.Empty;

        TimerStatusText = message;
        IsTimerActionEnabled = !anotherProjectIsRunning;
        TimerActionButtonText = !_engine.IsRunning
            ? "시작"
            : isActiveProject
                ? "종료"
                : "실행 중";
        TimerActionButtonBackground = !_engine.IsRunning
            ? StartButtonBackground
            : isActiveProject
                ? StopButtonBackground
                : DisabledButtonBackground;
        TimerActionButtonForeground = !_engine.IsRunning
            ? StartButtonForeground
            : DefaultButtonForeground;
    }

    private void RefreshRecordFilters()
    {
        Guid? selectedFilterProjectId = SelectedRecordFilter?.ProjectId;

        RecordFilterOptions.Clear();
        RecordFilterOptions.Add(new RecordFilterOption(null, "<모든 프로젝트>"));
        foreach (ProjectDefinition project in _engine.Projects.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            RecordFilterOptions.Add(new RecordFilterOption(project.Id, project.Name));
        }

        SelectedRecordFilter = RecordFilterOptions.FirstOrDefault(option => option.ProjectId == selectedFilterProjectId)
            ?? RecordFilterOptions[0];
    }

    private void RefreshRecordArea(DateTimeOffset observedAt)
    {
        Guid? projectFilter = SelectedRecordFilter?.ProjectId;
        SelectedRecordFilterLabel = SelectedRecordFilter?.Label ?? "<모든 프로젝트>";

        DateOnly today = DateOnly.FromDateTime(observedAt.LocalDateTime.Date);
        TodayWorkedText = FormatDuration(_engine.GetTodayDuration(today, observedAt, projectFilter));
        RecordHeadlineText = TodayWorkedText == "00:00:00"
            ? "오늘은 아직 작업 기록이 없습니다."
            : $"오늘은 {TodayWorkedText} 작업했습니다.";

        RefreshCalendar(today, observedAt, projectFilter);
        RefreshRecentRecords(projectFilter);
        RefreshRecordViewState();
    }

    private void RefreshCalendar(DateOnly today, DateTimeOffset observedAt, Guid? projectFilter)
    {
        CalendarRows.Clear();

        DateOnly firstDay = new(today.Year, today.Month, 1);
        DateOnly lastDay = firstDay.AddMonths(1).AddDays(-1);
        int leadingBlankCount = (int)firstDay.DayOfWeek;

        IReadOnlyList<DailyDurationSummary> summaries = _engine.GetDailyDurationSummaries(firstDay, lastDay, observedAt, projectFilter);
        Dictionary<DateOnly, DailyDurationSummary> summaryByDate = summaries.ToDictionary(summary => summary.Date);

        for (int index = 0; index < leadingBlankCount; index++)
        {
            CalendarRows.Add(new CalendarDayRow(string.Empty, string.Empty, false, false, true));
        }

        for (DateOnly date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            TimeSpan duration = summaryByDate.GetValueOrDefault(date)?.TotalDuration ?? TimeSpan.Zero;
            CalendarRows.Add(new CalendarDayRow(
                date.Day.ToString(CultureInfo.CurrentCulture),
                duration == TimeSpan.Zero ? string.Empty : FormatDurationShort(duration),
                duration > TimeSpan.Zero,
                date == today,
                false));
        }
    }

    private void RefreshRecentRecords(Guid? projectFilter)
    {
        RecentRecordRows.Clear();

        foreach (ProjectTimerRecord record in _engine.GetRecentRecords(12, projectFilter))
        {
            string programSummaryText = record.ProgramSummaries.Count == 0
                ? "등록 프로그램 포커스 기록 없음"
                : string.Join(" / ", record.ProgramSummaries.Select(summary =>
                    $"{summary.Program.DisplayName} {FormatDuration(summary.FocusDuration)}"));

            RecentRecordRows.Add(new RecentRecordRow(
                record.ProjectName,
                $"{FormatDateTime(record.StartedAt)} ~ {FormatDateTime(record.EndedAt)}",
                FormatDuration(record.TotalDuration),
                programSummaryText));
        }
    }

    private string GetLastRecordPeriodText(Guid projectId)
    {
        IReadOnlyList<ProjectTimerRecord> records = _engine.GetRecentRecords(1, projectId);
        ProjectTimerRecord? lastRecord = records.Count == 0 ? null : records[0];
        return lastRecord is null
            ? "최근 시작/종료 기록이 없습니다."
            : $"마지막 기록 {FormatDateTime(lastRecord.StartedAt)} / {FormatDateTime(lastRecord.EndedAt)}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        int hours = (int)duration.TotalHours;
        return $"{hours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
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

    private static string FormatDurationShort(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        int hours = (int)duration.TotalHours;
        return hours > 0 ? $"{hours}h {duration.Minutes}m" : $"{duration.Minutes}m";
    }

    private static string FormatDateTime(DateTimeOffset value)
    {
        return value.LocalDateTime.ToString("MM/dd HH:mm:ss", CultureInfo.CurrentCulture);
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

    private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return;
        }

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private enum PrototypeTab
    {
        Project,
        Record
    }

    private enum RecordSubView
    {
        Calendar,
        Recent
    }
}
