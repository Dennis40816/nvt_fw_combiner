using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.TestSupport;

/// <summary>Deterministic clock that returns caller-provided UTC timestamps in sequence.</summary>
public sealed class FakeClock : ISystemClock
{
    private readonly Queue<DateTimeOffset> _timestamps;

    /// <summary>Creates a clock over the supplied timestamp sequence.</summary>
    public FakeClock(IEnumerable<DateTimeOffset> timestamps)
    {
        ArgumentNullException.ThrowIfNull(timestamps);

        _timestamps = new Queue<DateTimeOffset>(timestamps);
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow => _timestamps.Dequeue();
}
