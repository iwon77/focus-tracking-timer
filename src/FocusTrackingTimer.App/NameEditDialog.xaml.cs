using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace FocusTrackingTimer.App;

public partial class NameEditDialog : Window, INotifyPropertyChanged
{
    private string _nameValue;

    public NameEditDialog(string titleText, string initialName, string placeholderText = "")
    {
        InitializeComponent();
        TitleText = titleText;
        PlaceholderText = placeholderText;
        _nameValue = initialName;
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string TitleText { get; }

    public string PlaceholderText { get; }

    public string NameValue
    {
        get => _nameValue;
        set => SetProperty(ref _nameValue, value);
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
