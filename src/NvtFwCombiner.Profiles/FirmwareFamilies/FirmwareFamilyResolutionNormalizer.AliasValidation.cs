using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

internal static partial class FirmwareFamilyResolutionNormalizer
{
    private static FirmwareMapFactBinding<FirmwareCapabilityFact>[] NormalizeCapabilities(
        IReadOnlyList<FirmwareCapabilityFactDocument> documents,
        IReadOnlyList<AliasDeclaration> aliases,
        Dictionary<string, FirmwareImageMap> mapsById,
        Dictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> structuresByMap,
        Dictionary<string, FirmwareMapApplicability> mapApplicabilities,
        Dictionary<FirmwareMapFactKey, FirmwareFactApplicability> aliasApplicabilities)
    {
        var direct = new Dictionary<FirmwareMapFactKey, CapabilityDirect>();
        for (int index = 0; index < documents.Count; index++)
        {
            FirmwareCapabilityFactDocument document = documents[index];
            string path = $"capabilities[{index}]";

            FirmwareImageMap map = mapsById.TryGetValue(document.MapId, out FirmwareImageMap? candidate)
                ? candidate
                : throw Error($"{path}.mapId", $"Unknown image map '{document.MapId}'.");
            if (!map.Applicability.MemberIds.Contains(document.MemberId, StringComparer.Ordinal))
            {
                throw Error(
                    $"{path}.memberId",
                    $"Member '{document.MemberId}' is not selected by image map '{document.MapId}'.");
            }

            FirmwareFactApplicability applicability = NormalizeFactApplicability(
                document.Applicability,
                structuresByMap[document.MapId],
                $"{path}.applicability");
            ValidateCapabilityApplicability(
                applicability,
                FirmwareFactApplicability.FromMap(mapApplicabilities[document.MapId]),
                structuresByMap[document.MapId],
                $"{path}.applicability");
            FirmwareCapabilityState state = document.State switch
            {
                "confirmed-present" => FirmwareCapabilityState.ConfirmedPresent,
                "confirmed-absent" => FirmwareCapabilityState.ConfirmedAbsent,
                "unknown" => FirmwareCapabilityState.Unknown,
                _ => throw Error($"{path}.state", "Unknown capability state."),
            };
            FirmwareCapabilityFact value = new(
                document.CapabilityFactId,
                document.CapabilityId,
                state,
                document.Reason,
                document.EvidenceRefs);
            var key = new FirmwareMapFactKey(
                document.MemberId,
                document.MapId,
                FirmwareFactKind.Capability,
                document.CapabilityFactId);
            if (!direct.TryAdd(key, new CapabilityDirect(value, applicability)))
            {
                throw Error(path, $"Duplicate direct capability fact '{DescribeKey(key)}'.");
            }
        }

        AliasDeclaration[] capabilityAliases =
        [
            .. aliases.Where(static alias => alias.TargetKey.FactKind == FirmwareFactKind.Capability),
        ];
        var values = direct.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Value);
        FirmwareMapFactKey[] expected =
        [
            .. direct.Keys.Concat(capabilityAliases.Select(static alias => alias.TargetKey)),
        ];
        Dictionary<FirmwareMapFactKey, ResolvedFact<FirmwareCapabilityFact>> resolved =
            new FactAliasResolver<FirmwareCapabilityFact>(values, capabilityAliases).ResolveAll(expected);

