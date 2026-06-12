using System.Windows;
using System.Windows.Media;
using FocusTrackingTimer.App.Infrastructure;

namespace FocusTrackingTimer.App;

public partial class ThemedMessageDialog : Window
{
    private readonly AppMessageBoxLayout _layout;
    private readonly MessageBoxResult _defaultResult;
    private MessageBoxResult _result;

    public ThemedMessageDialog(
        string windowTitle,
        string messageText,
        MessageBoxButton button,
        MessageBoxImage icon,
        AppMessageBoxLayout layout)
    {
        if (button is not MessageBoxButton.OK and not MessageBoxButton.OKCancel)
        {
            throw new NotSupportedException($"Unsupported message box button: {button}");
        }

        InitializeComponent();

        _layout = layout;
        WindowTitle = string.IsNullOrWhiteSpace(windowTitle) ? "알림" : windowTitle;
        DialogTitle = WindowTitle;
        MessageText = messageText ?? string.Empty;
        AccentBrush = ResolveAccentBrush(icon);
        CancelButtonVisibility = button == MessageBoxButton.OKCancel
            ? Visibility.Visible
            : Visibility.Collapsed;
        _defaultResult = button == MessageBoxButton.OK
            ? MessageBoxResult.OK
            : MessageBoxResult.Cancel;
        _result = _defaultResult;

        DataContext = this;
    }

    public string WindowTitle { get; }

    public string DialogTitle { get; }

    public string MessageText { get; }

    public Brush AccentBrush { get; }

    public Visibility CancelButtonVisibility { get; }

    public Visibility AccentVisibility => _layout == AppMessageBoxLayout.Themed
        ? Visibility.Visible
        : Visibility.Collapsed;

    public GridLength AccentRowHeight => _layout == AppMessageBoxLayout.Themed
        ? new GridLength(4)
        : new GridLength(0);

    public Visibility TitleVisibility => _layout == AppMessageBoxLayout.Themed
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Thickness ContentMargin => _layout == AppMessageBoxLayout.Themed
        ? new Thickness(18, 16, 18, 0)
        : new Thickness(0);

    public Thickness WindowMargin => _layout == AppMessageBoxLayout.Themed
        ? new Thickness(18)
        : new Thickness(18, 18, 8.5, 8.5);

    public Thickness ButtonPanelMargin => _layout == AppMessageBoxLayout.Themed
        ? new Thickness(18)
        : new Thickness(0, 18, 0, 0);

    public double MessageFontSize => _layout == AppMessageBoxLayout.Themed
        ? 14d
        : 14d;

    public Brush ContainerBackgroundBrush => _layout == AppMessageBoxLayout.Themed
        ? ThemeBrushes.Surface
        : ThemeBrushes.Transparent;

    public Brush ContainerBorderBrush => _layout == AppMessageBoxLayout.Themed
        ? ThemeBrushes.SecondaryBorder
        : ThemeBrushes.Transparent;

    public Thickness ContainerBorderThickness => _layout == AppMessageBoxLayout.Themed
        ? new Thickness(1)
        : new Thickness(0);

    public CornerRadius ContainerCornerRadius => _layout == AppMessageBoxLayout.Themed
        ? new CornerRadius(8)
        : new CornerRadius(0);

    public MessageBoxResult ShowModal()
    {
        _ = ShowDialog();
        return _result;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        _result = MessageBoxResult.OK;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _result = MessageBoxResult.Cancel;
        DialogResult = false;
    }

    private static Brush ResolveAccentBrush(MessageBoxImage icon)
    {
        return icon switch
        {
            MessageBoxImage.Warning or MessageBoxImage.Error => ThemeBrushes.Sunday,
            MessageBoxImage.Question => ThemeBrushes.Status,
            _ => ThemeBrushes.Focus
        };
    }

    public enum AppMessageBoxLayout
    {
        Themed,
        Plain
    }
}
