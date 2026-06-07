using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FocusTrackingTimer.App.Features.DailyRecords;
using FocusTrackingTimer.App.Features.Timer;
using FocusTrackingTimer.App.ViewModels;
using FocusTrackingTimer.Core.Persistence;
using FocusTrackingTimer.Core.Tracking;

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
    private readonly TimerFeatureController _timerFeature;
    private readonly DailyRecordFeatureController _dailyRecordFeature;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        Timer.ProgramSortOptions.Add(new ProgramSortOption(ProgramSortMode.MostUsed, "많이 사용한 순"));
        Timer.ProgramSortOptions.Add(new ProgramSortOption(ProgramSortMode.Registered, "등록 순서"));
        Timer.ProgramSortOptions.Add(new ProgramSortOption(ProgramSortMode.Manual, "사용자 지정"));
        Timer.SelectedProgramSortOption = Timer.ProgramSortOptions[0];

        _timerFeature = new TimerFeatureController(
            this,
            _engine,
            Timer,
            Environment.ProcessId,
            PersistState,
            RefreshAll,
            StartButtonBackground,
            StopButtonBackground,
            DisabledButtonBackground,
            StartButtonForeground,
            DefaultButtonForeground);
        _dailyRecordFeature = new DailyRecordFeatureController(
            _engine,
            DailyRecord,
            RecordSelectedButtonBackground,
            RecordUnselectedButtonBackground);

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
        string focusMessage = _timerFeature.RefreshFocusTracking(observedAt);
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
        _dailyRecordFeature.ShowCalendarRecord();
    }

    internal void RecentRecordButton_Click(object sender, RoutedEventArgs e)
    {
        _dailyRecordFeature.ShowRecentRecord();
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

    internal void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _timerFeature.SelectProject(Timer.SelectedProjectRow);
    }

    internal void TimerActionButton_Click(object sender, RoutedEventArgs e)
    {
        _timerFeature.ToggleTimer();
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

    internal void FocusRegisteredProgramButton_Click(object sender, RoutedEventArgs e)
    {
        RegisteredProgramRow? row = (sender as FrameworkElement)?.DataContext as RegisteredProgramRow;
        _timerFeature.FocusRegisteredProgram(row);
    }

    internal void ProgramSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _timerFeature.RefreshSelectedProjectArea(DateTimeOffset.Now, Timer.TimerStatusText);
    }

    internal void RecordFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _dailyRecordFeature.RefreshRecordArea(DateTimeOffset.Now);
    }

    internal void CalendarDayBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        CalendarDayRow? row = (sender as Border)?.DataContext as CalendarDayRow;
        _dailyRecordFeature.ShowCalendarHover(row);
    }

    internal void CalendarDayBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        CalendarDayRow? row = (sender as Border)?.DataContext as CalendarDayRow;
        _dailyRecordFeature.HideCalendarHover(row);
    }

    private void SetSelectedTab(MainMenuTab tab)
    {
        Menu.SelectTab(tab);

        if (tab == MainMenuTab.DailyRecord)
        {
            _dailyRecordFeature.RefreshRecordViewState();
            _dailyRecordFeature.RefreshRecordArea(DateTimeOffset.Now);
        }
    }

    private void RefreshAll(DateTimeOffset observedAt, string message)
    {
        _timerFeature.RefreshProjectSidebar(observedAt);
        _timerFeature.RefreshSelectedProjectArea(observedAt, message);
        _dailyRecordFeature.RefreshRecordFilters();
        _dailyRecordFeature.RefreshRecordArea(observedAt);
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

    private static string BuildStorePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FocusTrackingTimer",
            "focus-tracking-timer.db");
    }
}
