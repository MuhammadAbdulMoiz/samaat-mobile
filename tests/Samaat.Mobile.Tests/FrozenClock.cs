namespace Samaat.Mobile.Tests;

/// <summary>
/// A clock for renders that must come out identical on every run: the wall time stands still and the simulator's
/// battery drain (its 2 s tick) never fires, while every other timer (command latency, timeouts, retries) runs for
/// real so pairing and moves still complete. The pairing code stays random; compare those frames by eye.
/// </summary>
internal sealed class FrozenClock : TimeProvider
{
    /// <summary>The simulator's tick in <c>SimulatedMobile.Create</c>.</summary>
    private static readonly TimeSpan SimulatorTick = TimeSpan.FromSeconds(2);

    private static readonly DateTimeOffset Instant = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    public static FrozenClock Instance { get; } = new();

    public override DateTimeOffset GetUtcNow() => Instant;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        period == SimulatorTick ? System.CreateTimer(_ => { }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan)
            : System.CreateTimer(callback, state, dueTime, period);
}
