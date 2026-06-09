using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using FocusTrackingTimer.App.Infrastructure;

namespace FocusTrackingTimer.App.Features.Performance;

internal sealed class PerformanceMonitorViewModel : ObservableObject
{
    private PerformanceTrendMetricOption? _selectedTrendMetricOption;
    private PerformanceTrendRangeOption? _selectedTrendRangeOption;
    private PointCollection _trendPoints = new();
    private string _realtimeUpdatedAtText = "-";
    private string _trendWindowText = string.Empty;
    private string _trendSummaryText = string.Empty;
    private string _trendMinText = string.Empty;
    private string _trendMaxText = string.Empty;
    private string _trendLatestText = string.Empty;
    private string _trendPointCountText = string.Empty;
    private string _trendEmptyText = "No trend data available yet.";
    private Visibility _trendChartVisibility = Visibility.Collapsed;
    private Visibility _trendEmptyVisibility = Visibility.Visible;

    public ObservableCollection<PerformanceMetricRowViewModel> RealtimeRows { get; } = new();

    public ObservableCollection<PerformanceTrendMetricOption> TrendMetricOptions { get; } = new();

    public ObservableCollection<PerformanceTrendRangeOption> TrendRangeOptions { get; } = new();

    public PerformanceTrendMetricOption? SelectedTrendMetricOption
    {
        get => _selectedTrendMetricOption;
        set => SetProperty(ref _selectedTrendMetricOption, value);
    }

    public PerformanceTrendRangeOption? SelectedTrendRangeOption
    {
        get => _selectedTrendRangeOption;
        set => SetProperty(ref _selectedTrendRangeOption, value);
    }

    public PointCollection TrendPoints
    {
        get => _trendPoints;
        set => SetProperty(ref _trendPoints, value);
    }

    public string RealtimeUpdatedAtText
    {
        get => _realtimeUpdatedAtText;
        set => SetProperty(ref _realtimeUpdatedAtText, value);
    }

    public string TrendWindowText
    {
        get => _trendWindowText;
        set => SetProperty(ref _trendWindowText, value);
    }

    public string TrendSummaryText
    {
        get => _trendSummaryText;
        set => SetProperty(ref _trendSummaryText, value);
    }

    public string TrendMinText
    {
        get => _trendMinText;
        set => SetProperty(ref _trendMinText, value);
    }

    public string TrendMaxText
    {
        get => _trendMaxText;
        set => SetProperty(ref _trendMaxText, value);
    }

    public string TrendLatestText
    {
        get => _trendLatestText;
        set => SetProperty(ref _trendLatestText, value);
    }

    public string TrendPointCountText
    {
        get => _trendPointCountText;
        set => SetProperty(ref _trendPointCountText, value);
    }

    public string TrendEmptyText
    {
        get => _trendEmptyText;
        set => SetProperty(ref _trendEmptyText, value);
    }

    public Visibility TrendChartVisibility
    {
        get => _trendChartVisibility;
        set => SetProperty(ref _trendChartVisibility, value);
    }

    public Visibility TrendEmptyVisibility
    {
        get => _trendEmptyVisibility;
        set => SetProperty(ref _trendEmptyVisibility, value);
    }
}

internal sealed class PerformanceMetricRowViewModel
{
    public PerformanceMetricRowViewModel(string label, string description, string value)
    {
        Label = label;
        Description = description;
        Value = value;
    }

    public string Label { get; }

    public string Description { get; }

    public string Value { get; }
}

internal sealed record PerformanceTrendMetricOption(
    PerformanceTrendMetricKey Key,
    string Label);

internal sealed record PerformanceTrendRangeOption(
    PerformanceTrendRangeKey Key,
    string Label);

internal enum PerformanceTrendRangeKey
{
    LastHour,
    LastSixHours,
    LastTwentyFourHours,
    Today
}
