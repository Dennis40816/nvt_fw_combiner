using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Profiles-owned coordinator for exact trusted selection, canonical map resolution, and map admission.</summary>
internal static class V2CompositionPreparationService
{
    private const string SelectionStale = "profile.v2.selection.stale";

    /// <summary>Resolves and admits one exact catalog entry without mirroring the Domain map outcome.</summary>
    internal static bool TryPrepare(
        TrustedProfileBundleCatalog catalog,
        TrustedCompositionProfileCatalogEntry selectedProfile,
        FirmwareMapResolutionInputs resolutionInputs,
        [NotNullWhen(true)] out FirmwareMapResolutionResult? mapResolution,
        out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(selectedProfile);
        ArgumentNullException.ThrowIfNull(resolutionInputs);
        mapResolution = null;
        capabilityAdmissions = [];
        issues = [];
        if (!catalog.OwnsProfile(selectedProfile))
        {
            issues =
            [
                new CompositionIssue(
                    SelectionStale,
                    "The selected trusted profile no longer belongs to this catalog."),
            ];
            return false;
        }

        var profileMapIds = selectedProfile.Profile.MapBinding.MapIds.ToHashSet(StringComparer.Ordinal);
        var deferredInspectionStructureIds = selectedProfile.Profile.MetadataBindings
            .Select(static binding => binding.StructureId)
            .ToHashSet(StringComparer.Ordinal);
        var requiredMetadataStructureIds =
            selectedProfile.Profile.MapBinding.RequiredMetadataStructureIds
                .Where(structureId => !deferredInspectionStructureIds.Contains(structureId))
                .ToHashSet(StringComparer.Ordinal);
        mapResolution = selectedProfile.Family.Family.ResolveMapWithinForProfile(
            resolutionInputs,
            profileMapIds,
            requiredMetadataStructureIds);
        if (mapResolution.Status != FirmwareMapResolutionStatus.Unique)
        {
            return false;
        }

        issues = selectedProfile.Family.Family.AdmitRequiredCapabilities(
            selectedProfile.Profile.MapBinding,
            mapResolution.ResolvedMap!,
            out IReadOnlyList<CompiledCapabilityAdmission> admittedCapabilities);
        if (issues.Count != 0)
        {
            return false;
        }

        capabilityAdmissions = admittedCapabilities;
        return true;
    }
}
