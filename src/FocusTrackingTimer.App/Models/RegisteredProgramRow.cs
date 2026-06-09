using FocusTrackingTimer.App.Infrastructure;

namespace FocusTrackingTimer.App;

public sealed class RegisteredProgramRow : ObservableObject
{
    private string _focusDurationText;
    private string _statusBrush;
    private string _statusText;

    public RegisteredProgramRow(
        string DisplayName,
        string ProcessName,
        string FocusDurationText,
        string RegisteredAtText = "",
        string InitialDisplayName = "",
        string StatusBrush = "#A7A7A0",
        string StatusText = "?ㅽ뻾 以??꾨떂",
        bool IsPinned = false,
        string PinButtonText = "怨좎젙",
        bool ShowsPinnedDivider = false)
    {
        this.DisplayName = DisplayName;
        this.ProcessName = ProcessName;
        _focusDurationText = FocusDurationText;
        this.RegisteredAtText = RegisteredAtText;
        this.InitialDisplayName = InitialDisplayName;
        _statusBrush = StatusBrush;
        _statusText = StatusText;
        this.IsPinned = IsPinned;
        this.PinButtonText = PinButtonText;
        this.ShowsPinnedDivider = ShowsPinnedDivider;
    }

    public string DisplayName { get; }

    public string ProcessName { get; }

    public string FocusDurationText
    {
        get => _focusDurationText;
        set => SetProperty(ref _focusDurationText, value);
    }

    public string RegisteredAtText { get; }

    public string InitialDisplayName { get; }

    public string StatusBrush
    {
        get => _statusBrush;
        set => SetProperty(ref _statusBrush, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsPinned { get; }

    public string PinButtonText { get; }

    public bool ShowsPinnedDivider { get; }

    public string PinIconVisibility => IsPinned ? "Visible" : "Collapsed";

    public string PinnedDividerVisibility => ShowsPinnedDivider ? "Visible" : "Collapsed";
}
