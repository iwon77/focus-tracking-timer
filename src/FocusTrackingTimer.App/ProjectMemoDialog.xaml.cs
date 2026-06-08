using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using FocusTrackingTimer.App.Infrastructure;

namespace FocusTrackingTimer.App;

public partial class ProjectMemoDialog : Window, INotifyPropertyChanged
{
    private const int MaxMemoBytes = 500;
    private string _memoText;

    public ProjectMemoDialog(string projectName, DateTimeOffset createdAt, DateTimeOffset memoUpdatedAt, string memo)
    {
        InitializeComponent();
        ProjectName = projectName;
        CreatedAtText = $"프로젝트 생성일: {AppTimeFormatter.FormatProjectMetaDateTime(createdAt)}";
        MemoUpdatedAtText = $"마지막 메모 수정일: {AppTimeFormatter.FormatProjectMetaDateTime(memoUpdatedAt)}";
        _memoText = memo;
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ProjectName { get; }

    public string CreatedAtText { get; }

    public string MemoUpdatedAtText { get; }

    public string MemoText
    {
        get => _memoText;
        set => SetProperty(ref _memoText, TrimToUtf8ByteLimit(value ?? string.Empty, MaxMemoBytes));
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

    private static string TrimToUtf8ByteLimit(string text, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
        {
            return text;
        }

        int length = text.Length;
        while (length > 0)
        {
            string candidate = text[..length];
            if (Encoding.UTF8.GetByteCount(candidate) <= maxBytes)
            {
                return candidate;
            }

            length--;
        }

        return string.Empty;
    }
}
