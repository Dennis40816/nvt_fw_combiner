using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Report-safe evidence for an additional artifact delivered from one completed composition output.</summary>
public sealed class DeliveryArtifactSummary
{
    /// <summary>Creates one additional delivery artifact summary without recording a host path.</summary>
    public DeliveryArtifactSummary(
        string deliveryKind,
        string fileName,
        long size,
        string sha256,
        bool committed,
        ByteRange sourceRange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deliveryKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        DeliveryKind = deliveryKind;
        FileName = fileName;
        Size = size;
        Sha256 = sha256;
        Committed = committed;
        SourceRange = sourceRange;
    }

    /// <summary>Closed delivery role, such as a profile-declared A-bank FlashCode.</summary>
    public string DeliveryKind { get; }

    /// <summary>Selected file name without a host path.</summary>
    public string FileName { get; }

    /// <summary>Delivered artifact length in bytes.</summary>
    public long Size { get; }

    /// <summary>Lowercase SHA-256 hash of the delivered byte sequence.</summary>
    public string Sha256 { get; }

    /// <summary>True only after the additional artifact was atomically committed.</summary>
    public bool Committed { get; }

    /// <summary>Half-open range copied from the primary composition output.</summary>
    public ByteRange SourceRange { get; }
}
