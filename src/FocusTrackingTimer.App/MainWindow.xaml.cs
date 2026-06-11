using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FocusTrackingTimer.App.Features.DailyRecords;
using FocusTrackingTimer.App.Features.Performance;
using FocusTrackingTimer.App.Features.Timer;
using FocusTrackingTimer.App.Features.WeeklyRecords;
using FocusTrackingTimer.App.Infrastructure;
using FocusTrackingTimer.App.ViewModels;
using FocusTrackingTimer.Core.Persistence;
using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App;

public partial class MainWindow : Window
{
    private static readonly Brush StartButtonBackground = ThemeBrushes.Focus;
    private static readonly Brush StopButtonBackground = ThemeBrushes.ActiveText;
    private static readonly Brush DisabledButtonBackground = ThemeBrushes.HoverBorder;
    private static readonly Brush StartButtonForeground = ThemeBrushes.ActiveText;
    private static readonly Brush DefaultButtonForeground = ThemeBrushes.PrimaryText;
    private static readonly TimeSpan FocusFallbackInterval = TimeSpan.FromSeconds(60);

    private readonly ProjectTimerEngine _engine = new();
    private readonly SqliteProjectTimerStore _store = new(BuildStorePath());
    private readonly DispatcherTimer _uiTimer;
    private readonly PerformanceMonitorService _performanceMonitor;
    private readonly ProcessRunStateScanService _processScanService;
    private readonly ForegroundFocusEventService _foregroundFocusEvents = new();
    private readonly TimerFeatureController _timerFeature;
    private readonly DailyRecordFeatureController _dailyRecordFeature;
    private readonly WeeklyRecordFeatureController _weeklyRecordFeature;
    private DateTimeOffset? _lastUiTickObservedAt;
    private DateTimeOffset? _lastFocusFallbackObservedAt;
    private PerformanceMonitorWindow? _performanceMonitorWindow;
    private RunningProjectSummaryPipWindow? _runningProjectSummaryPipWindow;

