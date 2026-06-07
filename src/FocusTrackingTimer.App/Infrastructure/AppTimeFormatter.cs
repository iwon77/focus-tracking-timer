using System.Globalization;

namespace FocusTrackingTimer.App.Infrastructure;

internal static class AppTimeFormatter
{
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        int hours = (int)duration.TotalHours;
        return $"{hours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    public static string FormatDurationShort(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        int hours = (int)duration.TotalHours;
        return hours > 0 ? $"{hours}h {duration.Minutes}m" : $"{duration.Minutes}m";
    }

    public static string FormatDateTime(DateTimeOffset value)
    {
        return value.LocalDateTime.ToString("MM/dd HH:mm:ss", CultureInfo.CurrentCulture);
    }

    public static string FormatRecordMonth(DateOnly value)
    {
        return value.ToDateTime(TimeOnly.MinValue).ToString("yyyy년 M월", CultureInfo.CurrentCulture);
    }

    public static string FormatCalendarHoverTitle(DateOnly date)
    {
        return date.ToDateTime(TimeOnly.MinValue).ToString("yyyy년 M월 d일", CultureInfo.CurrentCulture);
    }
}
