using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Checks whether one normalized v2 profile can use one already-resolved canonical map.</summary>
internal static class CompositionProfileMapAdmissionValidator
{
    private const string ProfileFamilyIdMismatch = "profile.v2.map.profile-family-id-mismatch";
    private const string ProfileFamilyVersionMismatch = "profile.v2.map.profile-family-version-mismatch";
    private const string ProfileFamilyContentHashMismatch = "profile.v2.map.profile-family-content-hash-mismatch";
    private const string ResolvedFamilyIdMismatch = "profile.v2.map.resolved-family-id-mismatch";
    private const string ResolvedFamilyVersionMismatch = "profile.v2.map.resolved-family-version-mismatch";
    private const string ResolvedFamilyContentHashMismatch = "profile.v2.map.resolved-family-content-hash-mismatch";
    private const string ResolvedMapNotOwned = "profile.v2.map.resolved-map-not-owned";
    private const string MapNotAllowed = "profile.v2.map.map-not-allowed";
    private const string RequiredRegionMissing = "profile.v2.map.required-region-missing";
    private const string RequiredMetadataStructureMissing = "profile.v2.map.required-metadata-structure-missing";
    private const string MetadataTargetMissing = "profile.v2.map.metadata-target-missing";
    private const string RequiredCapabilityMissing = "profile.v2.map.required-capability-missing";
    private const string RequiredCapabilityAbsent = "profile.v2.map.required-capability-absent";
    private const string RequiredCapabilityUnknown = "profile.v2.map.required-capability-unknown";
    private const string RequiredCapabilityApplicabilityUnavailable = "profile.v2.map.required-capability-applicability-unavailable";
    private const string RequiredCapabilityAmbiguous = "profile.v2.map.required-capability-ambiguous";

    /// <summary>
    /// Validates map identity, effective physical facts, and canonical metadata declarations.
    /// This result neither executes inspection metadata nor grants execution authority.
    /// </summary>
    internal static IReadOnlyList<CompositionIssue> Validate(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(resolvedMap);

        CompositionProfileMapBinding binding = profile.MapBinding;
        var issues = new List<CompositionIssue>();

        bool familyAssociationIsValid = AddFamilyAssociationIssues(binding, family, resolvedMap, issues);
        if (!binding.MapIds.Contains(resolvedMap.ImageMap.MapId, StringComparer.Ordinal))
        {
            issues.Add(new CompositionIssue(
                MapNotAllowed,
                $"Resolved map '{resolvedMap.ImageMap.MapId}' is not declared by the profile map binding."));
        }

        var resolvedRegionIds = resolvedMap.ImageMap.Regions
            .Select(static region => region.RegionId)
            .ToHashSet(StringComparer.Ordinal);
        AddMissingIssues(
            binding.RequiredRegionIds,
            resolvedRegionIds,
            RequiredRegionMissing,
            "region",
            issues);

        var declaredMetadataStructureIds = binding.RequiredMetadataStructureIds
            .Where(structureId => IsMetadataStructureDeclared(
                family,
                resolvedMap,
                structureId))
            .ToHashSet(StringComparer.Ordinal);
        AddMissingIssues(
            binding.RequiredMetadataStructureIds,
            declaredMetadataStructureIds,
            RequiredMetadataStructureMissing,
            "metadata structure",
            issues);
        AddMetadataTargetIssues(profile, family, resolvedMap, issues);

        CompiledCapabilityAdmission[] capabilities = familyAssociationIsValid
            ? ResolveRequiredCapabilities(binding, family, resolvedMap, issues)
            : [];
        issues.Sort(static (left, right) =>
        {
            int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
            return code != 0
                ? code
                : StringComparer.Ordinal.Compare(left.Message, right.Message);
        });

        capabilityAdmissions = issues.Count == 0
            ? Array.AsReadOnly(capabilities)
            : [];
        return issues.AsReadOnly();
    }

    private static bool AddFamilyAssociationIssues(
        CompositionProfileMapBinding binding,
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        List<CompositionIssue> issues)
    {
        if (!StringComparer.Ordinal.Equals(binding.FamilyId, family.FamilyId))
        {
            issues.Add(new CompositionIssue(
                ProfileFamilyIdMismatch,
                "Profile and normalized family identify different firmware families."));
        }

        if (!StringComparer.Ordinal.Equals(binding.FamilyVersion, family.FamilyVersion))
        {
            issues.Add(new CompositionIssue(
                ProfileFamilyVersionMismatch,
                "Profile and normalized family identify different firmware family versions."));
        }

        if (!StringComparer.Ordinal.Equals(binding.FamilyContentHash, family.FamilyContentHash))
        {
            issues.Add(new CompositionIssue(
                ProfileFamilyContentHashMismatch,
                "Profile and normalized family identify different firmware family content hashes."));
        }

        if (!StringComparer.Ordinal.Equals(resolvedMap.FamilyId, family.FamilyId))
        {
            issues.Add(new CompositionIssue(
                ResolvedFamilyIdMismatch,
                "Resolved map and normalized family identify different firmware families."));
        }

        if (!StringComparer.Ordinal.Equals(resolvedMap.FamilyVersion, family.FamilyVersion))
        {
            issues.Add(new CompositionIssue(
                ResolvedFamilyVersionMismatch,
                "Resolved map and normalized family identify different firmware family versions."));
        }

        if (!StringComparer.Ordinal.Equals(resolvedMap.FamilyContentHash, family.FamilyContentHash))
        {
            issues.Add(new CompositionIssue(
                ResolvedFamilyContentHashMismatch,
                "Resolved map and normalized family identify different firmware family content hashes."));
        }

        if (!family.ImageMaps.Any(map => ReferenceEquals(map, resolvedMap.ImageMap)))
        {
            issues.Add(new CompositionIssue(
                ResolvedMapNotOwned,
                "Resolved map is not owned by the normalized family definition."));
        }

        return issues.Count == 0;
    }

