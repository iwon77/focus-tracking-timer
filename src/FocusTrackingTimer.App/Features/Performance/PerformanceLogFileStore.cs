using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace FocusTrackingTimer.App.Features.Performance;

internal sealed class PerformanceLogFileStore
{
    private const string FilePrefix = "performance-";
    private const string FileExtension = ".ndjson";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    private readonly string _directoryPath;
    private readonly int _retentionDays;

    public PerformanceLogFileStore(string directoryPath, int retentionDays = 7)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path is required.", nameof(directoryPath));
        }

        _directoryPath = Path.GetFullPath(directoryPath);
        _retentionDays = Math.Max(1, retentionDays);
    }

    public void Append(PerformanceMinuteLogEntry entry)
    {
        Directory.CreateDirectory(_directoryPath);

        string logLine = JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine;
        string filePath = BuildFilePath(DateOnly.FromDateTime(entry.WindowStartedAt.LocalDateTime.Date));
        File.AppendAllText(filePath, logLine, Encoding.UTF8);

        TrimOlderThan(DateOnly.FromDateTime(entry.WindowStartedAt.LocalDateTime.Date).AddDays(-(_retentionDays - 1)));
    }

    public IReadOnlyList<PerformanceMinuteLogEntry> Load(DateTimeOffset fromInclusive, DateTimeOffset toInclusive)
    {
        if (fromInclusive > toInclusive || !Directory.Exists(_directoryPath))
        {
            return [];
        }

        List<PerformanceMinuteLogEntry> entries = [];
        DateOnly fromDate = DateOnly.FromDateTime(fromInclusive.LocalDateTime.Date);
        DateOnly toDate = DateOnly.FromDateTime(toInclusive.LocalDateTime.Date);

        for (DateOnly date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            string filePath = BuildFilePath(date);
            if (!File.Exists(filePath))
            {
                continue;
            }

            foreach (string line in File.ReadLines(filePath, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    PerformanceMinuteLogEntry? entry = JsonSerializer.Deserialize<PerformanceMinuteLogEntry>(line, SerializerOptions);
                    if (entry is null ||
                        entry.WindowEndedAt < fromInclusive ||
                        entry.WindowStartedAt > toInclusive)
                    {
                        continue;
                    }

                    entries.Add(entry);
                }
                catch (JsonException)
                {
                    continue;
                }
            }
        }

        return [.. entries.OrderBy(static entry => entry.WindowStartedAt)];
    }

    private string BuildFilePath(DateOnly date)
    {
        return Path.Combine(_directoryPath, $"{FilePrefix}{date:yyyy-MM-dd}{FileExtension}");
    }

    private void TrimOlderThan(DateOnly earliestKeptDate)
    {
        if (!Directory.Exists(_directoryPath))
        {
            return;
        }

        foreach (string filePath in Directory.EnumerateFiles(_directoryPath, $"{FilePrefix}*{FileExtension}", SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            if (!fileName.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string dateText = fileName[FilePrefix.Length..];
            if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly fileDate))
            {
                continue;
            }

            if (fileDate < earliestKeptDate)
            {
                File.Delete(filePath);
            }
        }
    }
}

internal sealed record PerformanceMinuteLogEntry(
    DateTimeOffset WindowStartedAt,
    DateTimeOffset WindowEndedAt,
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
