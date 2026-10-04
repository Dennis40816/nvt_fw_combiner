using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Ports;

/// <summary>
/// Owns one immutable, completely verified artifact until disposal. Application
/// compares its complete stamp with the accepted identity before admitting it;
/// Domain consumers receive only <see cref="ICompositionByteSource"/>.
/// </summary>
public abstract class ArtifactReadLease : ICompositionByteSource, IDisposable
{
    private int _disposed;

    /// <summary>Creates a lease for the complete verified content identity.</summary>
    protected ArtifactReadLease(FileStamp fileStamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileStamp.Sha256);
        FileStamp = fileStamp;
    }

    /// <summary>Complete length and SHA-256 of the sealed bytes; this is not an admission decision.</summary>
    public FileStamp FileStamp { get; }

    /// <inheritdoc />
    public long Length => FileStamp.AcceptedLength;

    /// <inheritdoc />
    public ValueTask ReadExactlyAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        long endExclusive = destination.IsEmpty ? offset : new ByteRange(offset, destination.Length).EndExclusive;
        return endExclusive > Length
            ? throw new ArgumentOutOfRangeException(nameof(offset), "The requested range exceeds the source length.")
            : destination.IsEmpty
            ? ValueTask.CompletedTask
            : ReadRangeAsync(offset, destination, cancellationToken);
    }

    /// <summary>Releases the owned content once; subsequent range reads fail, including empty reads.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            DisposeCore();
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>Reads exactly one already-checked, nonempty source-local range.</summary>
    protected abstract ValueTask ReadRangeAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken);

    /// <summary>Releases the adapter's owned storage; called once by disposal.</summary>
    protected abstract void DisposeCore();
}
