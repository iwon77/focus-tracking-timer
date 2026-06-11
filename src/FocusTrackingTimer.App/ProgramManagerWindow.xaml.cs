using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App;

public partial class ProgramManagerWindow : Window
{
    private readonly ProjectTimerEngine _engine;
    private readonly Action? _onProgramsChanged;
    private readonly Guid _projectId;
    private readonly int _currentProcessId;
    private readonly DispatcherTimer _refreshTimer;

    public ProgramManagerWindow(
        ProjectTimerEngine engine,
        ProjectDefinition project,
        int currentProcessId,
        Action? onProgramsChanged = null)
    {
        InitializeComponent();
        DataContext = this;
        _engine = engine;
        _onProgramsChanged = onProgramsChanged;
        _projectId = project.Id;
        _currentProcessId = currentProcessId;
        TitleText = "프로그램 추가";

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;

        Loaded += OnLoaded;
        Closing += OnClosing;

        RefreshRows();
    }

    public string TitleText { get; }

    public ObservableCollection<RunningProcessRow> RunningProcesses { get; } = [];

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Start();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _refreshTimer.Stop();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        RefreshRows();
    }

    private void AddDetectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: RunningProcessRow row })
        {
            return;
        }

        TrackedApplication application = new(row.ProcessName, row.DisplayName);
        bool wasAdded = _engine.TryRegisterProgram(_projectId, application);
        if (!wasAdded)
        {
            MessageBox.Show(this, "이미 등록된 프로그램입니다.", "프로그램 추가");
        }

        _onProgramsChanged?.Invoke();
        RefreshRows();
    }

    private void FocusDetectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: RunningProcessRow row })
        {
            return;
        }

        if (!WindowFocusService.TryFocusProcessMainWindow(row.ProcessId))
        {
            MessageBox.Show(this, "이 프로그램은 화면으로 띄울 수 없습니다.", "프로그램 보기");
        }
    }

    private void RefreshRows()
    {
        HashSet<string> registeredProcessNames = _engine
            .GetRegisteredProgramInfos(_projectId)
            .Select(item => item.Program.ProcessName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<RunningProcessRow> applications = [.. RunningProcessCatalog.GetVisibleProcesses(_currentProcessId)
            .Where(application => !registeredProcessNames.Contains(application.ProcessName))];

        RunningProcesses.Clear();
        for (int index = 0; index < applications.Count; index++)
        {
            RunningProcessRow application = applications[index];
            RunningProcesses.Add(application with
            {
                IsFirst = index == 0,
                IsLast = index == applications.Count - 1
            });
        }
    }

}