    private static void AddMissingIssues(
        IEnumerable<string> requiredIds,
        HashSet<string> resolvedIds,
        string issueCode,
        string factDescription,
        List<CompositionIssue> issues)
    {
        foreach (string requiredId in requiredIds)
        {
            if (!resolvedIds.Contains(requiredId))
            {
                issues.Add(new CompositionIssue(
                    issueCode,
                    $"Required {factDescription} '{requiredId}' is unavailable from the resolved map."));
            }
        }
    }

    private static void AddMetadataTargetIssues(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        List<CompositionIssue> issues)
    {
        foreach (CompositionProfileMetadataBinding metadataBinding in profile.MetadataBindings)
        {
            if (!family.TryResolveStructure(
                    resolvedMap.ImageMap.MapId,
                    metadataBinding.StructureId,
                    out FirmwareMetadataStructure? structure))
            {
                continue;
            }

            foreach (FirmwareMetadataReferenceTarget target in
                     metadataBinding.TargetReferences)
            {
                if (!structure.Definition.ContainsReferenceTarget(target))
                {
                    issues.Add(new CompositionIssue(
                        MetadataTargetMissing,
                        $"Metadata binding '{metadataBinding.BindingId}' references unknown " +
                        $"{target.Kind} target '{target.TargetId}'."));
                }
            }
        }
    }

    private static CompiledCapabilityAdmission[] ResolveRequiredCapabilities(
        CompositionProfileMapBinding binding,
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        List<CompositionIssue> issues)
    {
        var admitted = new List<CompiledCapabilityAdmission>();
        foreach (string requiredCapabilityId in binding.RequiredCapabilityIds)
        {
            FirmwareMapFactBinding<FirmwareCapabilityFact>[] candidates =
            [
                .. family.CapabilityBindings.Where(candidate =>
                    StringComparer.Ordinal.Equals(candidate.EffectiveKey.MemberId, resolvedMap.MemberId) &&
                    StringComparer.Ordinal.Equals(candidate.EffectiveKey.MapId, resolvedMap.ImageMap.MapId) &&
                    StringComparer.Ordinal.Equals(candidate.Value.CapabilityId, requiredCapabilityId)),
            ];
            FirmwareMapFactBinding<FirmwareCapabilityFact>[] applicable =
            [
                .. candidates.Where(candidate =>
                    candidate.Applicability.Evaluate(resolvedMap) == FirmwareApplicabilityResult.Match),
            ];
            if (applicable.Length > 1)
            {
                issues.Add(new CompositionIssue(
                    RequiredCapabilityAmbiguous,
                    $"Required capability '{requiredCapabilityId}' has multiple applicable evidence bindings."));
                continue;
            }

            if (applicable.Length == 1)
            {
                FirmwareMapFactBinding<FirmwareCapabilityFact> capability = applicable[0];
                switch (capability.Value.State)
                {
                    case FirmwareCapabilityState.ConfirmedPresent:
                        admitted.Add(new CompiledCapabilityAdmission(requiredCapabilityId, capability));
                        break;
                    case FirmwareCapabilityState.ConfirmedAbsent:
                        issues.Add(new CompositionIssue(
                            RequiredCapabilityAbsent,
                            $"Required capability '{requiredCapabilityId}' is confirmed absent."));
                        break;
                    case FirmwareCapabilityState.Unknown:
                        issues.Add(new CompositionIssue(
                            RequiredCapabilityUnknown,
                            $"Required capability '{requiredCapabilityId}' has unknown evidence state."));
                        break;
                    default:
                        throw new InvalidOperationException("Unknown firmware capability state.");
                }

                continue;
            }

            if (candidates.Any(candidate =>
                candidate.Applicability.Evaluate(resolvedMap) == FirmwareApplicabilityResult.Pending))
            {
                issues.Add(new CompositionIssue(
                    RequiredCapabilityApplicabilityUnavailable,
                    $"Required capability '{requiredCapabilityId}' cannot be evaluated from the resolved-map selection."));
                continue;
            }

            issues.Add(new CompositionIssue(
                RequiredCapabilityMissing,
                $"Required capability '{requiredCapabilityId}' has no applicable evidence binding."));
        }

        return [.. admitted];
    }

    internal static bool IsMetadataStructureDeclared(
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        string structureId)
    {
        return family.TryResolveStructure(
            resolvedMap.ImageMap.MapId,
            structureId,
            out _);
    }
}
