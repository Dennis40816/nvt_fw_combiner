using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private const string SourceEnvelopeSeedInvalid = "profile.v2.source-envelope.seed-invalid";

    private sealed record SourceEnvelopeSeed(
        string SourceViewId,
        string TargetViewId,
        string OperationId);

    private static SourceEnvelopeSeed? TryAdmitSourceEnvelopeSeed(
        CompositionProfileDefinition profile,
        SourceEnvelopeExtent envelope,
        ResolvedInputSelection selection,
        List<CompositionIssue> issues)
    {
        InputArtifactProfileSpace[] boundInputs =
        [
            .. profile.Spaces.OfType<InputArtifactProfileSpace>().Where(space =>
                StringComparer.Ordinal.Equals(space.SlotId, envelope.SourceSlotId)),
        ];
        MutableCompositionProfileSpace output = AssertOutputSpace(profile);
        if (boundInputs.Length != 1 ||
            !selection.ActiveSlotIds.Contains(envelope.SourceSlotId) ||
            output.Capacity is not SourceSlotProfileCapacity sourceCapacity ||
            !StringComparer.Ordinal.Equals(sourceCapacity.SourceSlotId, envelope.SourceSlotId))
        {
            issues.Add(new CompositionIssue(
                SourceEnvelopeSeedInvalid,
                "The complete DP source and source-sized output must be selected exactly once."));
            return null;
        }

        CompositionProfileView[] sourceViews =
        [
            .. profile.Views.Where(view =>
                StringComparer.Ordinal.Equals(view.SpaceId, boundInputs[0].SpaceId) &&
                view.Selector is MapRegionViewSelector selector &&
                StringComparer.Ordinal.Equals(selector.RegionId, envelope.RootRegionId)),
        ];
        CompositionProfileView[] targetViews =
        [
            .. profile.Views.Where(view =>
                StringComparer.Ordinal.Equals(view.SpaceId, output.SpaceId) &&
                view.Selector is MapRegionViewSelector selector &&
                StringComparer.Ordinal.Equals(selector.RegionId, envelope.RootRegionId)),
        ];
        if (sourceViews.Length != 1 || targetViews.Length != 1 ||
            !selection.ActiveViewIds.Contains(sourceViews[0].ViewId) ||
            !selection.ActiveViewIds.Contains(targetViews[0].ViewId))
        {
            issues.Add(new CompositionIssue(
                SourceEnvelopeSeedInvalid,
                "The full-container source and target views must be unique active map-root views."));
            return null;
        }

        CompositionOperationDefinition[] activeOperations =
        [
            .. profile.Operations.Where(operation =>
                selection.ActiveOperationIds.Contains(operation.OperationId)),
        ];
        CompositionOperationDefinition[] seeds =
        [
            .. activeOperations.Where(operation =>
                operation.Kind == CompositionOperationKind.CopyRange &&
                operation.OverlapPolicy == OverlapPolicy.Reject &&
                StringComparer.Ordinal.Equals(operation.SourceViewId, sourceViews[0].ViewId) &&
                StringComparer.Ordinal.Equals(operation.TargetViewId, targetViews[0].ViewId)),
        ];
        if (seeds.Length != 1 ||
            activeOperations.Any(operation =>
                operation != seeds[0] &&
                (operation.Sequence <= seeds[0].Sequence ||
                 StringComparer.Ordinal.Equals(operation.SourceViewId, sourceViews[0].ViewId) ||
                 StringComparer.Ordinal.Equals(operation.TargetViewId, targetViews[0].ViewId))))
        {
            issues.Add(new CompositionIssue(
                SourceEnvelopeSeedInvalid,
                "Exactly one first ordered CopyRange may use the complete DP source and output views."));
            return null;
        }

        return new SourceEnvelopeSeed(sourceViews[0].ViewId, targetViews[0].ViewId, seeds[0].OperationId);
    }

    private static void ValidateSourceEnvelopeOutputWrites(
        CompositionProfileDefinition profile,
        FirmwareImageMap map,
        SourceEnvelopeSeed seed,
        IReadOnlyList<CompositionOperation> operations,
        List<CompositionIssue> issues)
    {
        string outputSpaceId = AssertOutputSpace(profile).SpaceId;
        foreach (CompositionOperation operation in operations.Where(operation =>
                     !StringComparer.Ordinal.Equals(operation.OperationId, seed.OperationId) &&
                     StringComparer.Ordinal.Equals(operation.TargetSpaceId, outputSpaceId)))
        {
            foreach (ByteRange writeRange in operation.DeclaredWriteRanges)
            {
                if (map.Regions.Any(region =>
                    region.Owner == FirmwareRegionOwner.Tp && region.Range.Contains(writeRange)))
                {
                    continue;
                }

                issues.Add(new CompositionIssue(
                    "profile.v2.source-envelope.non-tp-output-write",
                    $"Operation '{operation.OperationId}' writes final output range {writeRange} outside existing TP regions.",
                    operation.OperationId));
            }
        }
    }
}
