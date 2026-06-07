namespace FocusTrackingTimer.App;

public sealed record CalendarDayRow(
    DateOnly? Date,
    string DayText,
    string DurationText,
    bool HasDuration,
    bool IsToday,
    bool IsPlaceholder);
