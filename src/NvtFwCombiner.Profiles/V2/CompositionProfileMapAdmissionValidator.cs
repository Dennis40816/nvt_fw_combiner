using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Checks whether one normalized v2 profile can use one already-resolved canonical map.</summary>
internal static class CompositionProfileMapAdmissionValidator
{
    private const string FamilyIdMismatch = "profile.v2.map.family-id-mismatch";
    private const string FamilyVersionMismatch = "profile.v2.map.family-version-mismatch";
    private const string FamilyContentHashMismatch = "profile.v2.map.family-content-hash-mismatch";
    private const string MapNotAllowed = "profile.v2.map.map-not-allowed";
    private const string RequiredRegionMissing = "profile.v2.map.required-region-missing";
    private const string RequiredMetadataStructureMissing = "profile.v2.map.required-metadata-structure-missing";
    private const string RequiredCapabilityUnavailable = "profile.v2.map.required-capability-unavailable";

    /// <summary>
    /// Validates only map identity and effective physical/decoded facts. This result grants no execution authority.
    /// </summary>
    internal static CompositionProfileMapAdmissionResult Validate(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(resolvedMap);

        CompositionProfileMapBinding binding = profile.MapBinding;
        var issues = new List<CompositionIssue>();

        AddIdentityIssues(binding, resolvedMap, issues);
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

        var resolvedMetadataStructureIds = resolvedMap.ResolvedMetadataStructures
            .Select(static structure => structure.DecodedStructure.MetadataStructureId)
            .ToHashSet(StringComparer.Ordinal);
        AddMissingIssues(
            binding.RequiredMetadataStructureIds,
            resolvedMetadataStructureIds,
            RequiredMetadataStructureMissing,
            "metadata structure",
            issues);

        // Capability bindings are a sibling to resolved-map output. They cannot be inferred or promoted here.
        foreach (string capabilityId in binding.RequiredCapabilityIds)
        {
            issues.Add(new CompositionIssue(
                RequiredCapabilityUnavailable,
                $"Required capability '{capabilityId}' is unavailable from the resolved-map admission input."));
        }

        return new CompositionProfileMapAdmissionResult(issues);
    }

    private static void AddIdentityIssues(
        CompositionProfileMapBinding binding,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        List<CompositionIssue> issues)
    {
        if (!StringComparer.Ordinal.Equals(binding.FamilyId, resolvedMap.FamilyId))
        {
            issues.Add(new CompositionIssue(
                FamilyIdMismatch,
                "Profile and resolved map identify different firmware families."));
        }

        if (!StringComparer.Ordinal.Equals(binding.FamilyVersion, resolvedMap.FamilyVersion))
        {
            issues.Add(new CompositionIssue(
                FamilyVersionMismatch,
                "Profile and resolved map identify different firmware family versions."));
        }

        if (!StringComparer.Ordinal.Equals(binding.FamilyContentHash, resolvedMap.FamilyContentHash))
        {
            issues.Add(new CompositionIssue(
                FamilyContentHashMismatch,
                "Profile and resolved map identify different firmware family content hashes."));
        }
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
}

/// <summary>Immutable, non-executable outcome of admitting a v2 profile to one resolved map.</summary>
internal sealed class CompositionProfileMapAdmissionResult
{
    private readonly CompositionIssue[] _issues;

    internal CompositionProfileMapAdmissionResult(IEnumerable<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        _issues = [.. issues];
        if (_issues.Any(static issue => issue is null))
        {
            throw new ArgumentException("Map-admission issues cannot contain null.", nameof(issues));
        }

        Array.Sort(_issues, static (left, right) =>
        {
            int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
            return code != 0
                ? code
                : StringComparer.Ordinal.Compare(left.Message, right.Message);
        });
        Issues = Array.AsReadOnly(_issues);
    }

    /// <summary>True only when the profile's map requirements all match the selected resolved map.</summary>
    internal bool IsAdmitted => _issues.Length == 0;

    /// <summary>Deterministically ordered admission blockers.</summary>
    internal IReadOnlyList<CompositionIssue> Issues { get; }
}
