using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Actual atomically promoted bundle delivery retained by the run report.</summary>
public sealed class CompositionOutputBundleDeliverySummary
{
    internal CompositionOutputBundleDeliverySummary(
        string resolvedDirectory,
        IReadOnlyList<CompositionOutputBundleDeliveredArtifactSummary> artifacts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedDirectory);
        ArgumentNullException.ThrowIfNull(artifacts);
        ResolvedDirectory = resolvedDirectory;
        Artifacts = Array.AsReadOnly([.. artifacts]);
    }

    /// <summary>Actual suffix-resolved directory promoted by the host.</summary>
    public string ResolvedDirectory { get; }

    /// <summary>Actual output and source filenames in canonical delivery order.</summary>
    public IReadOnlyList<CompositionOutputBundleDeliveredArtifactSummary> Artifacts { get; }

    internal static CompositionOutputBundleDeliverySummary FromReceipt(
        CompositionOutputBundleCommitReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return new CompositionOutputBundleDeliverySummary(
            receipt.ResolvedDirectory,
            [
                .. receipt.Artifacts.Select(static artifact =>
                    new CompositionOutputBundleDeliveredArtifactSummary(
                        artifact.Role,
                        artifact.BindingId,
                        artifact.DeliveredFileName,
                        artifact.Size,
                        artifact.Sha256)),
            ]);
    }
}

/// <summary>One actual file delivered inside an atomic output bundle.</summary>
public sealed record CompositionOutputBundleDeliveredArtifactSummary(
    string Role,
    string? BindingId,
    string DeliveredFileName,
    long Size,
    string Sha256);
