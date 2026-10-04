namespace NvtFwCombiner.Domain.Composition;

/// <summary>Provides immutable bytes in one source-local address space without exposing storage or identity policy.</summary>
public interface ICompositionByteSource
{
    /// <summary>Complete non-negative length of the source-local address space.</summary>
    long Length { get; }

    /// <summary>
    /// Fills the caller's bounded buffer from the checked half-open source range
    /// [offset, offset + destination.Length). Empty reads through EOF are valid;
    /// negative or out-of-source ranges are rejected, and range overflow throws
    /// <see cref="OverflowException"/>. Reads after the owner's lifetime ends fail.
    /// </summary>
    ValueTask ReadExactlyAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken);
}
