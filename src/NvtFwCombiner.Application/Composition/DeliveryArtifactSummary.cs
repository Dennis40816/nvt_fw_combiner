using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Report-safe evidence for an additional artifact delivered from one completed composition output.</summary>
public sealed class DeliveryArtifactSummary(
    string deliveryKind,
    string fileName,
    long size,
    string sha256,
    bool committed,
    ByteRange sourceRange)
{
    /// <summary>Closed delivery role, such as a profile-declared A-bank FlashCode.</summary>
    public string DeliveryKind { get; } = CompositionSummaryValue.NotBlank(
        deliveryKind,
        nameof(deliveryKind));

    /// <summary>Selected file name without a host path.</summary>
    public string FileName { get; } = CompositionSummaryValue.NotBlank(fileName, nameof(fileName));

    /// <summary>Delivered artifact length in bytes.</summary>
    public long Size { get; } = CompositionSummaryValue.NonNegative(size, nameof(size));

    /// <summary>Lowercase SHA-256 hash of the delivered byte sequence.</summary>
    public string Sha256 { get; } = CompositionSummaryValue.NotBlank(sha256, nameof(sha256));

    /// <summary>True only after the additional artifact was atomically committed.</summary>
    public bool Committed { get; } = committed;

    /// <summary>Half-open range copied from the primary composition output.</summary>
    public ByteRange SourceRange { get; } = sourceRange;
}
