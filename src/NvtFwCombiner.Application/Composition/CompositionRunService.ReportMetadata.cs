using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static MetadataInspectionSnapshot? CreateReportMetadataSnapshot(
        CompositionRunRequest request,
        IReadOnlyDictionary<string, byte[]> inputBytes)
    {
        ResolvedMetadataPlan? plan = request.ResolvedCapability?.MetadataPlan;
        return plan is null || !plan.Entries.Any(static entry =>
                entry.Definition.Purposes.Contains(
                    MetadataReferencePurpose.ReportClassification))
            ? null
            : FirmwareMetadataInspector.Inspect(
                new MetadataInspectionRequest(
                    plan,
                    authoringRevision: 0,
                    CreateReportMetadataArtifacts(plan, inputBytes),
                    request.CompiledComposition.V2Details.Provenance.Context is
                        MapBoundV2CompilationContext context
                            ? context.ResolvedMap.TopologySelection
                            : null));
    }

    private static IEnumerable<FirmwareArtifactPayload>
        CreateReportMetadataArtifacts(
            ResolvedMetadataPlan plan,
            IReadOnlyDictionary<string, byte[]> inputBytes)
    {
        Dictionary<string, byte[]> artifacts = new(inputBytes, StringComparer.Ordinal);
        foreach (MetadataPlanEntry entry in plan.Entries.Select(
                     static resolved => resolved.Definition))
        {
            if (inputBytes.TryGetValue(entry.SlotId, out byte[]? bytes))
            {
                _ = artifacts.TryAdd(entry.SpaceId, bytes);
            }
        }

        return artifacts.Select(static pair =>
            new FirmwareArtifactPayload(pair.Key, pair.Value));
    }

    private static bool TryFindActiveTpHeaderField(
        MetadataInspectionSnapshot? snapshot,
        ByteRange sourceRange,
        out FirmwareMetadataField? field)
    {
        field = null;
        if (snapshot is null)
        {
            return false;
        }

        List<FirmwareMetadataField> matches = [];
        foreach (MetadataInspectionResult result in snapshot.Results)
        {
            MetadataPlanEntry entry = result.PlanEntry.Definition;
            FirmwareResolvedMetadataStructure? structure = result.Resolution?.Resolved;
            if (!entry.Purposes.Contains(MetadataReferencePurpose.ReportClassification) ||
                structure is null ||
                structure.StructureDefinition.Definition.StructureKind !=
                    FirmwareMetadataStructureKind.TpFlashHeader)
            {
                continue;
            }

            long structureStart = structure.LocatorOutcome.ResolvedRange.Range.Start;
            foreach (FirmwareResolvedMetadataField candidate in structure.Fields)
            {
                FirmwareMetadataField candidateField = candidate.Field;
                if (candidate.Applicability != FirmwareMetadataFieldApplicabilityState.Active ||
                    !entry.TargetReferences.Any(target =>
                        entry.StructureDefinition.Definition
                            .ReferenceTargetContainsField(target, candidateField)))
                {
                    continue;
                }

                var absoluteRange = new ByteRange(
                    checked(structureStart + candidateField.Range.Start),
                    candidateField.Range.Length);
                if (absoluteRange.Contains(sourceRange))
                {
                    matches.Add(candidateField);
                }
            }
        }

        if (matches.Count != 1)
        {
            return false;
        }

        field = matches[0];
        return true;
    }
}