    public MainWindow()
    {
        _performanceMonitor = new PerformanceMonitorService(_store.DatabasePath, BuildPerformanceLogDirectory());
        _processScanService = new ProcessRunStateScanService(Environment.ProcessId);
        InitializeComponent();
        DataContext = this;

        Timer.ProjectSortOptions.Add(new ProjectSortOption(ProjectSortMode.Created, "추가순"));
        Timer.ProjectSortOptions.Add(new ProjectSortOption(ProjectSortMode.Name, "이름순"));
        Timer.SelectedProjectSortOption = Timer.ProjectSortOptions[0];

        Timer.ProgramSortOptions.Add(new ProgramSortOption(ProgramSortMode.Registered, "추가순"));
        Timer.ProgramSortOptions.Add(new ProgramSortOption(ProgramSortMode.RegisteredDescending, "추가역순"));
        Timer.ProgramSortOptions.Add(new ProgramSortOption(ProgramSortMode.DisplayName, "이름순"));
        Timer.ProgramSortOptions.Add(new ProgramSortOption(ProgramSortMode.MostUsed, "집중 시간순"));
        Timer.SelectedProgramSortOption = Timer.ProgramSortOptions[0];

        _timerFeature = new TimerFeatureController(
            this,
            _engine,
            _store,
            Timer,
            Environment.ProcessId,
            GetLatestProcessStates,
            PersistProjectCatalog,
            AppendCompletedRecord,
            RefreshUiAfterCommand,
            StartButtonBackground,
            StopButtonBackground,
            DisabledButtonBackground,
            StartButtonForeground,
            DefaultButtonForeground);
        _dailyRecordFeature = new DailyRecordFeatureController(_engine, _store, DailyRecord);
        _weeklyRecordFeature = new WeeklyRecordFeatureController(_engine, _store, WeeklyRecord);

        _uiTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uiTimer.Tick += UiTimer_Tick;
        _foregroundFocusEvents.ForegroundChanged += ForegroundFocusEvents_ForegroundChanged;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    public MainMenuViewModel Menu { get; } = new();

    public TimerViewModel Timer { get; } = new(StartButtonBackground, StartButtonForeground);

    public DailyRecordViewModel DailyRecord { get; } = new();

    public WeeklyRecordViewModel WeeklyRecord { get; } = new();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        string startupMessage = "프로젝트를 추가하고 등록 프로그램을 관리해보세요.";
        bool seededWeeklySample = false;
        bool seededDailySample = false;
        long persistedFocusSegmentCount = 0;

        try
        {
            IReadOnlyList<ProjectState> projectCatalog = _store.LoadProjectCatalog();
            bool hasCompletedRecords = _store.HasCompletedRecords();
            persistedFocusSegmentCount = _store.LoadFocusSegmentCount();
            _engine.ReplaceState(new ProjectTimerEngineState(projectCatalog, []));

            if (projectCatalog.Count > 0 || hasCompletedRecords)
            {
                startupMessage = "저장한 프로젝트와 완료 기록을 불러왔습니다.";
            }

            seededWeeklySample = EnsureWeeklyClickTestSeed();
            seededDailySample = EnsureDailyCalendarVisualSeed();
            if (seededWeeklySample)
            {
                startupMessage = "이번 주 클릭 테스트용 샘플 기록을 추가했습니다.";
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"저장한 데이터를 불러오지 못했습니다.{Environment.NewLine}{exception.Message}",
                "SQLite 로드 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        _performanceMonitor.Initialize(persistedFocusSegmentCount);

        if (seededWeeklySample || seededDailySample)
        {
            PersistProjectCatalog();
            FlushPendingCompletedRecords();
        }

        _processScanService.RequestRefresh();
        _foregroundFocusEvents.Start();
        RefreshTimerUi(DateTimeOffset.Now, startupMessage, GetLatestProcessStates(), allowPersistentReload: true);
        RefreshRecordFilters();
        _uiTimer.Start();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _uiTimer.Stop();
        _foregroundFocusEvents.ForegroundChanged -= ForegroundFocusEvents_ForegroundChanged;
        _foregroundFocusEvents.Stop();

        if (_engine.IsRunning)
        {
            _engine.StopProject(DateTimeOffset.Now);
        }

        PersistProjectCatalog();
        FlushPendingCompletedRecords();
        _performanceMonitor.FlushPending();
    }

    private void UiTimer_Tick(object? sender, EventArgs e)
    {
        DateTimeOffset observedAt = DateTimeOffset.Now;
        Stopwatch tickStopwatch = Stopwatch.StartNew();

        try
        {
            string message = RefreshFocusTrackingFallbackIfNeeded(observedAt);
            RefreshTimerUi(observedAt, message, GetLatestProcessStates(), allowPersistentReload: false);
        }
        finally
        {
            tickStopwatch.Stop();
            bool isDelayedTick = _lastUiTickObservedAt.HasValue &&
                observedAt - _lastUiTickObservedAt.Value > _uiTimer.Interval + TimeSpan.FromMilliseconds(200);
            _lastUiTickObservedAt = observedAt;
            _performanceMonitor.RecordUiTick(
                observedAt,
                tickStopwatch.Elapsed,
                isDelayedTick,
                null,
                0);
        }
    }

    private void ForegroundFocusEvents_ForegroundChanged(IntPtr foregroundWindowHandle)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => HandleForegroundFocusChanged(foregroundWindowHandle, DateTimeOffset.Now));
            return;
        }

