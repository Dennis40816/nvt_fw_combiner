using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

public static partial class FirmwareFamilyResolutionNormalizer
{
    private static FirmwareFamilyResolutionDefinition NormalizeMapBoundFacts(
        FirmwareFamilyDocument document,
        string familyContentHash)
    {
        IReadOnlyList<FirmwareFamilyMemberDocument> members = RequireList(document.Members, "members");
        Dictionary<string, FirmwareFamilyMemberDocument> membersById = IndexUnique(
            members,
            static member => member.MemberId,
            "members",
            "memberId");
        Dictionary<string, FirmwareRegionSet> regionSetsById = NormalizeRegionSets(
            RequireList(document.RegionSets, "regionSets"));
        Dictionary<string, FirmwareMetadataSet> metadataSetsById = NormalizeMetadataSets(
            RequireList(document.MetadataSets, "metadataSets"));
        ValidateGlobalStructureIds(metadataSetsById.Values);

        MapInput[] maps = CreateMaps(
            RequireList(document.ImageMaps, "imageMaps"),
            membersById);
        Dictionary<string, MapInput> mapsById = maps.ToDictionary(
            static map => map.Document.MapId,
            StringComparer.Ordinal);
        AliasDeclaration[] aliases = CreateAliases(
            RequireList(document.FactAliases, "factAliases"),
            mapsById);

        Dictionary<FirmwareMapFactKey, ResolvedFact<FirmwareRegionSet>> regions = ResolvePhysicalFacts(
            maps,
            aliases,
            FirmwareFactKind.RegionSet,
            regionSetsById,
            static map => map.Document.RegionSetIds);
        Dictionary<FirmwareMapFactKey, ResolvedFact<FirmwareMetadataSet>> metadata = ResolvePhysicalFacts(
            maps,
            aliases,
            FirmwareFactKind.MetadataSet,
            metadataSetsById,
            static map => map.Document.MetadataSetIds);
        EnsureAllRegionSetsAreBound(regionSetsById, regions.Values);

        Dictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> structuresByMap =
            MaterializeStructuresByMap(maps, metadata);
        Dictionary<string, FirmwareMapApplicability> mapApplicabilities = NormalizeMapApplicabilities(
            maps,
            structuresByMap);
        Dictionary<FirmwareMapFactKey, FirmwareFactApplicability> aliasApplicabilities =
            NormalizeAliasApplicabilities(aliases, structuresByMap);

        ValidatePhysicalAliases(
            aliases,
            mapApplicabilities,
            structuresByMap,
            aliasApplicabilities);
        ValidateAliasPredicateDependencies(aliases, aliasApplicabilities, mapsById, metadata);

        FirmwareImageMap[] normalizedMaps = MaterializeMaps(
            maps,
            regions,
            metadata,
            mapApplicabilities,
            aliasApplicabilities);
        Dictionary<string, FirmwareImageMap> normalizedMapsById = normalizedMaps.ToDictionary(
            static map => map.MapId,
            StringComparer.Ordinal);
        FirmwareMapFactBinding<FirmwareCapabilityFact>[] capabilities = NormalizeCapabilities(
            RequireList(document.Capabilities, "capabilities"),
            aliases,
            normalizedMapsById,
            structuresByMap,
            mapApplicabilities,
            aliasApplicabilities);

        return TranslateInvariant("$", () => new FirmwareFamilyResolutionDefinition(
                document.FamilyId,
                document.FamilyVersion,
                familyContentHash,
                normalizedMaps,
                metadataSetsById.Values,
                capabilities));
    }

    private static MapInput[] CreateMaps(
        IReadOnlyList<FirmwareImageMapDocument> documents,
        IReadOnlyDictionary<string, FirmwareFamilyMemberDocument> membersById)
    {
        var maps = new MapInput[documents.Count];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < documents.Count; index++)
        {
            FirmwareImageMapDocument document = documents[index] ?? throw Error(
                $"imageMaps[{index}]",
                "Image map cannot be null.");
            string path = $"imageMaps[{index}]";
            if (string.IsNullOrWhiteSpace(document.MapId))
            {
                throw Error($"{path}.mapId", "Map id cannot be null or whitespace.");
            }

            if (!ids.Add(document.MapId))
            {
                throw Error($"{path}.mapId", $"Duplicate image map id '{document.MapId}'.");
            }

            ValidateMemberReferences(document.Applicability.MemberIds, membersById, $"{path}.applicability.memberIds");
            ValidateDistinctFactIds(document.RegionSetIds, $"{path}.regionSetIds");
            ValidateDistinctFactIds(document.MetadataSetIds, $"{path}.metadataSetIds");
            maps[index] = new MapInput(index, document);
        }

