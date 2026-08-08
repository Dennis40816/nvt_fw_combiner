using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Profiles-owned coordinator for exact trusted selection, canonical map resolution, and map admission.</summary>
internal static class V2CompositionPreparationService
{
    private const string SelectionStale = "profile.v2.selection.stale";

    /// <summary>Unforgeable exact catalog selection, resolved map, and capability admission.</summary>
    internal sealed class PreparedCompilation
    {
        private PreparedCompilation(
            ProfileBundleIdentity bundleIdentity,
            TrustedCompositionProfileCatalogEntry profileEntry,
            FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
            IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> capabilityAdmissions)
        {
            (BundleIdentity, ProfileEntry, ResolvedMap, CapabilityAdmissions) =
            (bundleIdentity, profileEntry, resolvedMap, capabilityAdmissions);
        }

        internal ProfileBundleIdentity BundleIdentity { get; }
        internal TrustedCompositionProfileCatalogEntry ProfileEntry { get; }
        internal FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap ResolvedMap { get; }
        internal IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> CapabilityAdmissions { get; }

        internal static bool TryCreate(
            TrustedProfileBundleCatalog catalog,
            TrustedCompositionProfileCatalogEntry selectedProfile,
            FirmwareMapResolutionInputs resolutionInputs,
            [NotNullWhen(true)] out PreparedCompilation? preparation,
            out FirmwareMapResolutionResult? mapResolution,
            out IReadOnlyList<CompositionIssue> issues)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(selectedProfile);
            ArgumentNullException.ThrowIfNull(resolutionInputs);
            preparation = null;
            mapResolution = null;
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
                out IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> admittedCapabilities);
            if (issues.Count != 0)
            {
                return false;
            }

            preparation = new PreparedCompilation(
                catalog.BundleIdentity,
                selectedProfile,
                mapResolution.ResolvedMap!,
                admittedCapabilities);
            return true;
        }
    }
}
