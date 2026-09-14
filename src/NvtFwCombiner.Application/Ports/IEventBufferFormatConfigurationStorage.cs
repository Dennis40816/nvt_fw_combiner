using NvtFwCombiner.Contracts.Configuration;

namespace NvtFwCombiner.Application.Ports;

/// <summary>Bounded schema-validated storage at a host-selected fixed path; no semantic fallback.</summary>
public interface IEventBufferFormatConfigurationStorage
{
    /// <summary>Returns null only for a missing file. Other invalid or unreadable input throws.</summary>
    ValueTask<EventBufferFormatStoredConfiguration?> ReadAsync(CancellationToken cancellationToken);

    /// <summary>Atomically persists a document and returns SHA-256 of the committed bytes.</summary>
    ValueTask<string> WriteAsync(EventBufferFormatConfigurationDocument document, CancellationToken cancellationToken);
}

/// <summary>A transport document with SHA-256 of its complete original source bytes.</summary>
public sealed record EventBufferFormatStoredConfiguration(
    EventBufferFormatConfigurationDocument Document,
    string SourceSha256);

/// <summary>Stored or proposed bytes do not satisfy the finite configuration format.</summary>
public sealed class EventBufferFormatConfigurationFormatException : IOException
{
    /// <summary>Creates a format failure without exposing input content.</summary>
    public EventBufferFormatConfigurationFormatException(Exception? innerException = null)
        : base("Invalid Event Buffer Format configuration document.", innerException)
    {
    }
}
