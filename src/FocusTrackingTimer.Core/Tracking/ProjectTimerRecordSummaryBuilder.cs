namespace FocusTrackingTimer.Core.Tracking;

internal static class ProjectTimerRecordSummaryBuilder
{
    public static IReadOnlyList<ProjectTimerRecordSlice> BuildRecordSlices(
        IEnumerable<ProjectTimerRecord> records,
        DateOnly fromDate,
        DateOnly toDate)
    {
        ValidateDateRange(fromDate, toDate);

        List<ProjectTimerRecordSlice> slices = [];
        foreach (ProjectTimerRecord record in records)
        {
            if (TryBuildRecordSlice(record, fromDate, toDate, out ProjectTimerRecordSlice? slice))
            {
                slices.Add(slice!);
            }
        }

        return [.. slices
            .OrderByDescending(static slice => slice.StartedAt)
            .ThenByDescending(static slice => slice.EndedAt)];
    }

    public static IReadOnlyList<DailyDurationSummary> BuildDailyDurationSummaries(
        IEnumerable<ProjectTimerRecord> records,
        DateOnly fromDate,
        DateOnly toDate)
    {
        ValidateDateRange(fromDate, toDate);

        Dictionary<DateOnly, TimeSpan> totalsByDate = [];
        foreach (ProjectTimerRecord record in records)
        {
            foreach ((DateOnly date, TimeSpan duration) in SplitFocusDurationsByDate(record.FocusSegments, fromDate, toDate))
            {
                totalsByDate[date] = totalsByDate.GetValueOrDefault(date, TimeSpan.Zero) + duration;
            }
        }

        List<DailyDurationSummary> summaries = [];
        for (DateOnly date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            summaries.Add(new DailyDurationSummary(date, totalsByDate.GetValueOrDefault(date, TimeSpan.Zero)));
        }

        return summaries;
    }

    public static IReadOnlyList<DailyProjectDurationSummary> BuildDailyProjectDurationSummaries(
        IEnumerable<ProjectTimerRecord> records,
        DateOnly fromDate,
        DateOnly toDate)
    {
        ValidateDateRange(fromDate, toDate);

        Dictionary<(DateOnly Date, Guid ProjectId), TimeSpan> totals = [];
        Dictionary<Guid, string> projectNames = [];

        foreach (ProjectTimerRecord record in records)
        {
            projectNames[record.ProjectId] = record.ProjectName;

            foreach ((DateOnly date, TimeSpan duration) in SplitFocusDurationsByDate(record.FocusSegments, fromDate, toDate))
            {
                (DateOnly Date, Guid ProjectId) key = (date, record.ProjectId);
                totals[key] = totals.GetValueOrDefault(key, TimeSpan.Zero) + duration;
            }
        }

        return [.. totals
            .Where(static pair => pair.Value > TimeSpan.Zero)
            .OrderBy(static pair => pair.Key.Date)
            .ThenByDescending(static pair => pair.Value)
            .ThenBy(pair => projectNames[pair.Key.ProjectId], StringComparer.CurrentCultureIgnoreCase)
            .Select(pair => new DailyProjectDurationSummary(
                pair.Key.Date,
                pair.Key.ProjectId,
                projectNames[pair.Key.ProjectId],
                pair.Value))];
    }

    private static void ValidateDateRange(DateOnly fromDate, DateOnly toDate)
    {
        if (toDate < fromDate)
        {
            throw new ArgumentOutOfRangeException(nameof(toDate), "The end date must be on or after the start date.");
        }
    }

    private static bool TryBuildRecordSlice(
        ProjectTimerRecord record,
        DateOnly fromDate,
        DateOnly toDate,
        out ProjectTimerRecordSlice? slice)
    {
        DateTimeOffset rangeStart = GetLocalBoundary(fromDate);
        DateTimeOffset rangeEnd = GetLocalBoundary(toDate.AddDays(1));
        IEnumerable<ProjectWorkSegment> workSegments = record.UsesWorkSegments
            ? record.WorkSegments
            : [new ProjectWorkSegment(record.StartedAt, record.EndedAt)];
        List<ProjectWorkSegment> slicedWorkSegments = [.. SliceWorkSegments(workSegments, rangeStart, rangeEnd)];
        if (slicedWorkSegments.Count == 0)
        {
            slice = null;
            return false;
        }

        DateTimeOffset sliceStart = slicedWorkSegments[0].StartedAt;
        DateTimeOffset sliceEnd = slicedWorkSegments[^1].EndedAt;
        List<ProgramFocusSegment> slicedSegments = [.. SliceFocusSegments(record.FocusSegments, rangeStart, rangeEnd)];
        slice = new ProjectTimerRecordSlice(
            record.ProjectId,
            record.ProjectName,
            sliceStart,
            sliceEnd,
            slicedWorkSegments,
            slicedSegments);
        return true;
    }

    private static IEnumerable<ProjectWorkSegment> SliceWorkSegments(
        IEnumerable<ProjectWorkSegment> workSegments,
        DateTimeOffset sliceStart,
        DateTimeOffset sliceEnd)
    {
        foreach (ProjectWorkSegment segment in workSegments)
        {
            DateTimeOffset overlappedStart = segment.StartedAt > sliceStart ? segment.StartedAt : sliceStart;
            DateTimeOffset overlappedEnd = segment.EndedAt < sliceEnd ? segment.EndedAt : sliceEnd;

            if (overlappedEnd <= overlappedStart)
            {
                continue;
            }

            yield return new ProjectWorkSegment(overlappedStart, overlappedEnd);
        }
    }

    private static IEnumerable<ProgramFocusSegment> SliceFocusSegments(
        IEnumerable<ProgramFocusSegment> focusSegments,
        DateTimeOffset sliceStart,
        DateTimeOffset sliceEnd)
    {
        foreach (ProgramFocusSegment segment in focusSegments)
        {
            DateTimeOffset overlappedStart = segment.StartedAt > sliceStart ? segment.StartedAt : sliceStart;
            DateTimeOffset overlappedEnd = segment.EndedAt < sliceEnd ? segment.EndedAt : sliceEnd;

            if (overlappedEnd <= overlappedStart)
            {
                continue;
            }

            yield return new ProgramFocusSegment(segment.Program, overlappedStart, overlappedEnd);
        }
    }

    private static IEnumerable<(DateOnly Date, TimeSpan Duration)> SplitFocusDurationsByDate(
        IEnumerable<ProgramFocusSegment> focusSegments,
        DateOnly fromDate,
        DateOnly toDate)
    {
        foreach (ProgramFocusSegment segment in focusSegments)
        {
            if (segment.FocusDuration <= TimeSpan.Zero)
            {
                continue;
            }

            DateTimeOffset current = segment.StartedAt;
            while (current < segment.EndedAt)
            {
                DateOnly currentDate = DateOnly.FromDateTime(current.LocalDateTime.Date);
                DateTime nextMidnightLocal = current.LocalDateTime.Date.AddDays(1);
                DateTimeOffset nextBoundary = new(nextMidnightLocal, TimeZoneInfo.Local.GetUtcOffset(nextMidnightLocal));
                DateTimeOffset sliceEnd = nextBoundary < segment.EndedAt ? nextBoundary : segment.EndedAt;

                if (currentDate >= fromDate && currentDate <= toDate)
                {
                    yield return (currentDate, sliceEnd - current);
                }

                current = sliceEnd;
            }
        }
    }

    private static DateTimeOffset GetLocalBoundary(DateOnly date)
    {
        DateTime localDateTime = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(localDateTime, TimeZoneInfo.Local.GetUtcOffset(localDateTime));
    }
}
