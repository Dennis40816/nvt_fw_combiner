namespace NvtFwCombiner.Application.Composition;

/// <summary>
/// Immutable in-memory Replace result bytes used by bounded inspection surfaces.
/// This snapshot is not part of the serialized composition report.
/// </summary>
public sealed class CompositionRunInspectionSnapshot
{
    private readonly byte[] _referenceBytes;

    internal CompositionRunInspectionSnapshot(
        string referenceSpaceId,
        byte[] immutableReferenceBytes,
        byte[] immutableOutputBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceSpaceId);
        ArgumentNullException.ThrowIfNull(immutableReferenceBytes);
        ArgumentNullException.ThrowIfNull(immutableOutputBytes);
        if (immutableReferenceBytes.Length != immutableOutputBytes.Length)
        {
            throw new ArgumentException(
                "Replace inspection bytes must have equal lengths.",
                nameof(immutableReferenceBytes));
        }

        ReferenceSpaceId = referenceSpaceId;
        _referenceBytes = immutableReferenceBytes;
        OutputBytes = immutableOutputBytes;
    }

    /// <summary>Canonical immutable address space that initialized the selected output.</summary>
    public string ReferenceSpaceId { get; }

    /// <summary>Reference image bytes captured during the authoritative run.</summary>
    public ReadOnlyMemory<byte> ReferenceBytes => _referenceBytes;

    /// <summary>Final output bytes from that same authoritative run.</summary>
    public ReadOnlyMemory<byte> OutputBytes { get; }
}
