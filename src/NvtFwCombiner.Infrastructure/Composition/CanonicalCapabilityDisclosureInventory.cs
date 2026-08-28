using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>
/// Materializes client disclosure once while the trusted catalog candidate is
/// loaded. Bootstrap registers this adapter; published clients never consult
/// the profile registries again.
/// </summary>
internal static class CanonicalCapabilityDisclosureInventory
{
    internal static CanonicalCapabilityDisclosure Create(
        IReadOnlyList<CanonicalCapabilityDefinition> definitions,
        IReadOnlyList<CanonicalDynamicCapabilityDefinition> dynamicDefinitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(dynamicDefinitions);
        string[] icIds =
        [
            .. BuiltInV2RegistrationRegistry.StandardMergeByIc.Keys
                .Order(StringComparer.Ordinal),
        ];
        Dictionary<string, IReadOnlyList<CapabilityProfileSummary>> profiles = new(
            StringComparer.Ordinal)
        {
            [ExperienceIds.StandardMerge] = CreateProfileSummaries(
                BuiltInV2RegistrationRegistry.StandardMergeByIc.Values),
            [ExperienceIds.AbMerge] = CreateProfileSummaries(
                BuiltInV2RegistrationRegistry.AbMergeByIc.Values),
            [ExperienceIds.DpReplace] = CreateProfileSummaries(
                BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.Values),
        };
        Dictionary<string, IReadOnlyList<CapabilityNumberChoice>> numberChoices =
            icIds.ToDictionary(
            static icId => icId,
            static icId => (IReadOnlyList<CapabilityNumberChoice>)Array.AsReadOnly(
            [
                .. IcNumberChoicePolicy.GetNumberSelectionChoices(
                        BuiltInPostbuildProfileCatalog.GetProfiles(icId))
                    .Select(static choice => new CapabilityNumberChoice(
                        choice.Token,
                        choice.DisplayLabel)),
            ]),
            StringComparer.Ordinal);
        var dpCapacities = new Dictionary<string, IReadOnlyList<long>>(
            StringComparer.Ordinal);
        foreach (BuiltInV2Registration registration in
                 BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.Values)
        {
            IReadOnlyList<long> capacities = registration.GetMapCapacities(
                out IReadOnlyList<CompositionIssue> issues);
            if (issues.Count == 0)
            {
                dpCapacities.Add(registration.IcId, capacities);
            }
        }

        Dictionary<string, CapabilityFamilySummary> families = icIds.ToDictionary(
            static icId => icId,
            icId => CreateFamilySummary(icId, definitions, dynamicDefinitions),
            StringComparer.Ordinal);
        string[] dpPerspectiveIcs =
        [
            .. BuiltInV2RegistrationRegistry.StandardMergeByIc.Values
                .Where(static registration =>
                    registration.TryGetContainerPolicy(out _))
                .Select(static registration => registration.IcId),
        ];
        return new CanonicalCapabilityDisclosure(
            profiles,
            numberChoices,
            dpCapacities,
            families,
            dpPerspectiveIcs);
    }

    private static ReadOnlyCollection<CapabilityProfileSummary> CreateProfileSummaries(
        IEnumerable<BuiltInV2Registration> registrations)
    {
        return Array.AsReadOnly(
        [
            .. registrations
                .Select(static registration => registration.CreateProfileSummary())
                .OrderBy(static summary => summary.IcId, StringComparer.Ordinal)
                .ThenBy(static summary => summary.ProfileId, StringComparer.Ordinal),
        ]);
    }

