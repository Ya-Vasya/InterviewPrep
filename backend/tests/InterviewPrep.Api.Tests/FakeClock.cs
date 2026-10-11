namespace InterviewPrep.Api.Tests;

/// <summary>A clock that only moves when a test says so.</summary>
public sealed class FakeClock : TimeProvider
{
    public static readonly DateTimeOffset Start = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _now = Start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;

    public void Set(DateTimeOffset now) => _now = now;

    public void Reset() => _now = Start;
}