        HandleForegroundFocusChanged(foregroundWindowHandle, DateTimeOffset.Now);
    }

    private void HandleForegroundFocusChanged(IntPtr foregroundWindowHandle, DateTimeOffset observedAt)
    {
        if (!_engine.IsRunning || _engine.IsPaused)
        {
            return;
        }

        _lastFocusFallbackObservedAt = observedAt;
        string message = _timerFeature.RefreshFocusTracking(
            observedAt,
            foregroundWindowHandle,
            GetLatestProcessStates());
        RefreshTimerUi(observedAt, message, GetLatestProcessStates(), allowPersistentReload: false);
    }

    private string RefreshFocusTrackingFallbackIfNeeded(DateTimeOffset observedAt)
    {
        if (!_engine.IsRunning || _engine.IsPaused)
        {
            return Timer.TimerStatusText;
        }

        if (_lastFocusFallbackObservedAt.HasValue &&
            observedAt - _lastFocusFallbackObservedAt.Value < FocusFallbackInterval)
        {
            return Timer.TimerStatusText;
        }

        _lastFocusFallbackObservedAt = observedAt;
        return _timerFeature.RefreshFocusTracking(observedAt, GetLatestProcessStates());
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

    private void OpenPerformanceMonitorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_performanceMonitorWindow is not null)
        {
            if (_performanceMonitorWindow.WindowState == WindowState.Minimized)
            {
                _performanceMonitorWindow.WindowState = WindowState.Normal;
            }

            _performanceMonitorWindow.Activate();
            return;
        }

        _performanceMonitorWindow = new PerformanceMonitorWindow(_performanceMonitor)
        {
            Owner = this
        };
        _performanceMonitorWindow.Closed += PerformanceMonitorWindow_Closed;
        _performanceMonitorWindow.Show();
    }

    private void PerformanceMonitorWindow_Closed(object? sender, EventArgs e)
    {
        if (_performanceMonitorWindow is not null)
        {
            _performanceMonitorWindow.Closed -= PerformanceMonitorWindow_Closed;
            _performanceMonitorWindow = null;
        }
    }

    internal void OpenRunningProjectSummaryPipWindow()
    {
        if (!_engine.IsRunning || !_engine.ActiveProjectId.HasValue)
        {
            MessageBox.Show(
                this,
                "프로젝트가 실행 중이지 않습니다.",
                "PIP",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (_runningProjectSummaryPipWindow is not null)
        {
            if (_runningProjectSummaryPipWindow.WindowState == WindowState.Minimized)
            {
                _runningProjectSummaryPipWindow.WindowState = WindowState.Normal;
            }

            _runningProjectSummaryPipWindow.Activate();
            return;
        }

        _runningProjectSummaryPipWindow = new RunningProjectSummaryPipWindow(this, Timer);
        _runningProjectSummaryPipWindow.Closed += RunningProjectSummaryPipWindow_Closed;
        _runningProjectSummaryPipWindow.Show();
        _runningProjectSummaryPipWindow.UpdateLayout();
        PositionRunningProjectSummaryPipWindow(_runningProjectSummaryPipWindow);
        Hide();
    }

    internal void RestoreMainWindowFromRunningProjectSummaryPip()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();

        if (_runningProjectSummaryPipWindow is not null)
        {
            _runningProjectSummaryPipWindow.RestoreMainWindowAndClose();
        }
    }

    private void RunningProjectSummaryPipWindow_Closed(object? sender, EventArgs e)
    {
        if (_runningProjectSummaryPipWindow is not null)
        {
            _runningProjectSummaryPipWindow.Closed -= RunningProjectSummaryPipWindow_Closed;
            _runningProjectSummaryPipWindow = null;
        }
    }

    private void PositionRunningProjectSummaryPipWindow(RunningProjectSummaryPipWindow pipWindow)
    {
        double leftOffset = 16d;
        double topOffset = 48d;

        pipWindow.Left = Left + leftOffset;
        pipWindow.Top = Top + topOffset;
    }

    internal void PreviousRecordYearButton_Click(object sender, RoutedEventArgs e)
    {
        _dailyRecordFeature.MoveDisplayedRecordMonth(-12);
    }

    internal void PreviousRecordMonthButton_Click(object sender, RoutedEventArgs e)
    {
        _dailyRecordFeature.MoveDisplayedRecordMonth(-1);
    }

    internal void NextRecordMonthButton_Click(object sender, RoutedEventArgs e)
    {
        _dailyRecordFeature.MoveDisplayedRecordMonth(1);
    }

    internal void NextRecordYearButton_Click(object sender, RoutedEventArgs e)
    {
        _dailyRecordFeature.MoveDisplayedRecordMonth(12);
    }

    internal void CurrentRecordMonthButton_Click(object sender, RoutedEventArgs e)
    {
        _dailyRecordFeature.MoveDisplayedRecordMonthToCurrent();
    }

    internal void PreviousWeekButton_Click(object sender, RoutedEventArgs e)
    {
        _weeklyRecordFeature.MoveDisplayedWeek(-1);
    }

    internal void CurrentWeekButton_Click(object sender, RoutedEventArgs e)
    {
        _weeklyRecordFeature.MoveDisplayedWeekToCurrent();
    }

    internal void NextWeekButton_Click(object sender, RoutedEventArgs e)
    {
        _weeklyRecordFeature.MoveDisplayedWeek(1);
    }

    internal void AddProjectButton_Click(object sender, RoutedEventArgs e)
    {
        _timerFeature.AddProject();
    }

    internal void DeleteProjectButton_Click(object sender, RoutedEventArgs e)
    {
        _timerFeature.DeleteSelectedProject();
    }

    internal void EditSelectedProjectButton_Click(object sender, RoutedEventArgs e)
    {
        _timerFeature.EditSelectedProject();
    }

    internal void PinProjectRowButton_Click(object sender, RoutedEventArgs e)
    {
        ProjectSidebarRow? row = (sender as FrameworkElement)?.DataContext as ProjectSidebarRow;
        _timerFeature.ToggleProjectPin(row);
        e.Handled = true;
    }

    internal void MemoSelectedProjectButton_Click(object sender, RoutedEventArgs e)
    {
        _timerFeature.EditSelectedProjectMemo();
    }

    internal void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _timerFeature.SelectProject(Timer.SelectedProjectRow);
    }

    internal void RunningProjectSummary_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _timerFeature.SelectActiveProject();
    }

    internal void TimerActionButton_Click(object sender, RoutedEventArgs e)
    {
        _timerFeature.ToggleTimerOrPause();
        if (_engine.IsRunning && !_engine.IsPaused)
        {
            DateTimeOffset observedAt = DateTimeOffset.Now;
            _lastFocusFallbackObservedAt = observedAt;
            string message = _timerFeature.RefreshFocusTracking(observedAt, GetLatestProcessStates());
            RefreshTimerUi(observedAt, message, GetLatestProcessStates(), allowPersistentReload: false);
        }
    }

    internal void StopTimerButton_Click(object sender, RoutedEventArgs e)
    {
        _timerFeature.StopTimer();
    }

    internal void OpenProgramManagerButton_Click(object sender, RoutedEventArgs e)
    {
        _timerFeature.OpenProgramManager();
    }

    internal void EditProgramButton_Click(object sender, RoutedEventArgs e)
    {
        RegisteredProgramRow? row = (sender as FrameworkElement)?.DataContext as RegisteredProgramRow;
        _timerFeature.EditProgram(row);
    }

    internal void DeleteProgramButton_Click(object sender, RoutedEventArgs e)
    {
        RegisteredProgramRow? row = (sender as FrameworkElement)?.DataContext as RegisteredProgramRow;
        _timerFeature.DeleteProgram(row);
    }

    internal void PinProgramButton_Click(object sender, RoutedEventArgs e)
    {
        RegisteredProgramRow? row = (sender as FrameworkElement)?.DataContext as RegisteredProgramRow;
        _timerFeature.ToggleProgramPin(row);
    }

    internal void FocusRegisteredProgramButton_Click(object sender, RoutedEventArgs e)
    {
        RegisteredProgramRow? row = (sender as FrameworkElement)?.DataContext as RegisteredProgramRow;
        _timerFeature.FocusRegisteredProgram(row);
    }

    internal void ProgramSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _timerFeature.RefreshSelectedProjectArea(
            DateTimeOffset.Now,
            Timer.TimerStatusText,
            GetLatestProcessStates(),
            allowPersistentReload: true);
    }

    internal void ProjectSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _timerFeature.RefreshProjectSidebar(DateTimeOffset.Now);
    }

    internal void RecordFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _dailyRecordFeature.RefreshRecordArea(DateTimeOffset.Now);
    }

    internal void WeeklyRecordFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _weeklyRecordFeature.RefreshWeeklyRecordArea(DateTimeOffset.Now);
    }

    internal void CalendarDayButton_Click(object sender, RoutedEventArgs e)
    {
        CalendarDayRow? row = (sender as FrameworkElement)?.DataContext as CalendarDayRow;
        _dailyRecordFeature.SelectDate(row);
    }

    internal void WeeklyRecordList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _weeklyRecordFeature.SelectRecord(WeeklyRecord.SelectedWeeklyRecordRow);
    }

    internal void WeeklySummaryDayButton_Click(object sender, RoutedEventArgs e)
    {
        WeeklyDayBubbleRow? row = (sender as FrameworkElement)?.DataContext as WeeklyDayBubbleRow;
        _weeklyRecordFeature.SelectSummaryDay(row);
    }

    private void SetSelectedTab(MainMenuTab tab)
    {
        Menu.SelectTab(tab);

        if (tab == MainMenuTab.DailyRecord)
        {
            _dailyRecordFeature.RefreshRecordArea(DateTimeOffset.Now);
        }
        else if (tab == MainMenuTab.WeeklyRecord)
        {
            _weeklyRecordFeature.RefreshWeeklyRecordArea(DateTimeOffset.Now);
        }
    }

    private void RefreshUiAfterCommand(
        DateTimeOffset observedAt,
        string message,
        bool refreshRecordFilters = false,
        bool refreshRecordViews = false)
    {
        RefreshTimerUi(observedAt, message, GetLatestProcessStates(), allowPersistentReload: true);

        if (refreshRecordFilters)
        {
            RefreshRecordFilters();
        }

        if (refreshRecordFilters || refreshRecordViews)
        {
            RefreshVisibleRecordView(observedAt);
        }
    }

    private void RefreshTimerUi(
        DateTimeOffset observedAt,
        string message,
        IReadOnlyDictionary<string, ProcessRunState>? processStates,
        bool allowPersistentReload)
    {
        _timerFeature.RefreshProjectSidebar(observedAt);
        _timerFeature.RefreshSelectedProjectArea(observedAt, message, processStates, allowPersistentReload);
    }

    private IReadOnlyDictionary<string, ProcessRunState> GetLatestProcessStates()
    {
        return _processScanService.GetLatestSnapshot().ProcessStates;
    }

    private void RefreshRecordFilters()
    {
        _dailyRecordFeature.RefreshRecordFilters();
        _weeklyRecordFeature.RefreshRecordFilters();
    }

    private void RefreshVisibleRecordView(DateTimeOffset observedAt)
    {
        if (Menu.SelectedTab == MainMenuTab.DailyRecord)
        {
            _dailyRecordFeature.RefreshRecordArea(observedAt);
            return;
        }

        if (Menu.SelectedTab == MainMenuTab.WeeklyRecord)
        {
            _weeklyRecordFeature.RefreshWeeklyRecordArea(observedAt);
        }
    }

    private void PersistSnapshotState()
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

    private void PersistProjectCatalog()
    {
        try
        {
            DateTimeOffset observedAt = DateTimeOffset.Now;
            Stopwatch stopwatch = Stopwatch.StartNew();
            _store.SaveProjectCatalog(_engine.CreateStateSnapshot().Projects);
            stopwatch.Stop();
            _performanceMonitor.RecordProjectCatalogSave(observedAt, stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"?곗씠?곕? ??ν븯吏 紐삵뻽?듬땲??{Environment.NewLine}{exception.Message}",
                "SQLite ????ㅻ쪟",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void AppendCompletedRecord(ProjectTimerRecord record)
    {
        try
        {
            DateTimeOffset observedAt = DateTimeOffset.Now;
            Stopwatch stopwatch = Stopwatch.StartNew();
            _store.AppendCompletedRecord(record);
            stopwatch.Stop();
            _ = _engine.ForgetCompletedRecord(record);
            _performanceMonitor.RecordCompletedRecordSave(observedAt, record, stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"?곗씠?곕? ??ν븯吏 紐삵뻽?듬땲??{Environment.NewLine}{exception.Message}",
                "SQLite ????ㅻ쪟",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void FlushPendingCompletedRecords()
    {
        foreach (ProjectTimerRecord record in _engine.CompletedRecords.ToList())
        {
            AppendCompletedRecord(record);
        }
    }

    private bool EnsureWeeklyClickTestSeed()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        DateOnly weekStart = GetWeekStart(DateOnly.FromDateTime(now.Date));
        DateOnly weekEnd = weekStart.AddDays(6);

        HashSet<DateOnly> datesWithRecords =
        [
            .. _store.LoadRecordSlices(weekStart, weekEnd)
                .Select(slice => DateOnly.FromDateTime(slice.StartedAt.LocalDateTime.Date))
        ];

        if (datesWithRecords.Count >= 2)
        {
            return false;
        }

        DateOnly? seedDate = null;
        for (DateOnly date = weekStart; date <= weekEnd; date = date.AddDays(1))
        {
            if (!datesWithRecords.Contains(date))
            {
                seedDate = date;
                break;
            }
        }

        if (seedDate is null)
        {
            return false;
        }

        _engine.TryAddProject("클릭 테스트 프로젝트", out ProjectDefinition project);
        _engine.TryRegisterProgram(project.Id, new TrackedApplication("notepad", "notepad"), now);

        DateTimeOffset startedAt = CreateLocalDateTimeOffset(seedDate.Value, new TimeOnly(15, 33));
        DateTimeOffset endedAt = startedAt.AddMinutes(21);

        _engine.StartProject(project.Id, startedAt);
        _engine.ObserveFocusedProgram("notepad", startedAt);
        _engine.StopProject(endedAt);

        return true;
    }

    private bool EnsureDailyCalendarVisualSeed()
    {
        DateOnly month = new(DateTime.Now.Year, DateTime.Now.Month, 1);
        string sampleProjectName = "?쇨컙 湲곕줉 ?덉떆 ?꾨줈?앺듃";
        DateOnly[] targetDates =
        [
            new DateOnly(month.Year, month.Month, 9),
            new DateOnly(month.Year, month.Month, 10),
            new DateOnly(month.Year, month.Month, 11),
            new DateOnly(month.Year, month.Month, 12),
            new DateOnly(month.Year, month.Month, 13)
        ];
        int[] focusMinutes = [15, 45, 90, 180, 300];
        DateOnly monthEnd = month.AddMonths(1).AddDays(-1);

        HashSet<DateOnly> existingDates =
        [
            .. _store.LoadRecordSlices(month, monthEnd)
                .Where(slice => string.Equals(slice.ProjectName, sampleProjectName, StringComparison.OrdinalIgnoreCase))
                .Select(slice => DateOnly.FromDateTime(slice.StartedAt.LocalDateTime.Date))
        ];

        if (targetDates.All(existingDates.Contains))
        {
            return false;
        }

        _engine.TryAddProject(sampleProjectName, out ProjectDefinition project);
        _engine.TryRegisterProgram(project.Id, new TrackedApplication("notepad", "notepad"), DateTimeOffset.Now);

        bool seeded = false;
        for (int index = 0; index < targetDates.Length; index++)
        {
            if (existingDates.Contains(targetDates[index]))
            {
                continue;
            }

            DateTimeOffset startedAt = CreateLocalDateTimeOffset(targetDates[index], new TimeOnly(9, 0));
            DateTimeOffset endedAt = startedAt.AddMinutes(focusMinutes[index]);

            _engine.StartProject(project.Id, startedAt);
            _engine.ObserveFocusedProgram("notepad", startedAt);
            _engine.StopProject(endedAt);
            seeded = true;
        }

        return seeded;
    }

    private static string BuildStorePath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "data",
            "focus-tracking-timer.db");
    }

    private static string BuildPerformanceLogDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FocusTrackingTimer",
            "performance");
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        int offset = (7 + (date.DayOfWeek - DayOfWeek.Sunday)) % 7;
        return date.AddDays(-offset);
    }

    private static DateTimeOffset CreateLocalDateTimeOffset(DateOnly date, TimeOnly time)
    {
        DateTime localDateTime = date.ToDateTime(time);
        return new DateTimeOffset(localDateTime, TimeZoneInfo.Local.GetUtcOffset(localDateTime));
    }
}
