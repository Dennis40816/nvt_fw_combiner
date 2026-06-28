namespace NvtFwCombiner.Application.Ports;

/// <summary>Provides deterministic time to application use cases.</summary>
public interface ISystemClock
{
    /// <summary>Returns the current UTC timestamp.</summary>
    DateTimeOffset UtcNow { get; }
}
