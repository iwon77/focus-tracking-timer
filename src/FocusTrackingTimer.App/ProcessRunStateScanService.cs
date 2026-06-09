namespace FocusTrackingTimer.App;

internal sealed class ProcessRunStateScanService
{
    private readonly object _sync = new();
    private readonly int _currentProcessId;
    private bool _isScanInFlight;
    private long _version;
    private ProcessRunStateScanSnapshot _latestSnapshot;

    public ProcessRunStateScanService(int currentProcessId)
    {
        _currentProcessId = currentProcessId;
        _latestSnapshot = new ProcessRunStateScanSnapshot(
            new Dictionary<string, ProcessRunState>(StringComparer.OrdinalIgnoreCase),
            TimeSpan.Zero,
            0,
            0);
    }

    public ProcessRunStateScanSnapshot GetLatestSnapshot()
    {
        lock (_sync)
        {
            return _latestSnapshot;
        }
    }

    public void RequestRefresh()
    {
        lock (_sync)
        {
            if (_isScanInFlight)
            {
                return;
            }

            _isScanInFlight = true;
        }

        _ = Task.Run(RunScan);
    }

    private void RunScan()
    {
        ProcessRunStateScanResult? result = null;

        try
        {
            result = RunningProcessCatalog.MeasureProcessRunStates(_currentProcessId);
        }
        catch
        {
        }
        finally
        {
            lock (_sync)
            {
                if (result is not null)
                {
                    _version++;
                    _latestSnapshot = new ProcessRunStateScanSnapshot(
                        result.ProcessStates,
                        result.Elapsed,
                        result.ExceptionCount,
                        _version);
                }

                _isScanInFlight = false;
            }
        }
    }
}

internal sealed record ProcessRunStateScanSnapshot(
    IReadOnlyDictionary<string, ProcessRunState> ProcessStates,
    TimeSpan Elapsed,
    int ExceptionCount,
    long Version);
