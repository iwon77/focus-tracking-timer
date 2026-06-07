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

    public static string FormatCalendarDate(DateOnly date)
    {
        return date.ToDateTime(TimeOnly.MinValue).ToString("yyyy년 M월 d일", CultureInfo.CurrentCulture);
    }

    public static string FormatWeekRange(DateOnly weekStart, DateOnly weekEnd)
    {
        string startText = weekStart.ToDateTime(TimeOnly.MinValue).ToString("M월 d일", CultureInfo.CurrentCulture);
        string endText = weekStart.Month == weekEnd.Month
            ? weekEnd.ToDateTime(TimeOnly.MinValue).ToString("d일", CultureInfo.CurrentCulture)
            : weekEnd.ToDateTime(TimeOnly.MinValue).ToString("M월 d일", CultureInfo.CurrentCulture);

        return $"{startText} - {endText}";
    }

    public static string FormatWeekOfMonthLabel(DateOnly date)
    {
        int weekOfMonth = ((date.Day - 1) / 7) + 1;
        return $"{date.Year}년 {date.Month}월 {weekOfMonth}번째 주";
    }

    public static string FormatDayLabel(DateOnly date)
    {
        string dayOfWeek = date.ToDateTime(TimeOnly.MinValue).ToString("ddd", CultureInfo.CurrentCulture);
        return $"{date.Month}/{date.Day} ({dayOfWeek})";
    }

    public static string FormatGroupDate(DateOnly date)
    {
        return date.ToDateTime(TimeOnly.MinValue).ToString("M월 d일 (ddd)", CultureInfo.CurrentCulture);
    }

    public static string FormatWeekDayName(DateOnly date)
    {
        string fullDayName = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(date.DayOfWeek);
        return fullDayName.Length > 0 ? fullDayName[..1] : string.Empty;
    }

    public static string FormatWeeklyBubbleDate(DateOnly date)
    {
        return $"{date.Month}/{date.Day}";
    }

    public static string FormatTimeRange(DateTimeOffset startedAt, DateTimeOffset endedAt)
    {
        return $"{startedAt.LocalDateTime:HH:mm} - {endedAt.LocalDateTime:HH:mm}";
    }

    public static string FormatPercentage(double ratio)
    {
        if (double.IsNaN(ratio) || double.IsInfinity(ratio) || ratio < 0)
        {
            ratio = 0;
        }

        return $"{Math.Round(ratio * 100):0}%";
    }
}
