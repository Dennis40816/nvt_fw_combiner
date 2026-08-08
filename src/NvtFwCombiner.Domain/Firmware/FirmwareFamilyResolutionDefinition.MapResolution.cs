namespace NvtFwCombiner.Domain.Firmware;

public sealed partial class FirmwareFamilyResolutionDefinition
{
    /// <summary>Resolves exactly one candidate map from immutable selections and private artifact snapshots.</summary>
    public FirmwareMapResolutionResult ResolveMap(FirmwareMapResolutionInputs inputs)
    {
        return ResolveMapCore(
            inputs,
            candidateMapIds: null,
            requiredMetadataStructureIds: null);
    }

    /// <summary>
    /// Resolves one trusted profile map using only selection metadata and the
    /// exact metadata structures required by that profile.
    /// </summary>
    internal FirmwareMapResolutionResult ResolveMapWithinForProfile(
        FirmwareMapResolutionInputs inputs,
        IReadOnlySet<string> candidateMapIds,
        IReadOnlySet<string> requiredMetadataStructureIds)
    {
        ArgumentNullException.ThrowIfNull(candidateMapIds);
        ArgumentOutOfRangeException.ThrowIfZero(candidateMapIds.Count, nameof(candidateMapIds));
        ArgumentNullException.ThrowIfNull(requiredMetadataStructureIds);

        return ResolveMapCore(
            inputs,
            candidateMapIds,
            requiredMetadataStructureIds);
    }

    private FirmwareMapResolutionResult ResolveMapCore(
        FirmwareMapResolutionInputs inputs,
        IReadOnlySet<string>? candidateMapIds,
        IReadOnlySet<string>? requiredMetadataStructureIds)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var uniqueCandidates = new List<ResolvedFirmwareImageMap>();
        bool hasPendingCandidate = false;

        foreach (FirmwareImageMap map in _imageMaps)
        {
            if (candidateMapIds is not null && !candidateMapIds.Contains(map.MapId))
            {
                continue;
            }

            FirmwareApplicabilityResult result = EvaluateCandidate(
                map,
                inputs,
                requiredMetadataStructureIds,
                out ResolvedFirmwareImageMap? resolvedMap);
            switch (result)
            {
                case FirmwareApplicabilityResult.Match:
                    uniqueCandidates.Add(resolvedMap!);
                    break;
                case FirmwareApplicabilityResult.Pending:
                    hasPendingCandidate = true;
                    break;
                case FirmwareApplicabilityResult.NoMatch:
                    break;
                default:
                    throw new InvalidOperationException("Unknown candidate map resolution result.");
            }
        }

