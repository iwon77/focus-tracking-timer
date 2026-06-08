using System.Windows;

namespace FocusTrackingTimer.App;

public sealed record WeeklyDayBubbleRow(
    DateOnly Date,
    string DayOfWeekText,
    string DateText,
    double BubbleDiameter,
    bool HasBubble,
    bool IsSelected,
    bool IsSunday,
    Visibility BubbleVisibility);
