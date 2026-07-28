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
    internal static CompositionProfileMapAdmissionResult Validate(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
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

        if (familyAssociationIsValid)
        {
            AdmittedCapabilityEvidence[] capabilities = ResolveRequiredCapabilities(binding, family, resolvedMap, issues);
            return issues.Count == 0
                ? CompositionProfileMapAdmissionResult.Succeeded(
                    new CompositionProfileMapAdmission(profile, family, resolvedMap, capabilities))
                : CompositionProfileMapAdmissionResult.Failed(issues);
        }

        return CompositionProfileMapAdmissionResult.Failed(issues);
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

    private static AdmittedCapabilityEvidence[] ResolveRequiredCapabilities(
        CompositionProfileMapBinding binding,
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        List<CompositionIssue> issues)
    {
        var admitted = new List<AdmittedCapabilityEvidence>();
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
                        admitted.Add(new AdmittedCapabilityEvidence(requiredCapabilityId, capability));
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

/// <summary>Immutable, non-executable outcome of admitting a v2 profile to one resolved map.</summary>
internal sealed class CompositionProfileMapAdmissionResult
{
    private readonly CompositionIssue[] _issues;

    private CompositionProfileMapAdmissionResult(
        CompositionProfileMapAdmission? admission,
        IEnumerable<CompositionIssue> issues)
    {
        _issues = ImmutableReferenceSnapshot.Create(issues, "Map-admission issues cannot contain null.");

        Array.Sort(_issues, static (left, right) =>
        {
            int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
            return code != 0
            ? code
            : StringComparer.Ordinal.Compare(left.Message, right.Message);
        });
        if ((admission is null) != (_issues.Length != 0))
        {
            throw new ArgumentException("Map admission must contain either one admission or one or more issues.");
        }

        Admission = admission;
        Issues = Array.AsReadOnly(_issues);
    }

    /// <summary>Atomic non-executable context when every admission check passed.</summary>
    internal CompositionProfileMapAdmission? Admission { get; }

    /// <summary>True only when the profile's map requirements all match the selected resolved map.</summary>
    internal bool IsAdmitted => Admission is not null;

    /// <summary>Deterministically ordered admission blockers.</summary>
    internal IReadOnlyList<CompositionIssue> Issues { get; }

    internal static CompositionProfileMapAdmissionResult Succeeded(CompositionProfileMapAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        return new CompositionProfileMapAdmissionResult(admission, []);
    }

    internal static CompositionProfileMapAdmissionResult Failed(IEnumerable<CompositionIssue> issues)
    {
        return new CompositionProfileMapAdmissionResult(admission: null, issues);
    }
}

/// <summary>Atomic, non-executable future compiler input admitted against one normalized family and resolved map.</summary>
internal sealed class CompositionProfileMapAdmission
{
    private readonly AdmittedCapabilityEvidence[] _requiredCapabilities;

    internal CompositionProfileMapAdmission(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        IEnumerable<AdmittedCapabilityEvidence> requiredCapabilities)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(resolvedMap);
        _requiredCapabilities = ImmutableReferenceSnapshot.Create(
            requiredCapabilities,
            "Admitted capability evidence must be non-null and unique by required capability id.");
        if (_requiredCapabilities.Select(static capability => capability.RequiredCapabilityId)
                .Distinct(StringComparer.Ordinal).Count() != _requiredCapabilities.Length)
        {
            throw new ArgumentException(
                "Admitted capability evidence must be non-null and unique by required capability id.",
                nameof(requiredCapabilities));
        }

        Array.Sort(_requiredCapabilities, static (left, right) =>
            StringComparer.Ordinal.Compare(left.RequiredCapabilityId, right.RequiredCapabilityId));
        ValidateFamilyAndMap(profile, family, resolvedMap);
        ValidateCapabilityEvidence(profile, family, resolvedMap, _requiredCapabilities);
        Profile = profile;
        ResolvedMap = resolvedMap;
        RequiredCapabilities = Array.AsReadOnly(_requiredCapabilities);
    }

    /// <summary>Normalized profile whose map requirements were admitted.</summary>
    internal CompositionProfileDefinition Profile { get; }

    /// <summary>Exact resolver-produced physical map accepted for the profile.</summary>
    internal FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap ResolvedMap { get; }

    /// <summary>Exact confirmed-present capability bindings required by the profile, in canonical requirement order.</summary>
    internal IReadOnlyList<AdmittedCapabilityEvidence> RequiredCapabilities { get; }

    private static void ValidateFamilyAndMap(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
    {
        CompositionProfileMapBinding binding = profile.MapBinding;
        if (!StringComparer.Ordinal.Equals(binding.FamilyId, family.FamilyId) ||
            !StringComparer.Ordinal.Equals(binding.FamilyVersion, family.FamilyVersion) ||
            !StringComparer.Ordinal.Equals(binding.FamilyContentHash, family.FamilyContentHash) ||
            !StringComparer.Ordinal.Equals(resolvedMap.FamilyId, family.FamilyId) ||
            !StringComparer.Ordinal.Equals(resolvedMap.FamilyVersion, family.FamilyVersion) ||
            !StringComparer.Ordinal.Equals(resolvedMap.FamilyContentHash, family.FamilyContentHash) ||
            !family.ImageMaps.Any(map => ReferenceEquals(map, resolvedMap.ImageMap)) ||
            !binding.MapIds.Contains(resolvedMap.ImageMap.MapId, StringComparer.Ordinal))
        {
            throw new ArgumentException("Admission profile, family, and resolved-map identities must be exact.");
        }

        var regionIds = resolvedMap.ImageMap.Regions
            .Select(static region => region.RegionId)
            .ToHashSet(StringComparer.Ordinal);
        if (binding.RequiredRegionIds.Any(requiredId => !regionIds.Contains(requiredId)) ||
            binding.RequiredMetadataStructureIds.Any(requiredId =>
                !CompositionProfileMapAdmissionValidator.IsMetadataStructureDeclared(
                    family,
                    resolvedMap,
                    requiredId)))
        {
            throw new ArgumentException("Admission map does not satisfy every required physical or metadata fact.");
        }
    }

    private static void ValidateCapabilityEvidence(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        IReadOnlyList<AdmittedCapabilityEvidence> requiredCapabilities)
    {
        string[] expectedCapabilityIds = [.. profile.MapBinding.RequiredCapabilityIds];
        string[] actualCapabilityIds = [.. requiredCapabilities.Select(static evidence => evidence.RequiredCapabilityId)];
        if (!expectedCapabilityIds.SequenceEqual(actualCapabilityIds, StringComparer.Ordinal))
        {
            throw new ArgumentException("Admission capability evidence must exactly match profile requirements.");
        }

        foreach (AdmittedCapabilityEvidence evidence in requiredCapabilities)
        {
            FirmwareMapFactBinding<FirmwareCapabilityFact> binding = evidence.Binding;
            if (!family.CapabilityBindings.Any(candidate => ReferenceEquals(candidate, binding)) ||
                !StringComparer.Ordinal.Equals(binding.EffectiveKey.MemberId, resolvedMap.MemberId) ||
                !StringComparer.Ordinal.Equals(binding.EffectiveKey.MapId, resolvedMap.ImageMap.MapId) ||
                binding.Applicability.Evaluate(resolvedMap) != FirmwareApplicabilityResult.Match)
            {
                throw new ArgumentException(
                    "Admission capability evidence must be family-owned and applicable to the resolved effective member and map.");
            }
        }
    }
}

/// <summary>One required technical capability and its effective-to-direct evidence binding.</summary>
internal sealed class AdmittedCapabilityEvidence
{
    internal AdmittedCapabilityEvidence(
        string requiredCapabilityId,
        FirmwareMapFactBinding<FirmwareCapabilityFact> binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredCapabilityId);
        ArgumentNullException.ThrowIfNull(binding);
        if (!StringComparer.Ordinal.Equals(requiredCapabilityId, binding.Value.CapabilityId) ||
            binding.Value.State != FirmwareCapabilityState.ConfirmedPresent)
        {
            throw new ArgumentException(
                "Admitted capability evidence must prove its required confirmed-present technical capability.",
                nameof(binding));
        }

        RequiredCapabilityId = requiredCapabilityId;
        Binding = binding;
    }

    /// <summary>Technical capability id required by the normalized profile.</summary>
    internal string RequiredCapabilityId { get; }

    /// <summary>Exact effective/direct alias provenance and evidence selected for the requirement.</summary>
    internal FirmwareMapFactBinding<FirmwareCapabilityFact> Binding { get; }
}