        return uniqueCandidates.Count >= 2
            ? FirmwareMapResolutionResult.Rejected(FirmwareMapResolutionRejectionKind.AmbiguousMaps)
            : hasPendingCandidate
            ? FirmwareMapResolutionResult.Pending()
            : uniqueCandidates.Count == 1
            ? FirmwareMapResolutionResult.Unique(uniqueCandidates[0])
            : FirmwareMapResolutionResult.Rejected(FirmwareMapResolutionRejectionKind.NoMatchingMap);
    }

    private FirmwareApplicabilityResult EvaluateCandidate(
        FirmwareImageMap map,
        FirmwareMapResolutionInputs inputs,
        IReadOnlySet<string>? requiredMetadataStructureIds,
        out ResolvedFirmwareImageMap? resolvedMap)
    {
        resolvedMap = null;
        bool isPending = false;
        FirmwareMapApplicability applicability = map.Applicability;
        if (!applicability.MemberIds.Contains(inputs.MemberId, StringComparer.Ordinal) ||
            !applicability.ModeIds.Contains(inputs.ModeId, StringComparer.Ordinal) ||
            inputs.CapacityBytes != applicability.CapacityBytes)
        {
            return FirmwareApplicabilityResult.NoMatch;
        }

        if (applicability.TopologyRequirement.Kind != TopologyRequirementKind.None)
        {
            if (inputs.RequestedTopology is null)
            {
                isPending = true;
            }
            else if (!applicability.TopologyRequirement.Matches(inputs.RequestedTopology))
            {
                return FirmwareApplicabilityResult.NoMatch;
            }
        }

        if (applicability.CommonFirmwareCategoryIds.Count != 0)
        {
            isPending = true;
        }

        // Metadata predicates become comparable only after candidate-scoped structure resolution.
        IReadOnlyList<FirmwareMetadataStructure> metadataStructures =
            requiredMetadataStructureIds is null
                ? GetStructuresForMap(map.MapId)
                : GetRequiredMetadataStructuresForMap(map, requiredMetadataStructureIds);
        FirmwareMetadataStructureResolution[] structureResolutions =
        [
            .. metadataStructures
                .Select(structure => ResolveMetadataStructure(map.MapId, structure.StructureId, inputs)),
        ];
        if (structureResolutions.Any(static resolution =>
                resolution.Status == FirmwareMetadataStructureResolutionStatus.Rejected))
        {
            return FirmwareApplicabilityResult.NoMatch;
        }

        if (structureResolutions.Any(static resolution =>
                resolution.Status == FirmwareMetadataStructureResolutionStatus.Pending))
        {
            isPending = true;
        }

        if (isPending)
        {
            return FirmwareApplicabilityResult.Pending;
        }

        FirmwareResolvedMetadataStructure[] resolvedStructures =
        [
            .. structureResolutions.Select(static resolution => resolution.Resolved!),
        ];
        Dictionary<string, FirmwareResolvedMetadataStructure> structuresById = resolvedStructures.ToDictionary(
            static structure => structure.DecodedStructure.MetadataStructureId,
            StringComparer.Ordinal);
        var predicateOutcomes = new List<FirmwareMetadataPredicateOutcome>();
        foreach (FirmwareMetadataPredicate predicate in applicability.MetadataPredicates)
        {
            FirmwareResolvedMetadataStructure structure = structuresById[predicate.MetadataStructureId];
            var fields = structure.DecodedStructure.Facts.ToDictionary(
                static fact => fact.FieldId,
                static fact => fact.Value,
                StringComparer.Ordinal);
            FirmwareMetadataPredicateOutcome outcome = predicate.Evaluate(fields);
            if (outcome.Result != FirmwarePredicateResult.Match)
            {
                return FirmwareApplicabilityResult.NoMatch;
            }

            predicateOutcomes.Add(outcome);
        }

        resolvedMap = new ResolvedFirmwareImageMap(
            ResolvedMapConstructionToken,
            this,
            inputs,
            map,
            resolvedStructures,
            predicateOutcomes);
        return FirmwareApplicabilityResult.Match;
    }

    private System.Collections.ObjectModel.ReadOnlyCollection<FirmwareMetadataStructure>
        GetRequiredMetadataStructuresForMap(
        FirmwareImageMap map,
        IReadOnlySet<string> requiredMetadataStructureIds)
    {
        IReadOnlyList<FirmwareMetadataStructure> structures = GetStructuresForMap(map.MapId);
        var structuresById = structures.ToDictionary(
            static structure => structure.StructureId,
            StringComparer.Ordinal);
        var selectedIds = new HashSet<string>(
            map.Applicability.MetadataPredicates.Select(
                static predicate => predicate.MetadataStructureId),
            StringComparer.Ordinal);
        selectedIds.UnionWith(requiredMetadataStructureIds);

        var pendingIds = new Stack<string>(selectedIds);
        while (pendingIds.TryPop(out string? structureId))
        {
            if (structuresById.TryGetValue(structureId, out FirmwareMetadataStructure? structure) &&
                structure.Locator is FirmwareMetadataFieldSelectedLocator selected &&
                selectedIds.Add(selected.PrerequisiteStructureId))
            {
                pendingIds.Push(selected.PrerequisiteStructureId);
            }
        }

        return Array.AsReadOnly(
        [
            .. structures.Where(structure => selectedIds.Contains(structure.StructureId)),
        ]);
    }

}
