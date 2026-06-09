using System.Diagnostics;
using System.IO;
using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.App.Features.Performance;

internal sealed class PerformanceMonitorService
{
    private readonly object _sync = new();
    private readonly string _databasePath;
    private readonly PerformanceLogFileStore _logStore;
    private PerformanceMinuteAccumulator? _currentWindow;
    private long _persistedFocusSegmentCount;
    private DateTimeOffset? _lastCpuSampleObservedAt;
    private TimeSpan? _lastTotalProcessorTime;
    private double _lastCpuUsagePercent;

    public PerformanceMonitorService(string databasePath, string logDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path is required.", nameof(databasePath));
        }

        _databasePath = Path.GetFullPath(databasePath);
        _logStore = new PerformanceLogFileStore(logDirectoryPath);
    }

    public void Initialize(long persistedFocusSegmentCount)
    {
        lock (_sync)
        {
            _persistedFocusSegmentCount = Math.Max(0, persistedFocusSegmentCount);
        }
    }

    public void RecordUiTick(
        DateTimeOffset observedAt,
        TimeSpan tickDuration,
        bool isDelayedTick,
        TimeSpan? processScanDuration,
        int processScanExceptionCount)
    {
        lock (_sync)
        {
            PerformanceMinuteAccumulator window = GetOrRotateWindow(observedAt);
            window.RecordTick(tickDuration, isDelayedTick);

            if (processScanDuration.HasValue)
            {
                window.RecordProcessScan(processScanDuration.Value, processScanExceptionCount);
            }

            UpdateRuntimeSnapshot(window);
        }
    }

    public void RecordProjectCatalogSave(DateTimeOffset observedAt, TimeSpan duration)
    {
        lock (_sync)
        {
            PerformanceMinuteAccumulator window = GetOrRotateWindow(observedAt);
            window.RecordSave(duration);
            UpdateRuntimeSnapshot(window);
        }
    }

    public void RecordCompletedRecordSave(DateTimeOffset observedAt, ProjectTimerRecord record, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_sync)
        {
            PerformanceMinuteAccumulator window = GetOrRotateWindow(observedAt);
            window.RecordSave(duration);
            _persistedFocusSegmentCount += record.FocusSegments.Count;
            UpdateRuntimeSnapshot(window);
        }
    }

    public PerformanceRealtimeSnapshot GetRealtimeSnapshot()
    {
        lock (_sync)
        {
            PerformanceMinuteAccumulator window = _currentWindow ?? new PerformanceMinuteAccumulator(TruncateToMinute(DateTimeOffset.Now));
            UpdateRuntimeSnapshot(window);
            return window.BuildRealtimeSnapshot(_persistedFocusSegmentCount);
        }
    }

    public PerformanceTrendSeries LoadTrend(
        PerformanceTrendMetricKey metricKey,
        DateTimeOffset fromInclusive,
        DateTimeOffset toInclusive)
    {
        IReadOnlyList<PerformanceMinuteLogEntry> entries;

        try
        {
            entries = _logStore.Load(fromInclusive, toInclusive);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            entries = [];
        }

        List<PerformanceTrendPoint> points = [.. entries.Select(entry => new PerformanceTrendPoint(
            entry.WindowEndedAt,
            GetMetricValue(metricKey, entry)))];

        return new PerformanceTrendSeries(metricKey, fromInclusive, toInclusive, points);
    }

    public void FlushPending()
    {
        lock (_sync)
        {
            if (_currentWindow is null || !_currentWindow.HasData)
            {
                return;
            }

            FlushWindow(_currentWindow, DateTimeOffset.Now);
            _currentWindow = null;
        }
    }

    private PerformanceMinuteAccumulator GetOrRotateWindow(DateTimeOffset observedAt)
    {
        DateTimeOffset windowStartedAt = TruncateToMinute(observedAt);

        if (_currentWindow is null)
        {
            _currentWindow = new PerformanceMinuteAccumulator(windowStartedAt);
            return _currentWindow;
        }

        if (_currentWindow.WindowStartedAt != windowStartedAt)
        {
            FlushWindow(_currentWindow, windowStartedAt);
            _currentWindow = new PerformanceMinuteAccumulator(windowStartedAt);
        }

        return _currentWindow;
    }

    private void FlushWindow(PerformanceMinuteAccumulator window, DateTimeOffset windowEndedAt)
    {
        try
        {
            _logStore.Append(window.BuildLogEntry(
                windowEndedAt,
                _persistedFocusSegmentCount,
                GetDatabaseFileBytes()));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void UpdateRuntimeSnapshot(PerformanceMinuteAccumulator window)
    {
        try
        {
            using Process process = Process.GetCurrentProcess();
            process.Refresh();
            double cpuUsagePercent = CalculateCpuUsagePercent(process.TotalProcessorTime);
            window.UpdateRuntimeSnapshot(
                cpuUsagePercent,
                process.WorkingSet64,
                process.PrivateMemorySize64,
                process.HandleCount,
                GetDatabaseFileBytes());
        }
        catch
        {
            window.UpdateRuntimeSnapshot(
                _lastCpuUsagePercent,
                window.WorkingSetBytes,
                window.PrivateMemoryBytes,
                window.HandleCount,
                GetDatabaseFileBytes());
        }
    }

    private double CalculateCpuUsagePercent(TimeSpan totalProcessorTime)
    {
        DateTimeOffset observedAt = DateTimeOffset.Now;

        if (!_lastCpuSampleObservedAt.HasValue || !_lastTotalProcessorTime.HasValue)
        {
            _lastCpuSampleObservedAt = observedAt;
            _lastTotalProcessorTime = totalProcessorTime;
            _lastCpuUsagePercent = 0;
            return _lastCpuUsagePercent;
        }

        double elapsedMilliseconds = (observedAt - _lastCpuSampleObservedAt.Value).TotalMilliseconds;
        double processorMilliseconds = (totalProcessorTime - _lastTotalProcessorTime.Value).TotalMilliseconds;

        _lastCpuSampleObservedAt = observedAt;
        _lastTotalProcessorTime = totalProcessorTime;

        if (elapsedMilliseconds <= 0 || processorMilliseconds <= 0)
        {
            _lastCpuUsagePercent = 0;
            return _lastCpuUsagePercent;
        }

        double cpuUsagePercent = processorMilliseconds / (elapsedMilliseconds * Environment.ProcessorCount) * 100d;
        _lastCpuUsagePercent = Math.Clamp(cpuUsagePercent, 0d, 100d);
        return _lastCpuUsagePercent;
    }

    private long GetDatabaseFileBytes()
    {
        try
        {
            return File.Exists(_databasePath)
                ? new FileInfo(_databasePath).Length
                : 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static DateTimeOffset TruncateToMinute(DateTimeOffset value)
    {
        return new DateTimeOffset(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Offset);
    }

    private static double GetMetricValue(PerformanceTrendMetricKey metricKey, PerformanceMinuteLogEntry entry)
    {
        return metricKey switch
        {
            PerformanceTrendMetricKey.CpuUsage => entry.CpuUsagePercent,
            PerformanceTrendMetricKey.TickP95 => entry.TickP95Ms,
            PerformanceTrendMetricKey.TickMax => entry.TickMaxMs,
            PerformanceTrendMetricKey.DelayedTickCount => entry.DelayedTickCount,
            PerformanceTrendMetricKey.ScanP95 => entry.ScanP95Ms,
            PerformanceTrendMetricKey.ScanMax => entry.ScanMaxMs,
            PerformanceTrendMetricKey.ScanExceptionCount => entry.ScanExceptionCount,
            PerformanceTrendMetricKey.SaveP95 => entry.SaveP95Ms,
            PerformanceTrendMetricKey.SaveMax => entry.SaveMaxMs,
            PerformanceTrendMetricKey.WorkingSet => entry.WorkingSetBytes,
            PerformanceTrendMetricKey.PrivateMemory => entry.PrivateMemoryBytes,
            PerformanceTrendMetricKey.HandleCount => entry.HandleCount,
            PerformanceTrendMetricKey.FocusSegmentsCount => entry.FocusSegmentsCount,
            PerformanceTrendMetricKey.DatabaseFileSize => entry.DatabaseFileBytes,
            _ => 0
        };
    }

    private sealed class PerformanceMinuteAccumulator
    {
        private readonly List<double> _cpuSamples = [];
        private readonly List<double> _tickSamples = [];
        private readonly List<double> _scanSamples = [];
        private readonly List<double> _saveSamples = [];
        private int _delayedTickCount;
        private int _scanExceptionCount;

        public PerformanceMinuteAccumulator(DateTimeOffset windowStartedAt)
        {
            WindowStartedAt = windowStartedAt;
            LastUpdatedAt = windowStartedAt;
        }

        public DateTimeOffset WindowStartedAt { get; }

        public DateTimeOffset LastUpdatedAt { get; private set; }

        public double CpuUsagePercent { get; private set; }

        public long WorkingSetBytes { get; private set; }

        public long PrivateMemoryBytes { get; private set; }

        public int HandleCount { get; private set; }

        public long DatabaseFileBytes { get; private set; }

        public bool HasData => _tickSamples.Count > 0 || _scanSamples.Count > 0 || _saveSamples.Count > 0;

        public void RecordTick(TimeSpan tickDuration, bool isDelayedTick)
        {
            _tickSamples.Add(Math.Max(0, tickDuration.TotalMilliseconds));
            if (isDelayedTick)
            {
                _delayedTickCount++;
            }

            LastUpdatedAt = DateTimeOffset.Now;
        }

        public void RecordProcessScan(TimeSpan processScanDuration, int exceptionCount)
        {
            _scanSamples.Add(Math.Max(0, processScanDuration.TotalMilliseconds));
            _scanExceptionCount += Math.Max(0, exceptionCount);
            LastUpdatedAt = DateTimeOffset.Now;
        }

        public void RecordSave(TimeSpan saveDuration)
        {
            _saveSamples.Add(Math.Max(0, saveDuration.TotalMilliseconds));
            LastUpdatedAt = DateTimeOffset.Now;
        }

        public void UpdateRuntimeSnapshot(
            double cpuUsagePercent,
            long workingSetBytes,
            long privateMemoryBytes,
            int handleCount,
            long databaseFileBytes)
        {
            CpuUsagePercent = Math.Max(0, cpuUsagePercent);
            _cpuSamples.Add(CpuUsagePercent);
            WorkingSetBytes = Math.Max(0, workingSetBytes);
            PrivateMemoryBytes = Math.Max(0, privateMemoryBytes);
            HandleCount = Math.Max(0, handleCount);
            DatabaseFileBytes = Math.Max(0, databaseFileBytes);
            LastUpdatedAt = DateTimeOffset.Now;
        }

        public PerformanceRealtimeSnapshot BuildRealtimeSnapshot(long focusSegmentsCount)
        {
            return new PerformanceRealtimeSnapshot(
                WindowStartedAt,
                LastUpdatedAt,
                CpuUsagePercent,
                CalculateP95(_tickSamples),
                CalculateMax(_tickSamples),
                _delayedTickCount,
                CalculateP95(_scanSamples),
                CalculateMax(_scanSamples),
                _scanExceptionCount,
                CalculateP95(_saveSamples),
                CalculateMax(_saveSamples),
                WorkingSetBytes,
                PrivateMemoryBytes,
                HandleCount,
                Math.Max(0, focusSegmentsCount),
                DatabaseFileBytes);
        }

        public PerformanceMinuteLogEntry BuildLogEntry(
            DateTimeOffset windowEndedAt,
            long focusSegmentsCount,
            long databaseFileBytes)
        {
            return new PerformanceMinuteLogEntry(
                WindowStartedAt,
                windowEndedAt,
                CalculateAverage(_cpuSamples),
                CalculateP95(_tickSamples),
                CalculateMax(_tickSamples),
                _delayedTickCount,
                CalculateP95(_scanSamples),
                CalculateMax(_scanSamples),
                _scanExceptionCount,
                CalculateP95(_saveSamples),
                CalculateMax(_saveSamples),
                WorkingSetBytes,
                PrivateMemoryBytes,
                HandleCount,
                Math.Max(0, focusSegmentsCount),
                Math.Max(0, databaseFileBytes));
        }

        private static double CalculateAverage(List<double> values)
        {
            return values.Count == 0 ? 0 : values.Average();
        }

        private static double CalculateP95(List<double> values)
        {
            if (values.Count == 0)
            {
                return 0;
            }

            double[] ordered = [.. values.OrderBy(static value => value)];
            int index = Math.Max(0, (int)Math.Ceiling(ordered.Length * 0.95d) - 1);
            return ordered[index];
        }

        private static double CalculateMax(List<double> values)
        {
            return values.Count == 0 ? 0 : values.Max();
        }
    }
}

internal sealed record PerformanceRealtimeSnapshot(
    DateTimeOffset WindowStartedAt,
    DateTimeOffset UpdatedAt,
    double CpuUsagePercent,
    double TickP95Ms,
    double TickMaxMs,
    int DelayedTickCount,
    double ScanP95Ms,
    double ScanMaxMs,
    int ScanExceptionCount,
    double SaveP95Ms,
    double SaveMaxMs,
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    int HandleCount,
    long FocusSegmentsCount,
    long DatabaseFileBytes);

internal sealed record PerformanceTrendSeries(
    PerformanceTrendMetricKey MetricKey,
    DateTimeOffset FromInclusive,
    DateTimeOffset ToInclusive,
    IReadOnlyList<PerformanceTrendPoint> Points);

internal sealed record PerformanceTrendPoint(DateTimeOffset Timestamp, double Value);

internal enum PerformanceTrendMetricKey
{
    CpuUsage,
    TickP95,
    TickMax,
    DelayedTickCount,
    ScanP95,
    ScanMax,
    ScanExceptionCount,
    SaveP95,
    SaveMax,
    WorkingSet,
    PrivateMemory,
    HandleCount,
    FocusSegmentsCount,
    DatabaseFileSize
}
