using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FocusTrackingTimer.Core.Persistence;
using FocusTrackingTimer.Core.Tracking;
using FocusTrackingTimer.App.ViewModels;

namespace FocusTrackingTimer.App;

public partial class MainWindow : Window
{
    private static readonly Brush StartButtonBackground = new SolidColorBrush(Color.FromRgb(31, 31, 31));
    private static readonly Brush StopButtonBackground = new SolidColorBrush(Color.FromRgb(245, 245, 242));
    private static readonly Brush DisabledButtonBackground = new SolidColorBrush(Color.FromRgb(225, 225, 225));
    private static readonly Brush StartButtonForeground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
    private static readonly Brush DefaultButtonForeground = new SolidColorBrush(Color.FromRgb(24, 24, 24));
    private static readonly Brush RecordSelectedButtonBackground = new SolidColorBrush(Color.FromRgb(237, 237, 234));
    private static readonly Brush RecordUnselectedButtonBackground = new SolidColorBrush(Color.FromRgb(255, 255, 255));

    private readonly ProjectTimerEngine _engine = new();
    private readonly SqliteProjectTimerStore _store = new(BuildStorePath());
    private readonly DispatcherTimer _uiTimer;
    private readonly int _currentProcessId = Environment.ProcessId;

    private DateOnly _displayedRecordMonth = new(DateTime.Now.Year, DateTime.Now.Month, 1);
    private DateOnly? _hoveredCalendarDate;
    private Dictionary<DateOnly, IReadOnlyList<string>> _calendarHoverLinesByDate = [];
    private ProjectDefinition? _selectedProject;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        Timer.ProgramSortOptions.Add(new ProgramSortOption(ProgramSortMode.MostUsed, "많이 사용한 순"));
        Timer.ProgramSortOptions.Add(new ProgramSortOption(ProgramSortMode.Registered, "등록 순서"));
        Timer.ProgramSortOptions.Add(new ProgramSortOption(ProgramSortMode.Manual, "사용자 지정"));
        Timer.SelectedProgramSortOption = Timer.ProgramSortOptions[0];
        DailyRecord.DisplayedRecordMonthText = FormatRecordMonth(_displayedRecordMonth);
        _uiTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uiTimer.Tick += UiTimer_Tick;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    public MainMenuViewModel Menu { get; } = new();

    public TimerViewModel Timer { get; } = new(StartButtonBackground, StartButtonForeground);

    public DailyRecordViewModel DailyRecord { get; } = new(RecordSelectedButtonBackground, RecordUnselectedButtonBackground);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        string startupMessage = "프로젝트를 추가하고 등록 프로그램을 관리해보세요.";

        try
        {
            _engine.ReplaceState(_store.LoadState());

            if (_engine.Projects.Count > 0 || _engine.CompletedRecords.Count > 0)
            {
                startupMessage = "저장된 프로젝트와 완료 기록을 불러왔습니다.";
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"저장된 데이터를 불러오지 못했습니다.{Environment.NewLine}{exception.Message}",
                "SQLite 로드 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        RefreshAll(DateTimeOffset.Now, startupMessage);
        _uiTimer.Start();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _uiTimer.Stop();

        if (_engine.IsRunning)
        {
            _engine.StopProject(DateTimeOffset.Now);
        }

        PersistState();
    }

    private void UiTimer_Tick(object? sender, EventArgs e)
    {
        DateTimeOffset observedAt = DateTimeOffset.Now;
        string focusMessage = RefreshFocusTracking(observedAt);
        RefreshAll(observedAt, focusMessage);
    }

    private void ProjectTabButton_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedTab(MainMenuTab.Timer);
    }

