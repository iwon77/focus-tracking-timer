using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace FocusTrackingTimer.App.Features.Performance;

internal sealed partial class PerformanceMonitorWindow : Window
{
    private const double ChartWidth = 760d;
    private const double ChartHeight = 220d;

    private readonly PerformanceMonitorService _service;
    private readonly PerformanceMonitorViewModel _viewModel = new();
    private readonly DispatcherTimer _refreshTimer;

    public PerformanceMonitorWindow(PerformanceMonitorService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));

        InitializeComponent();
        DataContext = _viewModel;

        _viewModel.TrendMetricOptions.Add(new PerformanceTrendMetricOption(PerformanceTrendMetricKey.CpuUsage, "CPU usage"));
        _viewModel.TrendMetricOptions.Add(new PerformanceTrendMetricOption(PerformanceTrendMetricKey.TickP95, "Tick p95"));
        _viewModel.TrendMetricOptions.Add(new PerformanceTrendMetricOption(PerformanceTrendMetricKey.TickMax, "Tick max"));
        _viewModel.TrendMetricOptions.Add(new PerformanceTrendMetricOption(PerformanceTrendMetricKey.DelayedTickCount, "Delayed ticks"));
        _viewModel.TrendMetricOptions.Add(new PerformanceTrendMetricOption(PerformanceTrendMetricKey.ScanP95, "Scan p95"));
        _viewModel.TrendMetricOptions.Add(new PerformanceTrendMetricOption(PerformanceTrendMetricKey.ScanMax, "Scan max"));
        _viewModel.TrendMetricOptions.Add(new PerformanceTrendMetricOption(PerformanceTrendMetricKey.ScanExceptionCount, "Scan exceptions"));
        _viewModel.TrendMetricOptions.Add(new PerformanceTrendMetricOption(PerformanceTrendMetricKey.SaveP95, "Save p95"));
        _viewModel.TrendMetricOptions.Add(new PerformanceTrendMetricOption(PerformanceTrendMetricKey.SaveMax, "Save max"));
        _viewModel.TrendMetricOptions.Add(new PerformanceTrendMetricOption(PerformanceTrendMetricKey.WorkingSet, "Memory usage"));
        _viewModel.TrendMetricOptions.Add(new PerformanceTrendMetricOption(PerformanceTrendMetricKey.PrivateMemory, "Private Memory"));
        _viewModel.TrendMetricOptions.Add(new PerformanceTrendMetricOption(PerformanceTrendMetricKey.HandleCount, "Handle Count"));
        _viewModel.TrendMetricOptions.Add(new PerformanceTrendMetricOption(PerformanceTrendMetricKey.FocusSegmentsCount, "focus_segments"));
        _viewModel.TrendMetricOptions.Add(new PerformanceTrendMetricOption(PerformanceTrendMetricKey.DatabaseFileSize, "DB file size"));

        _viewModel.TrendRangeOptions.Add(new PerformanceTrendRangeOption(PerformanceTrendRangeKey.LastHour, "Last hour"));
        _viewModel.TrendRangeOptions.Add(new PerformanceTrendRangeOption(PerformanceTrendRangeKey.LastSixHours, "Last 6 hours"));
        _viewModel.TrendRangeOptions.Add(new PerformanceTrendRangeOption(PerformanceTrendRangeKey.LastTwentyFourHours, "Last 24 hours"));
        _viewModel.TrendRangeOptions.Add(new PerformanceTrendRangeOption(PerformanceTrendRangeKey.Today, "Today"));

        _viewModel.SelectedTrendMetricOption = _viewModel.TrendMetricOptions[0];
        _viewModel.SelectedTrendRangeOption = _viewModel.TrendRangeOptions[0];

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;

        Loaded += PerformanceMonitorWindow_Loaded;
        Closed += PerformanceMonitorWindow_Closed;
    }

    private void PerformanceMonitorWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshRealtime();
        RefreshTrend();
        _refreshTimer.Start();
    }

    private void PerformanceMonitorWindow_Closed(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        RefreshRealtime();
    }

    private void RefreshRealtime()
    {
        PerformanceRealtimeSnapshot snapshot = _service.GetRealtimeSnapshot();
        _viewModel.RealtimeUpdatedAtText = $"Updated: {snapshot.UpdatedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}";

        _viewModel.RealtimeRows.Clear();
        _viewModel.RealtimeRows.Add(new PerformanceMetricRowViewModel(
            "CPU usage",
            "Current process CPU usage averaged since the previous sample.",
            PerformanceMetricFormatter.FormatPercent(snapshot.CpuUsagePercent)));
        _viewModel.RealtimeRows.Add(new PerformanceMetricRowViewModel(
            "Tick p95",
            "95 percent of recent UI ticks finished within this time.",
            PerformanceMetricFormatter.FormatMilliseconds(snapshot.TickP95Ms)));
        _viewModel.RealtimeRows.Add(new PerformanceMetricRowViewModel(
            "Tick max",
            "Longest single UI tick observed in the recent window.",
            PerformanceMetricFormatter.FormatMilliseconds(snapshot.TickMaxMs)));
        _viewModel.RealtimeRows.Add(new PerformanceMetricRowViewModel(
            "Delayed ticks",
            "Count of 1-second ticks delayed beyond the expected interval.",
            PerformanceMetricFormatter.FormatCount(snapshot.DelayedTickCount)));
        _viewModel.RealtimeRows.Add(new PerformanceMetricRowViewModel(
            "Scan p95",
            "95 percent of process scans finished within this time.",
            PerformanceMetricFormatter.FormatMilliseconds(snapshot.ScanP95Ms)));
        _viewModel.RealtimeRows.Add(new PerformanceMetricRowViewModel(
            "Scan max",
            "Longest single process scan observed in the recent window.",
            PerformanceMetricFormatter.FormatMilliseconds(snapshot.ScanMaxMs)));
        _viewModel.RealtimeRows.Add(new PerformanceMetricRowViewModel(
            "Scan exceptions",
            "Exceptions raised while reading process information.",
            PerformanceMetricFormatter.FormatCount(snapshot.ScanExceptionCount)));
        _viewModel.RealtimeRows.Add(new PerformanceMetricRowViewModel(
            "Save p95",
            "95 percent of catalog save and record append operations finished within this time.",
            PerformanceMetricFormatter.FormatMilliseconds(snapshot.SaveP95Ms)));
        _viewModel.RealtimeRows.Add(new PerformanceMetricRowViewModel(
            "Save max",
            "Longest single save operation observed in the recent window.",
            PerformanceMetricFormatter.FormatMilliseconds(snapshot.SaveMaxMs)));
        _viewModel.RealtimeRows.Add(new PerformanceMetricRowViewModel(
            "Memory usage",
            "Current working set currently resident in physical memory.",
            PerformanceMetricFormatter.FormatBytes(snapshot.WorkingSetBytes)));
        _viewModel.RealtimeRows.Add(new PerformanceMetricRowViewModel(
            "Private Memory",
            "Process-private committed memory currently held by the app.",
            PerformanceMetricFormatter.FormatBytes(snapshot.PrivateMemoryBytes)));
        _viewModel.RealtimeRows.Add(new PerformanceMetricRowViewModel(
            "Handle Count",
            "Current number of OS handles owned by the process.",
            PerformanceMetricFormatter.FormatCount(snapshot.HandleCount)));
        _viewModel.RealtimeRows.Add(new PerformanceMetricRowViewModel(
            "focus_segments",
            "Total number of persisted focus segments.",
            PerformanceMetricFormatter.FormatCount(snapshot.FocusSegmentsCount)));
        _viewModel.RealtimeRows.Add(new PerformanceMetricRowViewModel(
            "DB file size",
            "Current size of the SQLite database file.",
            PerformanceMetricFormatter.FormatBytes(snapshot.DatabaseFileBytes)));
    }

    private void RefreshTrend()
    {
        if (_viewModel.SelectedTrendMetricOption is null || _viewModel.SelectedTrendRangeOption is null)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        (DateTimeOffset fromInclusive, DateTimeOffset toInclusive) = ResolveRange(_viewModel.SelectedTrendRangeOption.Key, now);
        PerformanceTrendSeries trend = _service.LoadTrend(_viewModel.SelectedTrendMetricOption.Key, fromInclusive, toInclusive);

        _viewModel.TrendWindowText = PerformanceMetricFormatter.FormatRange(fromInclusive, toInclusive);

        if (trend.Points.Count == 0)
        {
            _viewModel.TrendPoints = new PointCollection();
            _viewModel.TrendSummaryText = $"{_viewModel.SelectedTrendMetricOption.Label} trend";
            _viewModel.TrendMinText = string.Empty;
            _viewModel.TrendMaxText = string.Empty;
            _viewModel.TrendLatestText = string.Empty;
            _viewModel.TrendPointCountText = string.Empty;
            _viewModel.TrendChartVisibility = Visibility.Collapsed;
            _viewModel.TrendEmptyVisibility = Visibility.Visible;
            return;
        }

        double minValue = trend.Points.Min(static point => point.Value);
        double maxValue = trend.Points.Max(static point => point.Value);
        double latestValue = trend.Points[^1].Value;

        _viewModel.TrendPoints = BuildPointCollection(trend.Points, minValue, maxValue);
        _viewModel.TrendSummaryText = $"{_viewModel.SelectedTrendMetricOption.Label} trend";
        _viewModel.TrendMinText = $"Min {PerformanceMetricFormatter.FormatTrendValue(trend.MetricKey, minValue)}";
        _viewModel.TrendMaxText = $"Max {PerformanceMetricFormatter.FormatTrendValue(trend.MetricKey, maxValue)}";
        _viewModel.TrendLatestText = $"Latest {PerformanceMetricFormatter.FormatTrendValue(trend.MetricKey, latestValue)}";
        _viewModel.TrendPointCountText = $"Samples {trend.Points.Count}";
        _viewModel.TrendChartVisibility = Visibility.Visible;
        _viewModel.TrendEmptyVisibility = Visibility.Collapsed;
    }

    private static (DateTimeOffset FromInclusive, DateTimeOffset ToInclusive) ResolveRange(
        PerformanceTrendRangeKey rangeKey,
        DateTimeOffset now)
    {
        return rangeKey switch
        {
            PerformanceTrendRangeKey.LastHour => (now.AddHours(-1), now),
            PerformanceTrendRangeKey.LastSixHours => (now.AddHours(-6), now),
            PerformanceTrendRangeKey.LastTwentyFourHours => (now.AddHours(-24), now),
            PerformanceTrendRangeKey.Today => (new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset), now),
            _ => (now.AddHours(-1), now)
        };
    }

    private static PointCollection BuildPointCollection(IReadOnlyList<PerformanceTrendPoint> points, double minValue, double maxValue)
    {
        PointCollection pointCollection = new();
        if (points.Count == 0)
        {
            return pointCollection;
        }

        double verticalRange = maxValue - minValue;

        for (int index = 0; index < points.Count; index++)
        {
            double x = points.Count == 1
                ? ChartWidth / 2d
                : index * (ChartWidth / (points.Count - 1d));
            double y = verticalRange <= 0.0001d
                ? ChartHeight / 2d
                : ChartHeight - ((points[index].Value - minValue) / verticalRange * ChartHeight);

            pointCollection.Add(new Point(x, y));
        }

        return pointCollection;
    }

    private void TrendSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        RefreshTrend();
    }
}
