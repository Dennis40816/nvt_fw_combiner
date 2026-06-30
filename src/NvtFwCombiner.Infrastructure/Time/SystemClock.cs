using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Time;

/// <summary>Application clock backed by the operating system UTC timestamp.</summary>
public sealed class SystemClock : ISystemClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