        var applications = direct.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Applicability);
        foreach (AliasDeclaration alias in capabilityAliases)
        {
            applications.Add(alias.TargetKey, aliasApplicabilities[alias.TargetKey]);
        }

        foreach (AliasDeclaration alias in capabilityAliases)
        {
            FirmwareFactApplicability aliasApplicability = applications[alias.TargetKey];
            FirmwareFactApplicability sourceApplicability = applications.TryGetValue(
                alias.SourceKey,
                out FirmwareFactApplicability? source)
                ? source
                : throw Error($"{alias.Path}.sourceCapabilityFactId", "Alias source capability fact is unresolved.");
            ValidateSharedPredicateStructures(
                alias,
                aliasApplicability,
                sourceApplicability,
                structuresByMap);
            ValidateCapabilityApplicability(
                aliasApplicability,
                FirmwareFactApplicability.FromMap(mapApplicabilities[alias.TargetKey.MapId]),
                structuresByMap[alias.TargetKey.MapId],
                $"{alias.Path}.applicability");
            if (!FirmwareFactApplicabilityRelations.IsContainedBy(
                    aliasApplicability,
                    sourceApplicability,
                    structuresByMap[alias.TargetKey.MapId]))
            {
                throw Error(
                    $"{alias.Path}.applicability",
                    "Alias applicability is not contained by its source availability.");
            }
        }

        FirmwareMapFactBinding<FirmwareCapabilityFact>[] bindings =
        [
            .. resolved.Values.Select(value => CreateBinding(
                value,
                applications[value.EffectiveKey],
                aliasApplicabilities)),
        ];
        ValidateCapabilityOverlap(bindings, structuresByMap);
        return bindings;
    }

    private static void ValidateCapabilityApplicability(
        FirmwareFactApplicability applicability,
        FirmwareFactApplicability mapApplicability,
        IReadOnlyDictionary<string, FirmwareMetadataStructure> structures,
        string path)
    {
        if (!FirmwareFactApplicabilityRelations.IsSatisfiable(applicability, structures))
        {
            throw Error(path, "Capability applicability is unsatisfiable.");
        }

        if (!FirmwareFactApplicabilityRelations.IsContainedBy(applicability, mapApplicability, structures))
        {
            throw Error(path, "Capability applicability is not contained by its image map.");
        }
    }

    private static void ValidateCapabilityOverlap(
        IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> bindings,
        Dictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> structuresByMap)
    {
        foreach (IGrouping<(string MemberId, string MapId, string CapabilityId), FirmwareMapFactBinding<FirmwareCapabilityFact>> group in
                 bindings.GroupBy(static binding => (
                     binding.EffectiveKey.MemberId,
                     binding.EffectiveKey.MapId,
                     binding.Value.CapabilityId)))
        {
            FirmwareMapFactBinding<FirmwareCapabilityFact>[] candidates = [.. group];
            for (int index = 0; index < candidates.Length; index++)
            {
                for (int other = index + 1; other < candidates.Length; other++)
                {
                    if (FirmwareFactApplicabilityRelations.Overlaps(
                            candidates[index].Applicability,
                            candidates[other].Applicability,
                            structuresByMap[group.Key.MapId]))
                    {
                        throw Error(
                            "capabilities",
                            $"Capability '{group.Key.CapabilityId}' has overlapping evidence providers for " +
                            $"member '{group.Key.MemberId}' and map '{group.Key.MapId}'.");
                    }
                }
            }
        }
    }

    private static void ValidateSharedPredicateStructures(
        AliasDeclaration alias,
        FirmwareFactApplicability candidate,
        FirmwareFactApplicability container,
        Dictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> structuresByMap)
    {
        IReadOnlyDictionary<string, FirmwareMetadataStructure> target = structuresByMap[alias.TargetKey.MapId];
        IReadOnlyDictionary<string, FirmwareMetadataStructure> source = structuresByMap[alias.SourceKey.MapId];
        foreach (string structureId in candidate.MetadataPredicates
                     .Concat(container.MetadataPredicates)
                     .Select(static predicate => predicate.MetadataStructureId)
                     .Distinct(StringComparer.Ordinal))
        {
            if (!target.TryGetValue(structureId, out FirmwareMetadataStructure? targetStructure) ||
                !source.TryGetValue(structureId, out FirmwareMetadataStructure? sourceStructure) ||
                !ReferenceEquals(targetStructure, sourceStructure))
            {
                throw Error(
                    $"{alias.Path}.applicability.metadataPredicates",
                    $"Source and target maps must select the same canonical metadata structure '{structureId}'.");
            }
        }
    }

    private static void ValidateAliasPredicateDependencies(
        IReadOnlyList<AliasDeclaration> aliases,
        Dictionary<FirmwareMapFactKey, FirmwareFactApplicability> aliasApplicabilities,
        IReadOnlyDictionary<string, MapInput> mapsById,
        IReadOnlyDictionary<FirmwareMapFactKey, ResolvedFact<FirmwareMetadataSet>> metadata)
    {
        var aliasesByTarget = aliases.ToDictionary(static alias => alias.TargetKey);
        var dependencies = aliases.ToDictionary(
            static alias => alias.TargetKey,
            static _ => new List<FirmwareMapFactKey>());
        foreach (AliasDeclaration alias in aliases)
        {
            if (aliasesByTarget.ContainsKey(alias.SourceKey))
            {
                dependencies[alias.TargetKey].Add(alias.SourceKey);
            }

            FirmwareFactApplicability applicability = aliasApplicabilities[alias.TargetKey];
            foreach (FirmwareMetadataPredicate predicate in applicability.MetadataPredicates)
            {
                FirmwareMapFactKey metadataKey = FindMetadataBindingForStructure(
                    alias.TargetKey.MemberId,
                    alias.TargetKey.MapId,
                    predicate.MetadataStructureId,
                    mapsById,
                    metadata);
                if (aliasesByTarget.ContainsKey(metadataKey))
                {
                    dependencies[alias.TargetKey].Add(metadataKey);
                }
            }
        }

        _ = AcyclicDependencyGraph.Sort(
            dependencies.Keys,
            key => dependencies[key].Distinct(),
            (_, dependency) => Error(
                "factAliases",
                $"Alias predicate dependency cycle includes '{DescribeKey(dependency)}'."));
    }

    private static FirmwareMapFactKey FindMetadataBindingForStructure(
        string memberId,
        string mapId,
        string structureId,
        IReadOnlyDictionary<string, MapInput> mapsById,
        IReadOnlyDictionary<FirmwareMapFactKey, ResolvedFact<FirmwareMetadataSet>> metadata)
    {
        MapInput map = mapsById[mapId];
        FirmwareMapFactKey[] matches =
        [
            .. map.Document.MetadataSetIds
                .Select(factId => new FirmwareMapFactKey(memberId, mapId, FirmwareFactKind.MetadataSet, factId))
                .Where(key => metadata[key].Value.Structures.Any(structure =>
                    StringComparer.Ordinal.Equals(structure.StructureId, structureId))),
        ];
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw Error(
                "factAliases",
                $"Metadata structure '{structureId}' is not selected by map '{mapId}' for member '{memberId}'."),
            _ => throw Error(
                "factAliases",
                $"Metadata structure '{structureId}' is ambiguously selected by map '{mapId}' for member '{memberId}'."),
        };
    }

    private static void EnsureAllRegionSetsAreBound(
        IReadOnlyDictionary<string, FirmwareRegionSet> regionSetsById,
        IEnumerable<ResolvedFact<FirmwareRegionSet>> bindings)
    {
        var boundIds = new HashSet<string>(
            bindings.Select(static binding => binding.Value.CanonicalFactId),
            StringComparer.Ordinal);
        string? orphan = regionSetsById.Keys.FirstOrDefault(id => !boundIds.Contains(id));
        if (orphan is not null)
        {
            throw Error("regionSets", $"Region set '{orphan}' is not referenced by an image map.");
        }
    }

    private static MapInput FindMap(
        IReadOnlyDictionary<string, MapInput> mapsById,
        string mapId,
        string path)
    {
        return mapsById.TryGetValue(mapId, out MapInput? map)
            ? map
            : throw Error(path, $"Unknown image map '{mapId}'.");
    }

    private static void EnsureMapSelectsMember(MapInput map, string memberId, string path)
    {
        if (!map.Document.Applicability.MemberIds.Contains(memberId, StringComparer.Ordinal))
        {
            throw Error(path, $"Member '{memberId}' is not selected by image map '{map.Document.MapId}'.");
        }
    }

    private static void EnsureMapDeclaresFact(
        MapInput map,
        FirmwareFactKind kind,
        string factId,
        string path)
    {
        IReadOnlyList<string> declared = kind switch
        {
            FirmwareFactKind.RegionSet => map.Document.RegionSetIds,
            FirmwareFactKind.MetadataSet => map.Document.MetadataSetIds,
            FirmwareFactKind.Capability => throw new ArgumentOutOfRangeException(
                nameof(kind),
                "Capabilities are not declared by an image map fact list."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Expected a physical fact kind."),
        };
        if (!declared.Contains(factId, StringComparer.Ordinal))
        {
            throw Error(
                path,
                $"Image map '{map.Document.MapId}' does not declare fact '{factId}'.");
        }
    }

    private static string FactCollectionName(FirmwareFactKind kind)
    {
        return kind switch
        {
            FirmwareFactKind.RegionSet => "regionSetIds",
            FirmwareFactKind.MetadataSet => "metadataSetIds",
            FirmwareFactKind.Capability => throw new ArgumentOutOfRangeException(
                nameof(kind),
                "Capabilities are not declared by an image map fact list."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Expected a physical fact kind."),
        };
    }

    private static string DescribeKey(FirmwareMapFactKey key)
    {
        return $"{key.MemberId}/{key.MapId}/{key.FactKind}/{key.FactId}";
    }

}
