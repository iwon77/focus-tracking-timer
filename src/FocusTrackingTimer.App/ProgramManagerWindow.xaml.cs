using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FocusTrackingTimer.Core.Tracking;
using Microsoft.Win32;

namespace FocusTrackingTimer.App;

public partial class ProgramManagerWindow : Window, INotifyPropertyChanged
{
    private readonly ProjectTimerEngine _engine;
    private readonly Action? _onProgramsChanged;
    private readonly Guid _projectId;
    private readonly int _currentProcessId;
    private readonly DispatcherTimer _refreshTimer;
    private string _manualExecutableInput = string.Empty;

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
        TitleText = $"{project.Name} 프로그램 추가";

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;

        Loaded += OnLoaded;
        Closing += OnClosing;

        RefreshRows();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string TitleText { get; }

    public ObservableCollection<RunningProcessRow> RunningProcesses { get; } = [];

    public string ManualExecutableInput
    {
        get => _manualExecutableInput;
        set => SetProperty(ref _manualExecutableInput, value);
    }

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

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            ManualExecutableInput = dialog.FileName;
        }
    }

    private void AddManualButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TrackedApplication application = TrackedApplication.FromExecutableInput(ManualExecutableInput);
            bool wasAdded = _engine.TryRegisterProgram(_projectId, application);
            if (!wasAdded)
            {
                MessageBox.Show(this, "이미 등록된 프로그램입니다.", "프로그램 등록");
            }

            ManualExecutableInput = string.Empty;
            _onProgramsChanged?.Invoke();
            RefreshRows();
        }
        catch (ArgumentException)
        {
            MessageBox.Show(this, "실행 파일 이름 또는 경로를 입력해주세요.", "프로그램 등록");
        }
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

    private void RefreshRows()
    {
        HashSet<string> registeredProcessNames = _engine
            .GetRegisteredProgramInfos(_projectId)
            .Select(item => item.Program.ProcessName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        RunningProcesses.Clear();
        foreach (TrackedApplication application in RunningProcessCatalog.GetVisibleProcesses(_currentProcessId)
                     .Where(application => !registeredProcessNames.Contains(application.ProcessName)))
        {
            RunningProcesses.Add(new RunningProcessRow(application.DisplayName, application.ProcessName));
        }
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
}
