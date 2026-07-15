namespace NvtFwCombiner.Domain.Composition;

/// <summary>One exact byte postcondition that a staged external processor must satisfy before import.</summary>
public sealed class ExternalProcessorOutputAssertion
{
    private readonly byte[] _expectedBytes;

    /// <summary>Creates an immutable exact-byte assertion over the staged target image.</summary>
    public ExternalProcessorOutputAssertion(ByteRange range, IReadOnlyList<byte> expectedBytes)
    {
        ArgumentNullException.ThrowIfNull(expectedBytes);
        _ = expectedBytes.Count == range.Length
            ? true
            : throw new ArgumentException(
                "External processor output assertion bytes must match the declared range length.",
                nameof(expectedBytes));

        Range = range;
        _expectedBytes = [.. expectedBytes];
    }

    /// <summary>Absolute target-image range whose post-transform bytes are required.</summary>
    public ByteRange Range { get; }

    /// <summary>Expected immutable bytes for <see cref="Range"/>.</summary>
    public ReadOnlyMemory<byte> ExpectedBytes => _expectedBytes;
}
