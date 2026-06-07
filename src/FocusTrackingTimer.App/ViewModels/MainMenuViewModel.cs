using System.Windows;
using System.Windows.Media;
using FocusTrackingTimer.App.Infrastructure;

namespace FocusTrackingTimer.App.ViewModels;

public sealed class MainMenuViewModel : ObservableObject
{
    private static readonly Brush SelectedTabBackground = new SolidColorBrush(Color.FromRgb(31, 31, 31));
    private static readonly Brush SelectedTabForeground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
    private static readonly Brush UnselectedTabBackground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
    private static readonly Brush UnselectedTabForeground = new SolidColorBrush(Color.FromRgb(24, 24, 24));

    private MainMenuTab _selectedTab = MainMenuTab.Timer;
    private Brush _timerTabBackground = SelectedTabBackground;
    private Brush _timerTabForeground = SelectedTabForeground;
    private Brush _dailyRecordTabBackground = UnselectedTabBackground;
    private Brush _dailyRecordTabForeground = UnselectedTabForeground;
    private Brush _weeklyRecordTabBackground = UnselectedTabBackground;
    private Brush _weeklyRecordTabForeground = UnselectedTabForeground;
    private Visibility _timerViewVisibility = Visibility.Visible;
    private Visibility _dailyRecordViewVisibility = Visibility.Collapsed;
    private Visibility _weeklyRecordViewVisibility = Visibility.Collapsed;

    public MainMenuTab SelectedTab
    {
        get => _selectedTab;
        private set => SetProperty(ref _selectedTab, value);
    }

    public Brush TimerTabBackground
    {
        get => _timerTabBackground;
        private set => SetProperty(ref _timerTabBackground, value);
    }

    public Brush TimerTabForeground
    {
        get => _timerTabForeground;
        private set => SetProperty(ref _timerTabForeground, value);
    }

    public Brush DailyRecordTabBackground
    {
        get => _dailyRecordTabBackground;
        private set => SetProperty(ref _dailyRecordTabBackground, value);
    }

    public Brush DailyRecordTabForeground
    {
        get => _dailyRecordTabForeground;
        private set => SetProperty(ref _dailyRecordTabForeground, value);
    }

    public Brush WeeklyRecordTabBackground
    {
        get => _weeklyRecordTabBackground;
        private set => SetProperty(ref _weeklyRecordTabBackground, value);
    }

    public Brush WeeklyRecordTabForeground
    {
        get => _weeklyRecordTabForeground;
        private set => SetProperty(ref _weeklyRecordTabForeground, value);
    }

    public Visibility TimerViewVisibility
    {
        get => _timerViewVisibility;
        private set => SetProperty(ref _timerViewVisibility, value);
    }

    public Visibility DailyRecordViewVisibility
    {
        get => _dailyRecordViewVisibility;
        private set => SetProperty(ref _dailyRecordViewVisibility, value);
    }

    public Visibility WeeklyRecordViewVisibility
    {
        get => _weeklyRecordViewVisibility;
        private set => SetProperty(ref _weeklyRecordViewVisibility, value);
    }

    public void SelectTab(MainMenuTab tab)
    {
        SelectedTab = tab;

        bool isTimer = tab == MainMenuTab.Timer;
        bool isDailyRecord = tab == MainMenuTab.DailyRecord;
        bool isWeeklyRecord = tab == MainMenuTab.WeeklyRecord;

        TimerViewVisibility = isTimer ? Visibility.Visible : Visibility.Collapsed;
        DailyRecordViewVisibility = isDailyRecord ? Visibility.Visible : Visibility.Collapsed;
        WeeklyRecordViewVisibility = isWeeklyRecord ? Visibility.Visible : Visibility.Collapsed;

        TimerTabBackground = isTimer ? SelectedTabBackground : UnselectedTabBackground;
        TimerTabForeground = isTimer ? SelectedTabForeground : UnselectedTabForeground;
        DailyRecordTabBackground = isDailyRecord ? SelectedTabBackground : UnselectedTabBackground;
        DailyRecordTabForeground = isDailyRecord ? SelectedTabForeground : UnselectedTabForeground;
        WeeklyRecordTabBackground = isWeeklyRecord ? SelectedTabBackground : UnselectedTabBackground;
        WeeklyRecordTabForeground = isWeeklyRecord ? SelectedTabForeground : UnselectedTabForeground;
    }
}
