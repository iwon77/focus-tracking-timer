using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using FocusTrackingTimer.App.ViewModels;

namespace FocusTrackingTimer.App.Views;

public partial class WeeklyRecordView : UserControl
{
    private ScrollViewer? _listScrollViewer;
    private WeeklyRecordViewModel? _observedViewModel;
    private bool _restoreScheduled;
    private double _pendingVerticalOffset;

    public WeeklyRecordView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private MainWindow HostWindow => (MainWindow)Window.GetWindow(this)!;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _listScrollViewer ??= FindDescendant<ScrollViewer>(WeeklyRecordListControl);
        AttachViewModel(DataContext as WeeklyRecordViewModel);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel(null);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        AttachViewModel(e.NewValue as WeeklyRecordViewModel);
    }

    private void PreviousWeekButton_Click(object sender, RoutedEventArgs e) => HostWindow.PreviousWeekButton_Click(sender, e);

    private void CurrentWeekButton_Click(object sender, RoutedEventArgs e) => HostWindow.CurrentWeekButton_Click(sender, e);

    private void NextWeekButton_Click(object sender, RoutedEventArgs e) => HostWindow.NextWeekButton_Click(sender, e);

    private void WeeklyRecordFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => HostWindow.WeeklyRecordFilter_SelectionChanged(sender, e);

    private void WeeklyRecordList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        HostWindow.WeeklyRecordList_SelectionChanged(sender, e);
    }

    private void WeeklySummaryDayButton_Click(object sender, RoutedEventArgs e)
    {
        HostWindow.WeeklySummaryDayButton_Click(sender, e);
        ScrollSelectedItemIntoView();
    }

    private void AttachViewModel(WeeklyRecordViewModel? viewModel)
    {
        if (ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        if (_observedViewModel is not null)
        {
            _observedViewModel.WeeklyRecordRows.CollectionChanged -= WeeklyRecordRows_CollectionChanged;
        }

        _observedViewModel = viewModel;

        if (_observedViewModel is not null)
        {
            ConfigureGrouping(_observedViewModel);
            _observedViewModel.WeeklyRecordRows.CollectionChanged += WeeklyRecordRows_CollectionChanged;
        }
    }

    private void WeeklyRecordRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        CaptureScrollOffset();
        ScheduleScrollRestore();
    }

    private static void ConfigureGrouping(WeeklyRecordViewModel viewModel)
    {
        ICollectionView groupedView = CollectionViewSource.GetDefaultView(viewModel.WeeklyRecordRows);
        groupedView.GroupDescriptions.Clear();
        groupedView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(WeeklyRecordRow.GroupDateText)));
    }

    private void CaptureScrollOffset()
    {
        _listScrollViewer ??= FindDescendant<ScrollViewer>(WeeklyRecordListControl);
        _pendingVerticalOffset = _listScrollViewer?.VerticalOffset ?? 0;
    }

    private void ScheduleScrollRestore()
    {
        if (_restoreScheduled)
        {
            return;
        }

        _restoreScheduled = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _restoreScheduled = false;
            _listScrollViewer ??= FindDescendant<ScrollViewer>(WeeklyRecordListControl);
            _listScrollViewer?.ScrollToVerticalOffset(_pendingVerticalOffset);
        }));
    }

    private void ScrollSelectedItemIntoView()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (WeeklyRecordListControl.SelectedItem is not null)
            {
                WeeklyRecordListControl.ScrollIntoView(WeeklyRecordListControl.SelectedItem);
            }
        }));
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T matched)
            {
                return matched;
            }

            T? nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
