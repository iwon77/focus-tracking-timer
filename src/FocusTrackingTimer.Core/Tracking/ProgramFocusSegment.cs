namespace FocusTrackingTimer.Core.Tracking;

public sealed record ProgramFocusSegment
{
    public ProgramFocusSegment(TrackedApplication program, DateTimeOffset startedAt, DateTimeOffset endedAt)
    {
        ArgumentNullException.ThrowIfNull(program);

        if (endedAt < startedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(endedAt), "End time must be later than start time.");
        }

        Program = program;
        StartedAt = startedAt;
        EndedAt = endedAt;
    }

    public TrackedApplication Program { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset EndedAt { get; }

    public TimeSpan FocusDuration => EndedAt - StartedAt;
}
