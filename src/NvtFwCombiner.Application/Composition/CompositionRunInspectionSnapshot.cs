namespace NvtFwCombiner.Application.Composition;

/// <summary>
/// Immutable in-memory Replace result bytes used by bounded inspection surfaces.
/// This snapshot is not part of the serialized composition report.
/// </summary>
public sealed class CompositionRunInspectionSnapshot
{
    internal CompositionRunInspectionSnapshot(
        string runId,
        string outputSpaceId,
        string referenceSpaceId,
        string outputSha256,
        byte[] immutableReferenceBytes,
        ReadOnlyMemory<byte> immutableOutputBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputSpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceSpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputSha256);
        ArgumentNullException.ThrowIfNull(immutableReferenceBytes);
        if (immutableReferenceBytes.Length != immutableOutputBytes.Length)
        {
            throw new ArgumentException(
                "Replace inspection bytes must have equal lengths.",
                nameof(immutableReferenceBytes));
        }

        RunId = runId;
        OutputSpaceId = outputSpaceId;
        ReferenceSpaceId = referenceSpaceId;
        OutputSha256 = outputSha256;
        ReferenceBytes = immutableReferenceBytes;
        OutputBytes = immutableOutputBytes;
    }

    /// <summary>Authoritative run that produced these inspection bytes.</summary>
    public string RunId { get; }

    /// <summary>Compiled mutable address space represented by output offsets.</summary>
    public string OutputSpaceId { get; }

    /// <summary>Canonical immutable address space that initialized the selected output.</summary>
    public string ReferenceSpaceId { get; }

    /// <summary>SHA-256 of the authoritative final output, copied from the run report.</summary>
    public string OutputSha256 { get; }

    /// <summary>Reference image bytes captured during the authoritative run.</summary>
    public ReadOnlyMemory<byte> ReferenceBytes { get; }

    /// <summary>Final output bytes from that same authoritative run.</summary>
    public ReadOnlyMemory<byte> OutputBytes { get; }
}