        return maps;
    }

    private static AliasDeclaration[] CreateAliases(
        IReadOnlyList<FirmwareFactAliasDocument> documents,
        IReadOnlyDictionary<string, MapInput> mapsById)
    {
        var aliases = new AliasDeclaration[documents.Count];
        var aliasIds = new HashSet<string>(StringComparer.Ordinal);
        var targets = new HashSet<FirmwareMapFactKey>();
        for (int index = 0; index < documents.Count; index++)
        {
            FirmwareFactAliasDocument document = documents[index] ?? throw Error(
                $"factAliases[{index}]",
                "Fact alias cannot be null.");
            string path = $"factAliases[{index}]";
            if (!aliasIds.Add(document.AliasId))
            {
                throw Error($"{path}.aliasId", $"Duplicate alias id '{document.AliasId}'.");
            }

            (FirmwareFactKind kind, string targetFactId, string sourceFactId) = GetAliasFactIds(document, path);
            MapInput targetMap = FindMap(mapsById, document.TargetMapId, $"{path}.targetMapId");
            MapInput sourceMap = FindMap(mapsById, document.SourceMapId, $"{path}.sourceMapId");
            EnsureMapSelectsMember(targetMap, document.TargetMemberId, $"{path}.targetMemberId");
            EnsureMapSelectsMember(sourceMap, document.SourceMemberId, $"{path}.sourceMemberId");
            if (kind is FirmwareFactKind.RegionSet or FirmwareFactKind.MetadataSet)
            {
                EnsureMapDeclaresFact(targetMap, kind, targetFactId, $"{path}.target");
                EnsureMapDeclaresFact(sourceMap, kind, sourceFactId, $"{path}.source");
            }

            var target = new FirmwareMapFactKey(document.TargetMemberId, document.TargetMapId, kind, targetFactId);
            var source = new FirmwareMapFactKey(document.SourceMemberId, document.SourceMapId, kind, sourceFactId);
            if (!targets.Add(target))
            {
                throw Error(path, $"Duplicate alias target '{DescribeKey(target)}'.");
            }

            aliases[index] = new AliasDeclaration(index, path, document, target, source);
        }

        return aliases;
    }

    private static (FirmwareFactKind Kind, string TargetFactId, string SourceFactId) GetAliasFactIds(
        FirmwareFactAliasDocument document,
        string path)
    {
        return document switch
        {
            FirmwareRegionSetAliasDocument region => (
                FirmwareFactKind.RegionSet,
                RequireFactId(region.TargetRegionSetId, $"{path}.targetRegionSetId"),
                RequireFactId(region.SourceRegionSetId, $"{path}.sourceRegionSetId")),
            FirmwareMetadataSetAliasDocument metadata => (
                FirmwareFactKind.MetadataSet,
                RequireFactId(metadata.TargetMetadataSetId, $"{path}.targetMetadataSetId"),
                RequireFactId(metadata.SourceMetadataSetId, $"{path}.sourceMetadataSetId")),
            FirmwareCapabilityAliasDocument capability => (
                FirmwareFactKind.Capability,
                RequireFactId(capability.TargetCapabilityFactId, $"{path}.targetCapabilityFactId"),
                RequireFactId(capability.SourceCapabilityFactId, $"{path}.sourceCapabilityFactId")),
            _ => throw Error(path, "Unknown fact alias shape."),
        };
    }

    private static Dictionary<FirmwareMapFactKey, ResolvedFact<TFact>> ResolvePhysicalFacts<TFact>(
        IReadOnlyList<MapInput> maps,
        IReadOnlyList<AliasDeclaration> aliases,
        FirmwareFactKind kind,
        IReadOnlyDictionary<string, TFact> directValuesById,
        Func<MapInput, IReadOnlyList<string>?> factIds)
        where TFact : class, IFirmwareMapFact
    {
        var expected = new List<FirmwareMapFactKey>();
        var direct = new Dictionary<FirmwareMapFactKey, TFact>();
        AliasDeclaration[] typedAliases = [.. aliases.Where(alias => alias.TargetKey.FactKind == kind)];
        Dictionary<FirmwareMapFactKey, AliasDeclaration> aliasesByTarget = typedAliases.ToDictionary(
            static alias => alias.TargetKey);
        foreach (MapInput map in maps)
        {
            IReadOnlyList<string> ids = RequireList(factIds(map), $"{map.Path}.{FactCollectionName(kind)}");
            foreach (string memberId in map.Document.Applicability.MemberIds)
            {
                for (int index = 0; index < ids.Count; index++)
                {
                    string factId = ids[index];
                    var key = new FirmwareMapFactKey(memberId, map.Document.MapId, kind, factId);
                    expected.Add(key);
                    if (directValuesById.TryGetValue(factId, out TFact? value) && !direct.TryAdd(key, value))
                    {
                        throw Error(map.Path, $"Duplicate direct fact '{DescribeKey(key)}'.");
                    }

                    if (!directValuesById.ContainsKey(factId) && !aliasesByTarget.ContainsKey(key))
                    {
                        AliasDeclaration? sourceAlias = typedAliases.FirstOrDefault(alias => alias.SourceKey == key);
                        throw Error(
                            sourceAlias is null
                                ? $"{map.Path}.{FactCollectionName(kind)}[{index}]"
                                : $"{sourceAlias.Path}.source",
                            $"Fact '{DescribeKey(key)}' has no direct provider or alias.");
                    }
                }
            }
        }

        return new FactAliasResolver<TFact>(direct, typedAliases).ResolveAll(expected);
    }

    private static Dictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> MaterializeStructuresByMap(
        IReadOnlyList<MapInput> maps,
        IReadOnlyDictionary<FirmwareMapFactKey, ResolvedFact<FirmwareMetadataSet>> metadata)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>>(StringComparer.Ordinal);
        foreach (MapInput map in maps)
        {
            var structures = new Dictionary<string, FirmwareMetadataStructure>(StringComparer.Ordinal);
            foreach (string factId in map.Document.MetadataSetIds)
            {
                ResolvedFact<FirmwareMetadataSet>[] values =
                [
                    .. map.Document.Applicability.MemberIds.Select(memberId => metadata[
                        new FirmwareMapFactKey(memberId, map.Document.MapId, FirmwareFactKind.MetadataSet, factId)]),
                ];
                ResolvedFact<FirmwareMetadataSet> first = values[0];
                if (values.Skip(1).Any(value =>
                    !StringComparer.Ordinal.Equals(value.Value.CanonicalFactId, first.Value.CanonicalFactId) ||
                    !ReferenceEquals(value.Value, first.Value)))
                {
                    throw Error(
                        $"{map.Path}.metadataSetIds",
                        $"Map fact '{factId}' resolves to different canonical metadata values for its members.");
                }

                foreach (FirmwareMetadataStructure structure in first.Value.Structures)
                {
                    if (!structures.TryAdd(structure.StructureId, structure))
                    {
                        throw Error(
                            $"{map.Path}.metadataSetIds",
                            $"Selected metadata structure id '{structure.StructureId}' is ambiguous.");
                    }
                }
            }

            result.Add(map.Document.MapId, structures);
        }

        return result;
    }

    private static Dictionary<string, FirmwareMapApplicability> NormalizeMapApplicabilities(
        IReadOnlyList<MapInput> maps,
        Dictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> structuresByMap)
    {
        var result = new Dictionary<string, FirmwareMapApplicability>(StringComparer.Ordinal);
        foreach (MapInput map in maps)
        {
            result.Add(
                map.Document.MapId,
                NormalizeApplicability(
                    map.Document.Applicability,
                    structuresByMap[map.Document.MapId],
                    $"{map.Path}.applicability"));
        }

        return result;
    }

    private static Dictionary<FirmwareMapFactKey, FirmwareFactApplicability> NormalizeAliasApplicabilities(
        IReadOnlyList<AliasDeclaration> aliases,
        Dictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> structuresByMap)
    {
        var result = new Dictionary<FirmwareMapFactKey, FirmwareFactApplicability>();
        foreach (AliasDeclaration alias in aliases)
        {
            FirmwareFactApplicability applicability = NormalizeFactApplicability(
                alias.Document.Applicability,
                structuresByMap[alias.TargetKey.MapId],
                $"{alias.Path}.applicability");
            if (!FirmwareFactApplicabilityRelations.IsSatisfiable(
                    applicability,
                    structuresByMap[alias.TargetKey.MapId]))
            {
                throw Error($"{alias.Path}.applicability", "Alias applicability is unsatisfiable.");
            }

            result.Add(alias.TargetKey, applicability);
        }

        return result;
    }

    private static void ValidatePhysicalAliases(
        IReadOnlyList<AliasDeclaration> aliases,
        Dictionary<string, FirmwareMapApplicability> mapApplicabilities,
        Dictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> structuresByMap,
        Dictionary<FirmwareMapFactKey, FirmwareFactApplicability> aliasApplicabilities)
    {
        foreach (AliasDeclaration alias in aliases.Where(static alias =>
                     alias.TargetKey.FactKind is FirmwareFactKind.RegionSet or FirmwareFactKind.MetadataSet))
        {
            var targetMapApplicability = FirmwareFactApplicability.FromMap(
                mapApplicabilities[alias.TargetKey.MapId]);
            FirmwareFactApplicability aliasApplicability = aliasApplicabilities[alias.TargetKey];
            IReadOnlyDictionary<string, FirmwareMetadataStructure> targetStructures =
                structuresByMap[alias.TargetKey.MapId];
            if (!FirmwareFactApplicabilityRelations.HasSameScope(
                    aliasApplicability,
                    targetMapApplicability,
                    targetStructures))
            {
                throw Error(
                    $"{alias.Path}.applicability",
                    "Physical fact alias applicability must equal the complete target map applicability.");
            }

            FirmwareFactApplicability sourceApplicability = aliasApplicabilities.TryGetValue(
                alias.SourceKey,
                out FirmwareFactApplicability? sourceAliasApplicability)
                ? sourceAliasApplicability
                : FirmwareFactApplicability.FromMap(mapApplicabilities[alias.SourceKey.MapId]);
            ValidateSharedPredicateStructures(
                alias,
                aliasApplicability,
                sourceApplicability,
                structuresByMap);
            if (!FirmwareFactApplicabilityRelations.IsContainedBy(
                    aliasApplicability,
                    sourceApplicability,
                    targetStructures))
            {
                throw Error(
                    $"{alias.Path}.applicability",
                    "Alias applicability is not contained by its source availability.");
            }
        }
    }

    private static FirmwareImageMap[] MaterializeMaps(
        IReadOnlyList<MapInput> maps,
        IReadOnlyDictionary<FirmwareMapFactKey, ResolvedFact<FirmwareRegionSet>> regions,
        IReadOnlyDictionary<FirmwareMapFactKey, ResolvedFact<FirmwareMetadataSet>> metadata,
        Dictionary<string, FirmwareMapApplicability> mapApplicabilities,
        Dictionary<FirmwareMapFactKey, FirmwareFactApplicability> aliasApplicabilities)
    {
        var result = new FirmwareImageMap[maps.Count];
        for (int index = 0; index < maps.Count; index++)
        {
            MapInput map = maps[index];
            FirmwareMapApplicability mapApplicability = mapApplicabilities[map.Document.MapId];
            var factApplicability = FirmwareFactApplicability.FromMap(mapApplicability);
            result[index] = TranslateInvariant(map.Path, () => new FirmwareImageMap(
                    map.Document.MapId,
                    map.Document.AddressSpaceId,
                    mapApplicability,
                    NormalizeCoveragePolicy(map.Document.CoveragePolicy, $"{map.Path}.coveragePolicy"),
                    MaterializeBindings(
                        map,
                        FirmwareFactKind.RegionSet,
                        map.Document.RegionSetIds,
                        map.Document.Applicability.MemberIds,
                        regions,
                        factApplicability,
                        aliasApplicabilities),
                    MaterializeBindings(
                        map,
                        FirmwareFactKind.MetadataSet,
                        map.Document.MetadataSetIds,
                        map.Document.Applicability.MemberIds,
                        metadata,
                        factApplicability,
                        aliasApplicabilities),
                    map.Document.EvidenceRefs));
        }

        return result;
    }

    private static IEnumerable<FirmwareMapFactBinding<TFact>> MaterializeBindings<TFact>(
        MapInput map,
        FirmwareFactKind kind,
        IReadOnlyList<string> factIds,
        IReadOnlyList<string> memberIds,
        IReadOnlyDictionary<FirmwareMapFactKey, ResolvedFact<TFact>> resolved,
        FirmwareFactApplicability factApplicability,
        IReadOnlyDictionary<FirmwareMapFactKey, FirmwareFactApplicability> aliasApplicabilities)
        where TFact : class, IFirmwareMapFact
    {
        foreach (string memberId in memberIds)
        {
            foreach (string factId in factIds)
            {
                var key = new FirmwareMapFactKey(memberId, map.Document.MapId, kind, factId);
                yield return CreateBinding(
                    resolved[key],
                    factApplicability,
                    aliasApplicabilities);
            }
        }
    }

    private static FirmwareMapFactBinding<TFact> CreateBinding<TFact>(
        ResolvedFact<TFact> resolved,
        FirmwareFactApplicability applicability,
        IReadOnlyDictionary<FirmwareMapFactKey, FirmwareFactApplicability> aliasApplicabilities)
        where TFact : class, IFirmwareMapFact
    {
        FirmwareFactAliasHop[] hops =
        [
            .. resolved.AliasChain.Select(alias => new FirmwareFactAliasHop(
                alias.Document.AliasId,
                alias.TargetKey,
                alias.SourceKey,
                aliasApplicabilities[alias.TargetKey],
                alias.Document.Reason,
                alias.Document.EvidenceRefs)),
        ];
        var provenance = new FirmwareFactProvenance(
            resolved.EffectiveKey,
            resolved.DirectSourceKey,
            hops,
            resolved.Value.EvidenceRefs);
        return new FirmwareMapFactBinding<TFact>(
            resolved.EffectiveKey,
            resolved.DirectSourceKey,
            resolved.Value.CanonicalFactId,
            resolved.Value,
            applicability,
            provenance);
    }

}
