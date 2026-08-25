using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>
/// Terminal Application decision for one firmware metadata projection.
/// A null plan with <see cref="IsApplicable"/> set remains a final absence.
/// </summary>
public sealed record FirmwareMetadataPlanAuthority(
    bool IsApplicable,
    ResolvedMetadataPlan? Plan,
    CapabilityCatalogIssue? Issue)
{
    /// <summary>Gets the terminal decision for inputs that do not expose DP metadata.</summary>
    public static FirmwareMetadataPlanAuthority NotApplicable { get; } =
        new(false, null, null);

    /// <summary>Creates an applicable terminal decision, including a valid absent plan.</summary>
    public static FirmwareMetadataPlanAuthority Terminal(
        ResolvedMetadataPlan? plan,
        CapabilityCatalogIssue? issue = null)
    {
        return new(true, plan, issue);
    }
}

/// <summary>Resolves one terminal metadata authority without granting execution authority.</summary>
public interface IFirmwareMetadataPlanAuthorityResolver
{
    /// <summary>Resolves one terminal metadata decision for the captured input batch.</summary>
    FirmwareMetadataPlanAuthority Resolve(
        string icId,
        FirmwareInspectionSnapshotInput input,
        long inputLength,
        FirmwareInspectionStatusBatch dpInputBatch,
        FirmwareInspectionStatusBatch standardMergeInputBatch,
        FirmwareInspectionStatusBatch ctrlRamInputBatch);
}

/// <summary>
/// Application-owned metadata authority resolver. Fixed workflows consume only
/// their compiled inspection batch; generic discovery performs one exact,
/// capacity-bound metadata-only catalog query.
/// </summary>
public sealed class FirmwareMetadataPlanAuthorityResolver(
    ICanonicalCapabilityQuery catalog) : IFirmwareMetadataPlanAuthorityResolver
{
    private readonly ICanonicalCapabilityQuery _catalog =
        catalog ?? throw new ArgumentNullException(nameof(catalog));

    /// <inheritdoc />
    public FirmwareMetadataPlanAuthority Resolve(
        string icId,
        FirmwareInspectionSnapshotInput input,
        long inputLength,
        FirmwareInspectionStatusBatch dpInputBatch,
        FirmwareInspectionStatusBatch standardMergeInputBatch,
        FirmwareInspectionStatusBatch ctrlRamInputBatch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegative(inputLength);
        ArgumentNullException.ThrowIfNull(dpInputBatch);
        ArgumentNullException.ThrowIfNull(standardMergeInputBatch);
        ArgumentNullException.ThrowIfNull(ctrlRamInputBatch);

        if (input.AbMergeAddressSpaceId is not null)
        {
            return FirmwareMetadataPlanAuthority.NotApplicable;
        }

        if (input.StandardMergeAddressSpaceId is not null)
        {
            return FirmwareMetadataPlanAuthority.Terminal(
                standardMergeInputBatch.ExactMetadataPlan);
        }

        if (input.DpReplaceAddressSpaceId is not null)
        {
            return FirmwareMetadataPlanAuthority.Terminal(
                dpInputBatch.ExactMetadataPlan);
        }

        if (input.CtrlRamReplaceAddressSpaceId is not null)
        {
            if (!StringComparer.Ordinal.Equals(
                    input.CtrlRamReplaceAddressSpaceId,
                    CompositionAddressSpaceIds.ReferenceBase))
            {
                return FirmwareMetadataPlanAuthority.NotApplicable;
            }

            ResolvedMetadataPlan? exactPlan = ctrlRamInputBatch.ExactMetadataPlan;
            if (exactPlan is not null)
            {
                return FirmwareMetadataPlanAuthority.Terminal(exactPlan);
            }

            bool isBaseOnlyDiscovery =
                ctrlRamInputBatch.CtrlRamBaseDiscovery is { } discovery &&
                StringComparer.Ordinal.Equals(discovery.InspectionId, input.InspectionId);
            return isBaseOnlyDiscovery
                ? ResolveGeneric(
                    icId,
                    ExperienceIds.DpReplace,
                    "1-ic",
                    inputLength)
                : FirmwareMetadataPlanAuthority.Terminal(plan: null);
        }

        bool hasDistinctTpArtifact = !string.IsNullOrWhiteSpace(input.TpPath) &&
            !StringComparer.Ordinal.Equals(input.Path, input.TpPath);
        return ResolveGeneric(
            icId,
            hasDistinctTpArtifact
                ? ExperienceIds.StandardMerge
                : ExperienceIds.DpReplace,
            hasDistinctTpArtifact ? "selector-free" : "1-ic",
            inputLength);
    }

    private FirmwareMetadataPlanAuthority ResolveGeneric(
        string icId,
        string workflowId,
        string icCountVariant,
        long inputLength)
    {
        MetadataPlanResolutionResult resolution =
            _catalog.ResolveUniqueMetadataPlan(
                IcIdentifier.Normalize(icId),
                workflowId,
                icCountVariant,
                inputLength);
        return FirmwareMetadataPlanAuthority.Terminal(
            resolution.MetadataPlan,
            resolution.Issue);
    }
}
