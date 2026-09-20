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
/// Application-owned metadata authority resolver. Fixed workflows consume
/// their compiled inspection batch. Generic discovery and a typed CtrlRAM
/// reference that needs read-only DPCMI display perform one exact,
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
            if (exactPlan is not null && DeclaresDpcmi(exactPlan))
            {
                return FirmwareMetadataPlanAuthority.Terminal(exactPlan);
            }

            // A CtrlRAM plan without DPCMI (including an empty compiled plan)
            // owns report classification, not the Base's read-only DP facts.
            // Resolve those through the same metadata-only port used before
            // any replacement is selected; this grants no DP write authority.
            return ResolveFullImage(icId, inputLength);
        }

        bool hasDistinctTpArtifact = !string.IsNullOrWhiteSpace(input.TpPath) &&
            !StringComparer.Ordinal.Equals(input.Path, input.TpPath);
        return hasDistinctTpArtifact
            ? ResolveGeneric(icId, ExperienceIds.StandardMerge, "selector-free", inputLength)
            : ResolveFullImage(icId, inputLength);
    }

    private FirmwareMetadataPlanAuthority ResolveFullImage(string icId, long inputLength)
    {
        MetadataPlanResolutionResult resolution = _catalog.ResolveFullImageMetadataPlan(
            IcIdentifier.Normalize(icId), inputLength);
        return FirmwareMetadataPlanAuthority.Terminal(resolution.MetadataPlan, resolution.Issue);
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

    private static bool DeclaresDpcmi(ResolvedMetadataPlan plan)
    {
        return plan.Entries.Any(entry => StringComparer.Ordinal.Equals(
            entry.Definition.StructureDefinition.Definition.DefinitionId,
            DpcmiMetadataContract.StructureId));
    }
}
