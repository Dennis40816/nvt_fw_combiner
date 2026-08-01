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

    /// <summary>Resolves only physical maps named by an already trusted workflow profile binding.</summary>
    internal FirmwareMapResolutionResult ResolveMapWithin(
        FirmwareMapResolutionInputs inputs,
        IReadOnlySet<string> candidateMapIds)
    {
        ArgumentNullException.ThrowIfNull(candidateMapIds);
        ArgumentOutOfRangeException.ThrowIfZero(candidateMapIds.Count, nameof(candidateMapIds));

        return ResolveMapCore(
            inputs,
            candidateMapIds,
            requiredMetadataStructureIds: null);
    }

    /// <summary>Resolves one trusted profile map using only metadata required by map-selection predicates.</summary>
    internal FirmwareMapResolutionResult ResolveMapWithinForSelection(
        FirmwareMapResolutionInputs inputs,
        IReadOnlySet<string> candidateMapIds)
    {
        ArgumentNullException.ThrowIfNull(candidateMapIds);
        ArgumentOutOfRangeException.ThrowIfZero(candidateMapIds.Count, nameof(candidateMapIds));

        return ResolveMapCore(
            inputs,
            candidateMapIds,
            new HashSet<string>(StringComparer.Ordinal));
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
        var pendingRequirements = new HashSet<FirmwareMapResolutionPendingRequirement>();

        foreach (FirmwareImageMap map in _imageMaps)
        {
            if (candidateMapIds is not null && !candidateMapIds.Contains(map.MapId))
            {
                continue;
            }

            CandidateResolution candidate = EvaluateCandidate(
                map,
                inputs,
                requiredMetadataStructureIds);
            switch (candidate.Result)
            {
                case FirmwareApplicabilityResult.Match:
                    uniqueCandidates.Add(candidate.ResolvedMap!);
                    break;
                case FirmwareApplicabilityResult.Pending:
                    pendingRequirements.UnionWith(candidate.PendingRequirements);
                    break;
                case FirmwareApplicabilityResult.NoMatch:
                    break;
                default:
                    throw new InvalidOperationException("Unknown candidate map resolution result.");
            }
        }

        return uniqueCandidates.Count >= 2
            ? FirmwareMapResolutionResult.Rejected(FirmwareMapResolutionRejectionKind.AmbiguousMaps)
            : pendingRequirements.Count != 0
            ? FirmwareMapResolutionResult.Pending(pendingRequirements)
            : uniqueCandidates.Count == 1
            ? FirmwareMapResolutionResult.Unique(uniqueCandidates[0])
            : FirmwareMapResolutionResult.Rejected(FirmwareMapResolutionRejectionKind.NoMatchingMap);
    }

    private CandidateResolution EvaluateCandidate(
        FirmwareImageMap map,
        FirmwareMapResolutionInputs inputs,
        IReadOnlySet<string>? requiredMetadataStructureIds)
    {
        FirmwareMapApplicabilityEvaluation staticEvaluation = map.Applicability.Evaluate(inputs);
        if (staticEvaluation.Result == FirmwareApplicabilityResult.NoMatch)
        {
            return CandidateResolution.NoMatch();
        }

        var pendingRequirements = new HashSet<FirmwareMapResolutionPendingRequirement>();
        foreach (FirmwareMapPendingRequirementKind requirement in staticEvaluation.PendingRequirements)
        {
            switch (requirement)
            {
                case FirmwareMapPendingRequirementKind.RequestedTopologyMissing:
                    _ = pendingRequirements.Add(new FirmwareMapResolutionPendingRequirement(
                        FirmwareMapResolutionPendingKind.RequestedTopologyMissing));
                    break;
                case FirmwareMapPendingRequirementKind.CommonFirmwareCategoryDerivationUnavailable:
                    _ = pendingRequirements.Add(new FirmwareMapResolutionPendingRequirement(
                        FirmwareMapResolutionPendingKind.CommonFirmwareCategoryDerivationUnavailable));
                    break;
                case FirmwareMapPendingRequirementKind.MetadataResolutionRequired:
                    break;
                default:
                    throw new InvalidOperationException("Unknown static map pending requirement.");
            }
        }

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
            return CandidateResolution.NoMatch();
        }

        foreach (FirmwareMetadataStructureResolution resolution in structureResolutions.Where(static resolution =>
                     resolution.Status == FirmwareMetadataStructureResolutionStatus.Pending))
        {
            _ = pendingRequirements.Add(new FirmwareMapResolutionPendingRequirement(
                FirmwareMapResolutionPendingKind.ArtifactMissing,
                resolution.ArtifactBindingId));
        }

        if (pendingRequirements.Count != 0)
        {
            return CandidateResolution.Pending(pendingRequirements);
        }

        FirmwareResolvedMetadataStructure[] resolvedStructures =
        [
            .. structureResolutions.Select(static resolution => resolution.Resolved!),
        ];
        Dictionary<string, FirmwareResolvedMetadataStructure> structuresById = resolvedStructures.ToDictionary(
            static structure => structure.DecodedStructure.MetadataStructureId,
            StringComparer.Ordinal);
        var predicateOutcomes = new List<FirmwareMetadataPredicateOutcome>();
        foreach (FirmwareMetadataPredicate predicate in map.Applicability.MetadataPredicates)
        {
            FirmwareResolvedMetadataStructure structure = structuresById[predicate.MetadataStructureId];
            var fields = structure.DecodedStructure.Facts.ToDictionary(
                static fact => fact.FieldId,
                static fact => fact.Value,
                StringComparer.Ordinal);
            FirmwareMetadataPredicateOutcome outcome = predicate.Evaluate(fields);
            if (outcome.Result != FirmwarePredicateResult.Match)
            {
                return CandidateResolution.NoMatch();
            }

            predicateOutcomes.Add(outcome);
        }

        return CandidateResolution.Match(new ResolvedFirmwareImageMap(
            ResolvedMapConstructionToken,
            this,
            inputs,
            map,
            resolvedStructures,
            predicateOutcomes,
            metadataStructures));
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

    private sealed record CandidateResolution(
        FirmwareApplicabilityResult Result,
        ResolvedFirmwareImageMap? ResolvedMap,
        IReadOnlyList<FirmwareMapResolutionPendingRequirement> PendingRequirements)
    {
        internal static CandidateResolution NoMatch()
        {
            return new CandidateResolution(FirmwareApplicabilityResult.NoMatch, null, []);
        }

        internal static CandidateResolution Pending(
            IEnumerable<FirmwareMapResolutionPendingRequirement> pendingRequirements)
        {
            ArgumentNullException.ThrowIfNull(pendingRequirements);
            return new CandidateResolution(
                FirmwareApplicabilityResult.Pending,
                null,
                [.. pendingRequirements]);
        }

        internal static CandidateResolution Match(ResolvedFirmwareImageMap resolvedMap)
        {
            ArgumentNullException.ThrowIfNull(resolvedMap);
            return new CandidateResolution(FirmwareApplicabilityResult.Match, resolvedMap, []);
        }
    }
}
