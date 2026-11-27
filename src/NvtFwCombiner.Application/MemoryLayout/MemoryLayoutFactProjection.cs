// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.MemoryLayout;

internal sealed record MemoryLayoutProjectedFacts(
    MemoryLayoutFactSet<MemoryLayoutReadOnlyRange> ReadOnlyRanges,
    MemoryLayoutFactSet<MemoryLayoutVersionLocation> DpVersionLocations,
    MemoryLayoutFactSet<CompositionOperation> Steps);

/// <summary>Read-only translation of compiled access, operations and already-resolved metadata facts.</summary>
internal static class MemoryLayoutFactProjection
{
    internal static MemoryLayoutProjectedFacts Create(
        ResolvedCapability capability, ActiveSessionSnapshot authoring,
        FirmwareImageMap? map, string addressSpaceId, long capacity,
        Func<string, ByteRange, IReadOnlyList<ByteRange>>? findOutputLocations = null,
        CompiledComposition? compiledOverlay = null)
    {
        return new(
            ProjectReadOnlyRanges(capability.CompiledComposition,
                map, addressSpaceId, capacity),
            ProjectDpVersionLocations(capability, authoring, addressSpaceId, capacity, findOutputLocations),
            compiledOverlay is null
                ? MemoryLayoutFactSet<CompositionOperation>.Unavailable(
                    MemoryLayoutFactUnavailableReason.CompiledPlanNotResolved)
                : MemoryLayoutFactSet<CompositionOperation>.Known(compiledOverlay.Plan.OrderedOperations));
    }

    internal static MemoryLayoutFactSet<MemoryLayoutReadOnlyRange> ProjectReadOnlyRanges(
        CompiledComposition composition, FirmwareImageMap? map, string addressSpaceId, long capacity)
    {
        // This context retains AB Merge access for layout authority, not active Replace authoring.
        if (composition.V2Details.Provenance.Context is RuntimeReferenceBankReplaceV2CompilationContext)
        {
            return MemoryLayoutFactSet<MemoryLayoutReadOnlyRange>.Unavailable(
                MemoryLayoutFactUnavailableReason.ActiveAuthoringPolicyNotResolved);
        }
        CompiledRegionAccessContract contract = composition.V2Details.RegionAccessContract;
        if (map is null)
        {
            return MemoryLayoutFactSet<MemoryLayoutReadOnlyRange>.Unavailable(
                MemoryLayoutFactUnavailableReason.NoPhysicalMap);
        }

        if (capacity > map.CapacityBytes)
        {
            return MemoryLayoutFactSet<MemoryLayoutReadOnlyRange>.Unavailable(
                MemoryLayoutFactUnavailableReason.OutputExtentNotCoveredByAccessPolicy);
        }

        List<MemoryLayoutReadOnlyRange> ranges = [];
        foreach (CompiledReadOnlyRange declaration in contract.GetReadOnlyRanges(map))
        {
            long end = Math.Min(declaration.Range.Range.EndExclusive, capacity);
            if (end > declaration.Range.Range.Start)
            {
                ranges.Add(new MemoryLayoutReadOnlyRange(declaration,
                    new FirmwareAddressedRange(addressSpaceId,
                        ByteRange.FromStartEndExclusive(declaration.Range.Range.Start, end))));
            }
        }
        return MemoryLayoutFactSet<MemoryLayoutReadOnlyRange>.Known(ranges
            .OrderBy(static item => item.OutputRange.Range.Start)
            .ThenBy(static item => item.RegionId, StringComparer.Ordinal));
    }