    private void CalendarTabButton_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedTab(MainMenuTab.DailyRecord);
    }

    private void WeeklyTabButton_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedTab(MainMenuTab.WeeklyRecord);
    }

    internal void CalendarRecordButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRecordViewState();
        RefreshRecordArea(DateTimeOffset.Now);
    }

    internal void RecentRecordButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRecordViewState();
        RefreshRecordArea(DateTimeOffset.Now);
    }

    internal void PreviousRecordYearButton_Click(object sender, RoutedEventArgs e)
    {
        MoveDisplayedRecordMonth(-12);
    }

    internal void PreviousRecordMonthButton_Click(object sender, RoutedEventArgs e)
    {
        MoveDisplayedRecordMonth(-1);
    }

    internal void NextRecordMonthButton_Click(object sender, RoutedEventArgs e)
    {
        MoveDisplayedRecordMonth(1);
    }

    internal void NextRecordYearButton_Click(object sender, RoutedEventArgs e)
    {
        MoveDisplayedRecordMonth(12);
    }

    internal void CurrentRecordMonthButton_Click(object sender, RoutedEventArgs e)
    {
        _displayedRecordMonth = GetCurrentRecordMonth();
        RefreshRecordArea(DateTimeOffset.Now);
    }

    internal void AddProjectButton_Click(object sender, RoutedEventArgs e)
    {
        string projectName = GetNextDefaultProjectName();

        if (!_engine.TryAddProject(projectName, out ProjectDefinition project))
        {
            Timer.TimerStatusText = "프로젝트를 추가하지 못했습니다.";
            return;
        }

        _selectedProject = project;
        PersistState();
        RefreshAll(DateTimeOffset.Now, $"'{project.Name}' 프로젝트를 추가했습니다.");
    }

    internal void DeleteProjectButton_Click(object sender, RoutedEventArgs e)
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
            Timer.TimerStatusText = "실행 중인 프로젝트는 삭제할 수 없습니다.";
            return;
        }

        _selectedProject = _engine.Projects.FirstOrDefault();
        PersistState();
        RefreshAll(DateTimeOffset.Now, "프로젝트를 삭제했습니다.");
    }

    internal void EditSelectedProjectButton_Click(object sender, RoutedEventArgs e)
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
        PersistState();
        RefreshAll(DateTimeOffset.Now, "프로젝트 이름을 변경했습니다.");
    }

    internal void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Timer.SelectedProjectRow is null)
        {
            return;
        }

        ProjectDefinition? project = _engine.Projects.FirstOrDefault(item => item.Id == Timer.SelectedProjectRow.ProjectId);
        if (project is null)
        {
            return;
        }

        _selectedProject = project;
        RefreshSelectedProjectArea(DateTimeOffset.Now, "선택한 프로젝트를 표시합니다.");
    }

    internal void TimerActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            Timer.SelectedProjectTitle = "먼저 프로젝트를 선택해주세요.";
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
            PersistState();
            RefreshAll(now, $"'{record.ProjectName}' 타이머를 종료했습니다.");
        }
    }

    internal void OpenProgramManagerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            Timer.TimerStatusText = "프로그램을 추가하려면 먼저 프로젝트를 선택해주세요.";
            return;
        }

        ProgramManagerWindow manager = new(_engine, _selectedProject, _currentProcessId, PersistState)
        {
            Owner = this
        };
        _ = manager.ShowDialog();
        PersistState();
        RefreshAll(DateTimeOffset.Now, "등록 프로그램 변경사항을 반영했습니다.");
    }

    internal void EditProgramButton_Click(object sender, RoutedEventArgs e)
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
        PersistState();

        if (_engine.IsRunning && _engine.ActiveProjectId == _selectedProject.Id)
        {
            FocusObservation observation = ForegroundWindowTracker.GetCurrentFocusedApplication(_currentProcessId);
            _engine.ObserveFocusedProgram(observation.Application?.ProcessName, now);
        }

        RefreshAll(now, "프로그램 표시 이름을 변경했습니다.");
    }

    internal void DeleteProgramButton_Click(object sender, RoutedEventArgs e)
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
        PersistState();
        RefreshAll(now, "등록 프로그램을 삭제했습니다.");
    }

    internal void ProgramSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshSelectedProjectArea(DateTimeOffset.Now, Timer.TimerStatusText);
    }

    internal void RecordFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshRecordArea(DateTimeOffset.Now);
    }

    internal void CalendarDayBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not Border { DataContext: CalendarDayRow row } ||
            row.Date is null)
        {
            HideCalendarHoverCard();
            return;
        }

        if (!_calendarHoverLinesByDate.TryGetValue(row.Date.Value, out IReadOnlyList<string>? lines) ||
            lines.Count == 0)
        {
            HideCalendarHoverCard();
            return;
        }

        _hoveredCalendarDate = row.Date;
        DailyRecord.CalendarHoverTitle = FormatCalendarHoverTitle(row.Date.Value);
        SetCalendarHoverLines(lines);
        DailyRecord.CalendarHoverCardVisibility = Visibility.Visible;
    }

    internal void CalendarDayBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not Border { DataContext: CalendarDayRow row } ||
            row.Date is null ||
            _hoveredCalendarDate != row.Date)
        {
            return;
        }

        HideCalendarHoverCard();
    }

    private void SetSelectedTab(MainMenuTab tab)
    {
        Menu.SelectTab(tab);

        if (tab == MainMenuTab.DailyRecord)
        {
            RefreshRecordViewState();
            RefreshRecordArea(DateTimeOffset.Now);
        }
    }

    private void RefreshRecordViewState()
    {
        DailyRecord.CalendarRecordVisibility = Visibility.Visible;
        DailyRecord.RecentRecordVisibility = Visibility.Collapsed;
        DailyRecord.CalendarButtonBackground = RecordSelectedButtonBackground;
        DailyRecord.RecentButtonBackground = RecordUnselectedButtonBackground;
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

        Timer.ProjectRows.Clear();
        foreach (ProjectDefinition project in _engine.Projects.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            bool isActiveProject = _engine.ActiveProjectId == project.Id;
            string timerText = isActiveProject ? FormatDuration(_engine.GetCurrentRunDuration(project.Id, observedAt)) : string.Empty;
            string statusText = isActiveProject ? $"{timerText} 실행 중" : string.Empty;

            Timer.ProjectRows.Add(new ProjectSidebarRow(
                project.Id,
                project.Name,
                string.Empty,
                statusText));
        }

        if (selectedProjectId.HasValue)
        {
            Timer.SelectedProjectRow = Timer.ProjectRows.FirstOrDefault(item => item.ProjectId == selectedProjectId.Value);
            _selectedProject = _engine.Projects.FirstOrDefault(item => item.Id == selectedProjectId.Value);
        }
        else if (_engine.Projects.Count > 0)
        {
            _selectedProject = _engine.Projects[0];
            Timer.SelectedProjectRow = Timer.ProjectRows.FirstOrDefault(item => item.ProjectId == _selectedProject.Id);
        }
        else
        {
            Timer.SelectedProjectRow = null;
            _selectedProject = null;
        }
    }

    private void RefreshSelectedProjectArea(DateTimeOffset observedAt, string message)
    {
        Timer.IsProjectEditEnabled = _selectedProject is not null;
        Timer.IsProjectDeleteEnabled = _selectedProject is not null && _engine.ActiveProjectId != _selectedProject.Id;

        if (_selectedProject is null)
        {
            Timer.SelectedProjectTitle = "프로젝트를 추가해보세요";
            Timer.ActiveSessionPeriodText = string.Empty;
            Timer.TimerStatusText = message;
            Timer.FocusStatusText = string.Empty;
            Timer.ActiveProjectWallClockText = "00:00:00";
            Timer.ActiveProjectElapsedText = "00:00:00";
            Timer.SelectedProjectTodayText = "00:00:00";
            Timer.IsTimerActionEnabled = false;
            Timer.TimerActionButtonText = "시작";
            Timer.TimerActionButtonBackground = DisabledButtonBackground;
            Timer.TimerActionButtonForeground = DefaultButtonForeground;
            Timer.RegisteredProgramRows.Clear();
            return;
        }

        bool isActiveProject = _engine.ActiveProjectId == _selectedProject.Id;
        ProgramSortMode sortMode = Timer.SelectedProgramSortOption?.Mode ?? ProgramSortMode.MostUsed;
        Dictionary<string, string> initialDisplayNameByProcessName = _engine
            .GetRegisteredProgramInfos(_selectedProject.Id)
            .ToDictionary(
                info => info.Program.ProcessName,
                info => info.InitialDisplayName,
                StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, ProcessRunState> processStates = RunningProcessCatalog.GetProcessRunStates(_currentProcessId);

        Timer.RegisteredProgramRows.Clear();
        foreach (ProgramFocusSummary summary in _engine.GetCurrentSessionProgramSummaries(_selectedProject.Id, observedAt, sortMode))
        {
            (string statusBrush, string statusText) = GetProgramRuntimeStatus(summary.Program.ProcessName, processStates);
            Timer.RegisteredProgramRows.Add(new RegisteredProgramRow(
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

        Timer.SelectedProjectTitle = _selectedProject.Name;
        Timer.ActiveSessionPeriodText = _engine.ActiveStartedAt.HasValue && isActiveProject
            ? $"시작 {FormatDateTime(_engine.ActiveStartedAt.Value)} / 종료 대기 중"
            : string.Empty;
        Timer.ActiveProjectElapsedText = isActiveProject
            ? FormatDuration(_engine.GetCurrentRunDuration(_selectedProject.Id, observedAt))
            : "00:00:00";
        Timer.ActiveProjectWallClockText = isActiveProject
            ? FormatDuration(_engine.GetCurrentWallClockDuration(_selectedProject.Id, observedAt))
            : "00:00:00";
        Timer.SelectedProjectTodayText = FormatDuration(_engine.GetTodayDuration(
            DateOnly.FromDateTime(observedAt.LocalDateTime.Date),
            observedAt,
            _selectedProject.Id));

        Timer.FocusStatusText = isActiveProject
            ? (_engine.ActiveFocusedProgramName is null
                ? "등록 프로그램이 포커스될 때까지 프로젝트 타이머는 멈춰 있습니다."
                : $"현재 포커스 프로그램: {_engine.ActiveFocusedProgramName}")
            : string.Empty;

        Timer.TimerStatusText = message;
        Timer.IsTimerActionEnabled = !anotherProjectIsRunning;
        Timer.TimerActionButtonText = !_engine.IsRunning
            ? "시작"
            : isActiveProject
                ? "종료"
                : "실행 중";
        Timer.TimerActionButtonBackground = !_engine.IsRunning
            ? StartButtonBackground
            : isActiveProject
                ? StopButtonBackground
                : DisabledButtonBackground;
        Timer.TimerActionButtonForeground = !_engine.IsRunning
            ? StartButtonForeground
            : DefaultButtonForeground;
    }

    private void RefreshRecordFilters()
    {
        Guid? selectedFilterProjectId = DailyRecord.SelectedRecordFilter?.ProjectId;

        DailyRecord.RecordFilterOptions.Clear();
        DailyRecord.RecordFilterOptions.Add(new RecordFilterOption(null, "<모든 프로젝트>"));
        foreach (ProjectDefinition project in _engine.Projects.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            DailyRecord.RecordFilterOptions.Add(new RecordFilterOption(project.Id, project.Name));
        }

        DailyRecord.SelectedRecordFilter = DailyRecord.RecordFilterOptions.FirstOrDefault(option => option.ProjectId == selectedFilterProjectId)
            ?? DailyRecord.RecordFilterOptions[0];
    }

    private void RefreshRecordArea(DateTimeOffset observedAt)
    {
        Guid? projectFilter = DailyRecord.SelectedRecordFilter?.ProjectId;
        DailyRecord.SelectedRecordFilterLabel = DailyRecord.SelectedRecordFilter?.Label ?? "<모든 프로젝트>";
        DailyRecord.DisplayedRecordMonthText = FormatRecordMonth(_displayedRecordMonth);

        DateOnly today = DateOnly.FromDateTime(observedAt.LocalDateTime.Date);
        DailyRecord.TodayWorkedText = FormatDuration(_engine.GetTodayDuration(today, observedAt, projectFilter));
        DailyRecord.RecordHeadlineText = DailyRecord.TodayWorkedText == "00:00:00"
            ? "오늘은 아직 작업 기록이 없습니다."
            : $"오늘은 {DailyRecord.TodayWorkedText} 작업했습니다.";

        RefreshCalendar(today, _displayedRecordMonth, observedAt, projectFilter);
        RefreshRecentRecords(projectFilter);
        RefreshRecordViewState();
    }

    private void RefreshCalendar(
        DateOnly today,
        DateOnly displayedRecordMonth,
        DateTimeOffset observedAt,
        Guid? projectFilter)
    {
        DailyRecord.CalendarRows.Clear();

        DateOnly firstDay = new(displayedRecordMonth.Year, displayedRecordMonth.Month, 1);
        DateOnly lastDay = firstDay.AddMonths(1).AddDays(-1);
        int leadingBlankCount = (int)firstDay.DayOfWeek;

        IReadOnlyList<DailyDurationSummary> summaries = _engine.GetDailyDurationSummaries(firstDay, lastDay, observedAt, projectFilter);
        Dictionary<DateOnly, DailyDurationSummary> summaryByDate = summaries.ToDictionary(summary => summary.Date);
        Dictionary<DateOnly, IReadOnlyList<string>> detailByDate = BuildCalendarHoverLinesByDate(
            firstDay,
            lastDay,
            observedAt,
            projectFilter);

        for (int index = 0; index < leadingBlankCount; index++)
        {
            DailyRecord.CalendarRows.Add(new CalendarDayRow(null, string.Empty, string.Empty, false, false, true));
        }

        for (DateOnly date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            TimeSpan duration = summaryByDate.GetValueOrDefault(date)?.TotalDuration ?? TimeSpan.Zero;
            DailyRecord.CalendarRows.Add(new CalendarDayRow(
                date,
                date.Day.ToString(CultureInfo.CurrentCulture),
                duration == TimeSpan.Zero ? string.Empty : FormatDurationShort(duration),
                duration > TimeSpan.Zero,
                date == today,
                false));
        }

        _calendarHoverLinesByDate = detailByDate;
        RefreshCalendarHoverCard(detailByDate);
    }

    private void RefreshRecentRecords(Guid? projectFilter)
    {
        DailyRecord.RecentRecordRows.Clear();

        foreach (ProjectTimerRecord record in _engine.GetRecentRecords(12, projectFilter))
        {
            string programSummaryText = record.ProgramSummaries.Count == 0
                ? "등록 프로그램 포커스 기록 없음"
                : string.Join(" / ", record.ProgramSummaries.Select(summary =>
                    $"{summary.Program.DisplayName} {FormatDuration(summary.FocusDuration)}"));

            DailyRecord.RecentRecordRows.Add(new RecentRecordRow(
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

    private static string FormatRecordMonth(DateOnly value)
    {
        return value.ToDateTime(TimeOnly.MinValue).ToString("yyyy년 M월", CultureInfo.CurrentCulture);
    }

    private static string FormatCalendarHoverTitle(DateOnly date)
    {
        return date.ToDateTime(TimeOnly.MinValue).ToString("yyyy년 M월 d일", CultureInfo.CurrentCulture);
    }

    private static DateOnly GetCurrentRecordMonth()
    {
        DateTime today = DateTime.Now;
        return new DateOnly(today.Year, today.Month, 1);
    }

    private static string BuildStorePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FocusTrackingTimer",
            "focus-tracking-timer.db");
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

    private Dictionary<DateOnly, IReadOnlyList<string>> BuildCalendarHoverLinesByDate(
        DateOnly firstDay,
        DateOnly lastDay,
        DateTimeOffset observedAt,
        Guid? projectFilter)
    {
        if (projectFilter.HasValue)
        {
            return [];
        }

        return _engine.GetDailyProjectDurationSummaries(firstDay, lastDay, observedAt)
            .GroupBy(summary => summary.Date)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)[.. group
                    .OrderByDescending(static summary => summary.TotalDuration)
                    .ThenBy(static summary => summary.ProjectName, StringComparer.CurrentCultureIgnoreCase)
                    .Select(summary => $"{summary.ProjectName} {FormatDuration(summary.TotalDuration)}")]);
    }

    private void RefreshCalendarHoverCard(Dictionary<DateOnly, IReadOnlyList<string>> detailByDate)
    {
        if (_hoveredCalendarDate is not { } hoveredDate)
        {
            DailyRecord.CalendarHoverCardVisibility = Visibility.Collapsed;
            return;
        }

        if (!detailByDate.TryGetValue(hoveredDate, out IReadOnlyList<string>? lines) ||
            lines.Count == 0)
        {
            HideCalendarHoverCard();
            return;
        }

        DailyRecord.CalendarHoverTitle = FormatCalendarHoverTitle(hoveredDate);
        SetCalendarHoverLines(lines);
        DailyRecord.CalendarHoverCardVisibility = Visibility.Visible;
    }

    private void HideCalendarHoverCard()
    {
        _hoveredCalendarDate = null;
        DailyRecord.CalendarHoverTitle = string.Empty;
        DailyRecord.CalendarHoverLines.Clear();
        DailyRecord.CalendarHoverCardVisibility = Visibility.Collapsed;
    }

    private void SetCalendarHoverLines(IEnumerable<string> lines)
    {
        DailyRecord.CalendarHoverLines.Clear();
        foreach (string line in lines)
        {
            DailyRecord.CalendarHoverLines.Add(line);
        }
    }

    private void PersistState()
    {
        try
        {
            _store.SaveState(_engine.CreateStateSnapshot());
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"데이터를 저장하지 못했습니다.{Environment.NewLine}{exception.Message}",
                "SQLite 저장 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void MoveDisplayedRecordMonth(int monthOffset)
    {
        _displayedRecordMonth = _displayedRecordMonth.AddMonths(monthOffset);
        RefreshRecordArea(DateTimeOffset.Now);
    }

}
