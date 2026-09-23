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
            IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> capabilityAdmissions,
            SourceEnvelopeExtent? sourceEnvelope)
        {
            (BundleIdentity, ProfileEntry, ResolvedMap, CapabilityAdmissions) =
                (bundleIdentity, profileEntry, resolvedMap, capabilityAdmissions);
            SourceEnvelope = sourceEnvelope;
        }

        internal ProfileBundleIdentity BundleIdentity { get; }
        internal TrustedCompositionProfileCatalogEntry ProfileEntry { get; }
        internal FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap ResolvedMap { get; }
        internal IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> CapabilityAdmissions { get; }
        internal SourceEnvelopeExtent? SourceEnvelope { get; }

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
            SourceEnvelopeExtent? envelope = null;
            SourceEnvelopeProfileBinding? binding = selectedProfile.Profile.Header.SourceEnvelopeBinding;
            FirmwareArtifactPayload? boundSource = binding is null
                ? null
                : resolutionInputs.Artifacts.SingleOrDefault(artifact =>
                    StringComparer.Ordinal.Equals(artifact.ArtifactId, binding.SourceSlotId));
            bool hasExactProfileMap = selectedProfile.Family.Family.ImageMaps.Any(map =>
                profileMapIds.Contains(map.MapId) &&
                map.Applicability.MemberIds.Contains(resolutionInputs.MemberId, StringComparer.Ordinal) &&
                map.Applicability.ModeIds.Contains(resolutionInputs.ModeId, StringComparer.Ordinal) &&
                map.CapacityBytes == resolutionInputs.CapacityBytes);
            if (binding is { AllowsAbsentSource: false } && boundSource is null &&
                !hasExactProfileMap)
            {
                issues = [new CompositionIssue(
                    "profile.v2.source-envelope.source-missing",
                    "The profile requires one captured complete DP source before source-envelope compilation.")];
                return false;
            }

            bool hasNonstandardBoundSource = binding is not null &&
                boundSource is not null &&
                boundSource.LengthBytes == resolutionInputs.CapacityBytes &&
                !hasExactProfileMap;
            if (hasNonstandardBoundSource)
            {
                mapResolution = selectedProfile.Family.Family.ResolveLayoutTemplateWithinForProfile(
                    resolutionInputs,
                    binding!.LayoutTemplateMapId,
                    profileMapIds,
                    requiredMetadataStructureIds);
                if (mapResolution.Status == FirmwareMapResolutionStatus.Unique)
                {
                    FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap template = mapResolution.ResolvedMap!;
                    FirmwareRegion? root = template.ImageMap.Regions.SingleOrDefault(region =>
                        StringComparer.Ordinal.Equals(region.RegionId, binding.RootRegionId));
                    if (root is null || root.ParentRegionId is not null ||
                        root.Range.Start != 0 || root.Range.EndExclusive != template.CapacityBytes)
                    {
                        issues = [new CompositionIssue(
                            "profile.v2.source-envelope.root-invalid",
                            "The declared layout template must contain the exact full-container root.")];
                        return false;
                    }

                    envelope = new SourceEnvelopeExtent(
                        binding.SourceSlotId,
                        binding.RootRegionId,
                        binding.LayoutTemplateMapId,
                        template.CapacityBytes,
                        boundSource!.LengthBytes,
                        binding.ExpectedOuterLengths,
                        binding.UnexpectedLengthIssueCode);
                }
            }
            else
            {
                mapResolution = selectedProfile.Family.Family.ResolveMapWithinForProfile(
                    resolutionInputs,
                    profileMapIds,
                    requiredMetadataStructureIds);
            }
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
                admittedCapabilities,
                envelope);
            return true;
        }
    }
}
