using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using FocusTrackingTimer.App.Infrastructure;

namespace FocusTrackingTimer.App.ViewModels;

public sealed class TimerViewModel : ObservableObject
{
    private string _selectedProjectTitle = "작업을 추가해보세요";
    private string _activeSessionPeriodText = "작업 시작/종료 시간이 여기에 표시됩니다.";
    private string _timerStatusText = "시작 버튼을 누르면 등록 프로그램 포커스 시간만 기록합니다.";
    private string _focusStatusText = "등록 프로그램 포커스 상태가 여기에 표시됩니다.";
    private string _runningProjectNameText = "-";
    private string _runningProjectWallClockText = "-";
    private string _runningProjectFocusText = "-";
    private Visibility _runningProjectSummaryPipButtonVisibility = Visibility.Collapsed;
    private string _activeProjectWallClockText = "00:00:00";
    private string _activeProjectElapsedText = "00:00:00";
    private string _selectedProjectTodayText = "00:00:00";
    private bool _isTimerActionEnabled;
    private bool _isTimerActionOutlined;
    private bool _isTimerStopEnabled;
    private bool _isProjectEditEnabled;
    private bool _isProjectDeleteEnabled;
    private bool _isProjectMemoEnabled;
    private string _timerActionButtonText = "시작";
    private Brush _timerActionButtonBackground;
    private Brush _timerActionButtonForeground;
    private ProjectSidebarRow? _selectedProjectRow;
    private ProjectSortOption? _selectedProjectSortOption;
    private ProgramSortOption? _selectedProgramSortOption;

    public TimerViewModel(Brush timerActionButtonBackground, Brush timerActionButtonForeground)
    {
        _timerActionButtonBackground = timerActionButtonBackground;
        _timerActionButtonForeground = timerActionButtonForeground;
    }

    public ObservableCollection<ProjectSidebarRow> ProjectRows { get; } = [];

    public ObservableCollection<ProjectSidebarRow> PinnedProjectRows { get; } = [];

    public ObservableCollection<ProjectSortOption> ProjectSortOptions { get; } = [];

    public ObservableCollection<RegisteredProgramRow> RegisteredProgramRows { get; } = [];

    public ObservableCollection<ProgramSortOption> ProgramSortOptions { get; } = [];

    public string SelectedProjectTitle
    {
        get => _selectedProjectTitle;
        set => SetProperty(ref _selectedProjectTitle, value);
    }

    public string ActiveSessionPeriodText
    {
        get => _activeSessionPeriodText;
        set => SetProperty(ref _activeSessionPeriodText, value);
    }

    public string TimerStatusText
    {
        get => _timerStatusText;
        set => SetProperty(ref _timerStatusText, value);
    }

    public string FocusStatusText
    {
        get => _focusStatusText;
        set => SetProperty(ref _focusStatusText, value);
    }

    public string RunningProjectNameText
    {
        get => _runningProjectNameText;
        set => SetProperty(ref _runningProjectNameText, value);
    }

    public string RunningProjectWallClockText
    {
        get => _runningProjectWallClockText;
        set => SetProperty(ref _runningProjectWallClockText, value);
    }

    public string RunningProjectFocusText
    {
        get => _runningProjectFocusText;
        set => SetProperty(ref _runningProjectFocusText, value);
    }

    public Visibility RunningProjectSummaryPipButtonVisibility
    {
        get => _runningProjectSummaryPipButtonVisibility;
        set => SetProperty(ref _runningProjectSummaryPipButtonVisibility, value);
    }

    public string ActiveProjectElapsedText
    {
        get => _activeProjectElapsedText;
        set => SetProperty(ref _activeProjectElapsedText, value);
    }

    public string ActiveProjectWallClockText
    {
        get => _activeProjectWallClockText;
        set => SetProperty(ref _activeProjectWallClockText, value);
    }

    public string SelectedProjectTodayText
    {
        get => _selectedProjectTodayText;
        set => SetProperty(ref _selectedProjectTodayText, value);
    }

    public bool IsTimerActionEnabled
    {
        get => _isTimerActionEnabled;
        set => SetProperty(ref _isTimerActionEnabled, value);
    }

    public bool IsTimerActionOutlined
    {
        get => _isTimerActionOutlined;
        set => SetProperty(ref _isTimerActionOutlined, value);
    }

    public bool IsTimerStopEnabled
    {
        get => _isTimerStopEnabled;
        set => SetProperty(ref _isTimerStopEnabled, value);
    }

    public bool IsProjectEditEnabled
    {
        get => _isProjectEditEnabled;
        set => SetProperty(ref _isProjectEditEnabled, value);
    }

    public bool IsProjectDeleteEnabled
    {
        get => _isProjectDeleteEnabled;
        set => SetProperty(ref _isProjectDeleteEnabled, value);
    }

    public bool IsProjectMemoEnabled
    {
        get => _isProjectMemoEnabled;
        set => SetProperty(ref _isProjectMemoEnabled, value);
    }

    public string TimerActionButtonText
    {
        get => _timerActionButtonText;
        set => SetProperty(ref _timerActionButtonText, value);
    }

    public Brush TimerActionButtonBackground
    {
        get => _timerActionButtonBackground;
        set => SetProperty(ref _timerActionButtonBackground, value);
    }

    public Brush TimerActionButtonForeground
    {
        get => _timerActionButtonForeground;
        set => SetProperty(ref _timerActionButtonForeground, value);
    }

    public ProjectSidebarRow? SelectedProjectRow
    {
        get => _selectedProjectRow;
        set => SetProperty(ref _selectedProjectRow, value);
    }

    public ProjectSortOption? SelectedProjectSortOption
    {
        get => _selectedProjectSortOption;
        set => SetProperty(ref _selectedProjectSortOption, value);
    }

    public ProgramSortOption? SelectedProgramSortOption
    {
        get => _selectedProgramSortOption;
        set => SetProperty(ref _selectedProgramSortOption, value);
    }
}
