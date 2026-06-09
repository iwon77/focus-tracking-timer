using System.Globalization;

namespace FocusTrackingTimer.App.Features.Performance;

internal static class PerformanceMetricFormatter
{
    public static string FormatPercent(double value)
    {
        return $"{Math.Max(0, value):0.0} %";
    }

    public static string FormatMilliseconds(double value)
    {
        return $"{Math.Max(0, value):0.0} ms";
    }

    public static string FormatCount(double value)
    {
        return Math.Round(Math.Max(0, value)).ToString("N0", CultureInfo.CurrentCulture);
    }

    public static string FormatCount(long value)
    {
        return Math.Max(0, value).ToString("N0", CultureInfo.CurrentCulture);
    }

    public static string FormatBytes(double value)
    {
        return FormatBytes(Convert.ToInt64(Math.Max(0, value)));
    }

    public static string FormatBytes(long value)
    {
        long normalizedValue = Math.Max(0, value);
        const double kiloByte = 1024d;
        const double megaByte = kiloByte * 1024d;
        const double gigaByte = megaByte * 1024d;

        if (normalizedValue >= gigaByte)
        {
            return $"{normalizedValue / gigaByte:0.0} GB";
        }

        if (normalizedValue >= megaByte)
        {
            return $"{normalizedValue / megaByte:0.0} MB";
        }

        if (normalizedValue >= kiloByte)
        {
            return $"{normalizedValue / kiloByte:0.0} KB";
        }

        return $"{normalizedValue} B";
    }

    public static string FormatTrendValue(PerformanceTrendMetricKey metricKey, double value)
    {
        return metricKey switch
        {
            PerformanceTrendMetricKey.CpuUsage
                => FormatPercent(value),
            PerformanceTrendMetricKey.TickP95 or
            PerformanceTrendMetricKey.TickMax or
            PerformanceTrendMetricKey.ScanP95 or
            PerformanceTrendMetricKey.ScanMax or
            PerformanceTrendMetricKey.SaveP95 or
            PerformanceTrendMetricKey.SaveMax
                => FormatMilliseconds(value),
            PerformanceTrendMetricKey.WorkingSet or
            PerformanceTrendMetricKey.PrivateMemory or
            PerformanceTrendMetricKey.DatabaseFileSize
                => FormatBytes(value),
            PerformanceTrendMetricKey.DelayedTickCount or
            PerformanceTrendMetricKey.ScanExceptionCount or
            PerformanceTrendMetricKey.HandleCount or
            PerformanceTrendMetricKey.FocusSegmentsCount
                => FormatCount(value),
            _ => value.ToString("0.0", CultureInfo.CurrentCulture)
        };
    }

    public static string FormatRange(DateTimeOffset fromInclusive, DateTimeOffset toInclusive)
    {
        return $"{fromInclusive.LocalDateTime:yyyy-MM-dd HH:mm} - {toInclusive.LocalDateTime:yyyy-MM-dd HH:mm}";
    }
}
