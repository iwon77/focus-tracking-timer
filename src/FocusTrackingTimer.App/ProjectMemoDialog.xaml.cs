using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace FocusTrackingTimer.App;

public partial class ProjectMemoDialog : Window, INotifyPropertyChanged
{
    private string _memoText;

    public ProjectMemoDialog(string projectName, DateTimeOffset createdAt, string memo)
    {
        InitializeComponent();
        ProjectName = projectName;
        CreatedAtText = $"생성일: {createdAt.LocalDateTime:yyyy-MM-dd}";
        _memoText = memo;
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ProjectName { get; }

    public string CreatedAtText { get; }

    public string MemoText
    {
        get => _memoText;
        set => SetProperty(ref _memoText, value);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
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