    private static CapabilityFamilySummary CreateFamilySummary(
        string icId,
        IReadOnlyList<CanonicalCapabilityDefinition> definitions,
        IReadOnlyList<CanonicalDynamicCapabilityDefinition> dynamicDefinitions)
    {
        var compositions = new List<CompiledComposition>(definitions
            .Where(definition =>
                StringComparer.Ordinal.Equals(definition.Identity.IcId, icId))
            .Select(static definition => definition.CompiledComposition));
        BuiltInV2Registration registration =
            BuiltInV2RegistrationRegistry.StandardMergeByIc[icId];
        bool hasStandardRoute = definitions.Any(definition =>
                StringComparer.Ordinal.Equals(definition.Identity.IcId, icId) &&
                StringComparer.Ordinal.Equals(
                    definition.Identity.WorkflowId,
                    ExperienceIds.StandardMerge)) ||
            dynamicDefinitions.Any(definition =>
                StringComparer.Ordinal.Equals(definition.Identity.IcId, icId) &&
                StringComparer.Ordinal.Equals(
                    definition.Identity.WorkflowId,
                    ExperienceIds.StandardMerge));
        if (hasStandardRoute)
        {
            IReadOnlyList<long> capacities = registration.GetMapCapacities(
                out IReadOnlyList<CompositionIssue> issues);
            if (issues.Count != 0)
            {
                throw new InvalidDataException(
                    $"Canonical family disclosure for '{icId}' was rejected: " +
                    string.Join(", ", issues.Select(static issue => issue.Code)));
            }

            long?[] candidates = capacities.Count == 0
                ? [null]
                : [.. capacities.Select(static capacity => (long?)capacity)];
            foreach (long? capacity in candidates)
            {
                registration.TryCompile(
                    capacity,
                    out CompiledComposition? composition,
                    out IReadOnlyList<CompositionIssue> compileIssues);
                if (composition is null || compileIssues.Count != 0)
                {
                    throw new InvalidDataException(
                        $"Canonical family disclosure for '{icId}' failed at capacity " +
                        $"'{(capacity is null ? "default" : $"0x{capacity:X}")}': " +
                        string.Join(", ", compileIssues.Select(static issue => issue.Code)));
                }

                compositions.Add(composition);
            }
        }

        var discovered = new List<(
            string FamilyId,
            FirmwareFamilyRelationship Relationship)>();
        foreach (MapBoundV2CompilationContext context in compositions
                     .DistinctBy(static composition =>
                         composition.CompilationFingerprint)
                     .Select(static composition =>
                         composition.V2Details.Provenance.Context)
                     .OfType<MapBoundV2CompilationContext>())
        {
            discovered.AddRange(context.ResolvedMap.FamilyRelationships
                .Where(relationship => relationship.MemberIds.Contains(
                    icId,
                    StringComparer.Ordinal))
                .Select(relationship => (
                    context.ResolvedMap.FamilyId,
                    relationship)));
        }

        (string FamilyId, FirmwareFamilyRelationship Relationship)[] bindings =
        [
            .. discovered.DistinctBy(static binding => (
                binding.FamilyId,
                binding.Relationship.RelationshipId)),
        ];
        IEnumerable<(string FamilyId, FirmwareFamilyRelationship Relationship)>
            selectedSource = bindings.Any(static binding =>
                binding.Relationship is PerfectFamilyRelationship)
                    ? bindings.Where(static binding =>
                        binding.Relationship is PerfectFamilyRelationship)
                    : bindings.Where(static binding =>
                        binding.Relationship is SharedFactRelationship);
        (string FamilyId, FirmwareFamilyRelationship Relationship)[] selected =
            [.. selectedSource];
        string[] familyIds =
        [
            .. selected.Select(static binding => binding.FamilyId)
                .Distinct(StringComparer.Ordinal),
        ];
        return selected.Length == 0
            ? new CapabilityFamilySummary(
                null,
                CapabilityFamilyRelationship.Standalone,
                null)
            : familyIds.Length != 1
                ? throw new InvalidDataException(
                    $"Canonical family facts for '{icId}' resolve to multiple families: " +
                    string.Join(", ", familyIds.Order(StringComparer.Ordinal)))
                : new CapabilityFamilySummary(
                    familyIds[0],
                    selected[0].Relationship is PerfectFamilyRelationship
                        ? CapabilityFamilyRelationship.PerfectAlias
                        : CapabilityFamilyRelationship.PartialAlias,
                    string.Join(
                        " ",
                        selected.Select(static binding => binding.Relationship.Reason)
                            .Distinct(StringComparer.Ordinal)
                            .Order(StringComparer.Ordinal)));
    }
}