    private static MemoryLayoutFactSet<MemoryLayoutVersionLocation> ProjectDpVersionLocations(
        ResolvedCapability capability, ActiveSessionSnapshot authoring, string addressSpaceId, long capacity,
        Func<string, ByteRange, IReadOnlyList<ByteRange>>? findOutputLocations)
    {
        (ResolvedMetadataPlanEntry Entry, IReadOnlyList<FirmwareMetadataField> Fields)[] declarations =
            [.. capability.MetadataPlan.Entries.Select(entry => (
                Entry: entry, Fields: DpcmiMetadataProjector.GetVersionFields(entry.Definition)))
                .Where(static declaration => declaration.Fields.Count != 0)];
        MemoryLayoutFactSet<MemoryLayoutVersionLocation> carriers = ProjectDpVersionCarrierLocations(
            capability.CompiledComposition, addressSpaceId, capacity);
        if (declarations.Length == 0 || (carriers.Status == MemoryLayoutFactStatus.Unavailable &&
            carriers.UnavailableReason != MemoryLayoutFactUnavailableReason.NotDeclared))
        {
            return carriers;
        }
        if (authoring.MetadataInspection is not { } inspection ||
            !ReferenceEquals(authoring.ExactCapability, capability))
        {
            return MemoryLayoutFactSet<MemoryLayoutVersionLocation>.Unavailable(
                MemoryLayoutFactUnavailableReason.MetadataNotResolved);
        }

        List<MemoryLayoutVersionLocation> locations = [.. carriers.Values];
        foreach ((ResolvedMetadataPlanEntry entry, IReadOnlyList<FirmwareMetadataField> fields) in declarations)
        {
            MetadataInspectionResult? result = inspection.Results.FirstOrDefault(result =>
                ReferenceEquals(result.PlanEntry, entry));
            if (result is not { State: MetadataInspectionState.Value, Resolution.Resolved: { } resolved })
            {
                return MemoryLayoutFactSet<MemoryLayoutVersionLocation>.Unavailable(
                    MemoryLayoutFactUnavailableReason.MetadataNotResolved);
            }
            foreach (FirmwareMetadataField field in fields)
            {
                var sourceRange = new ByteRange(
                    checked(resolved.LocatorOutcome.ResolvedRange.Range.Start + field.Range.Start),
                    field.Range.Length);
                IReadOnlyList<ByteRange> outputRanges = findOutputLocations?.Invoke(
                    entry.Definition.SpaceId, sourceRange) ?? [];
                if (outputRanges.Count == 0)
                {
                    return MemoryLayoutFactSet<MemoryLayoutVersionLocation>.Unavailable(
                        MemoryLayoutFactUnavailableReason.OutputLocationNotResolved);
                }
                locations.AddRange(outputRanges.Select(range => new MemoryLayoutMetadataVersionLocation(
                    entry.Definition.BindingId, entry.Definition.StructureDefinition.StructureId,
                    field, new FirmwareAddressedRange(addressSpaceId, range))));
            }
        }
        return MemoryLayoutFactSet<MemoryLayoutVersionLocation>.Known(locations);
    }

    internal static MemoryLayoutFactSet<MemoryLayoutVersionLocation> ProjectDpVersionCarrierLocations(
        CompiledComposition composition, string addressSpaceId, long capacity)
    {
        IReadOnlyList<(CompiledInputVersionKind Kind, FirmwareRegion? Region)> declarations =
            CompiledInputArtifactObservationService.GetDeclaredDpVersionCarriers(composition);
        if (declarations.Count == 0)
        {
            return MemoryLayoutFactSet<MemoryLayoutVersionLocation>.Unavailable(
                MemoryLayoutFactUnavailableReason.NotDeclared);
        }
        var output = new ByteRange(0, capacity);
        return declarations.Any(declaration => declaration.Region is null || !output.Contains(declaration.Region.Range))
            ? MemoryLayoutFactSet<MemoryLayoutVersionLocation>.Unavailable(
                MemoryLayoutFactUnavailableReason.OutputLocationNotResolved)
            : MemoryLayoutFactSet<MemoryLayoutVersionLocation>.Known(declarations.Select(declaration =>
                new MemoryLayoutVersionCarrierLocation(declaration.Kind, declaration.Region!,
                    new FirmwareAddressedRange(addressSpaceId, declaration.Region!.Range))));
    }

}
