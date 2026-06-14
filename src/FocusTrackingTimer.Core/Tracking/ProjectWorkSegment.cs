namespace FocusTrackingTimer.Core.Tracking;

public sealed record ProjectWorkSegment
{
    public ProjectWorkSegment(DateTimeOffset startedAt, DateTimeOffset endedAt)
    {
        if (endedAt < startedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(endedAt), "End time must be later than start time.");
        }

        StartedAt = startedAt;
        EndedAt = endedAt;
    }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset EndedAt { get; }

    public TimeSpan Duration => EndedAt - StartedAt;
}
