using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>One additional artifact committed from a completed primary composition output.</summary>
public sealed record CompositionDeliveryArtifact(
    string DeliveryKind,
    string OutputPath,
    string OutputFileName,
    long OutputSize,
    ByteRange SourceRange,
    string Sha256);
