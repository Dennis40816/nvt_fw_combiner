using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryCreateV2DpReplaceMemoryMapRows(
        string icId,
        long? baseCapacity,
        out IReadOnlyList<WorkbenchMemoryMapRow> rows)
    {
        if (!TryResolveDpPerspectiveDpReplaceDisplay(icId, baseCapacity, out DpPerspectiveDpReplaceDisplay? display))
        {
            rows = [];
            return false;
        }

        if (display.IsLengthPending)
        {
            rows =
            [
                new WorkbenchMemoryMapRow(
                    $"Base BIN length: {FormatV2DpReplaceCapacities(display)}",
                    "Base flash",
                    "Select",
                    "DP replacement",
                    $"Select a base BIN to compile the {icId} V2 DP Replace plan."),
            ];
            return true;
        }

        if (display.Composition is not { } composition)
        {
            bool unsupportedLength = IsV2DpReplaceUnsupportedLength(display);
            rows =
            [
                new WorkbenchMemoryMapRow(
                    FormatV2DpReplaceFailureLabel(display),
                    unsupportedLength ? "Base flash" : "Profile",
                    unsupportedLength ? "Replace" : "Blocked",
                    unsupportedLength ? "DP replacement" : "No output",
                    FormatV2DpReplaceIssues(display)),
            ];
            return true;
        }

        rows =
        [
            .. composition.Plan.OrderedOperations.Select(operation => new WorkbenchMemoryMapRow(
                FormatDisplayRange(operation.TargetRange),
                V2DpReplaceBeforeSource(operation),
                V2DpReplaceAction(operation),
                V2DpReplaceAfterSource(operation),
                FormatV2DpReplaceOperationDetail(operation))),
        ];
        return true;
    }

    private static bool TryGetV2DpReplaceMemoryRangeLabel(
        string icId,
        long? baseCapacity,
        out string rangeLabel)
    {
        if (!TryResolveDpPerspectiveDpReplaceDisplay(icId, baseCapacity, out DpPerspectiveDpReplaceDisplay? display))
        {
            rangeLabel = string.Empty;
            return false;
        }

        rangeLabel = display.IsLengthPending
            ? $"Base BIN length: {FormatV2DpReplaceCapacities(display)}"
            : display.Composition is { } composition
            ? FormatFullRange(composition.Plan.OutputInitialization.Capacity)
            : FormatV2DpReplaceFailureLabel(display);
        return true;
    }

    private static bool TryCreateV2DpReplaceCoverageSegments(
        string icId,
        long? baseCapacity,
        out IReadOnlyList<WorkbenchMemoryCoverageSegment> segments)
    {
        if (!TryResolveDpPerspectiveDpReplaceDisplay(icId, baseCapacity, out DpPerspectiveDpReplaceDisplay? display))
        {
            segments = [];
            return false;
        }

        if (display.IsLengthPending)
        {
            segments =
            [
                new WorkbenchMemoryCoverageSegment(
                    "Base length pending",
                    "DP base required",
                    $"Select a base BIN to resolve the actual DP Replace length ({FormatV2DpReplaceCapacities(display)}).",
                    "#CBD5E1",
                    280,
                    false),
            ];
            return true;
        }

        if (display.Composition is not { } composition)
        {
            bool unsupportedLength = IsV2DpReplaceUnsupportedLength(display);
            if (unsupportedLength && display.RequestedBaseCapacity is long requestedCapacity)
            {
                segments =
                [
                    new WorkbenchMemoryCoverageSegment(
                        $"Unsupported {FormatHexLength(requestedCapacity)}",
                        "Unsupported base",
                        $"This base BIN length is not approved for {icId} DP Replace; use {FormatV2DpReplaceCapacities(display)}.",
                        "#FCA5A5",
                        280,
                        false),
                ];
                return true;
            }

            segments =
            [
                new WorkbenchMemoryCoverageSegment(
                    FormatV2DpReplaceFailureLabel(display),
                    "Invalid profile",
                    FormatV2DpReplaceIssues(display),
                    "#FCA5A5",
                    280,
                    false),
            ];
            return true;
        }

        long capacity = composition.Plan.OutputInitialization.Capacity;
        CoverageSegment[] coverage =
        [
            new CoverageSegment(
                new ByteRange(0, capacity),
                "Base flash",
                "Kept from the original base firmware unless a V2 plan operation covers it.",
                "#CBD5E1",
                false),
        ];
        foreach (CompositionOperation operation in composition.Plan.OrderedOperations)
        {
            string sourceLabel = V2DpReplaceCoverageSourceLabel(operation);
            coverage = ApplyCoverageWrite(
                coverage,
                new CoverageSegment(
                    operation.TargetRange,
                    sourceLabel,
                    FormatV2DpReplaceCoverageDetail(operation),
                    CoverageFill(sourceLabel),
                    !string.Equals(sourceLabel, "Base flash", StringComparison.Ordinal)));
        }

        segments = ToWorkbenchCoverageSegments(coverage, capacity);
        return true;
    }

    private static bool TryGetV2DpReplaceInputDescription(string icId, out string description)
    {
        if (!TryResolveDpPerspectiveDpReplaceDisplay(icId, baseCapacity: null, out DpPerspectiveDpReplaceDisplay? display))
        {
            description = string.Empty;
            return false;
        }

        description = display.Issues.Count == 0
            ? $"Replacement DP is padded to the selected base BIN length ({FormatV2DpReplaceCapacities(display)}); only the original TP range is restored from base."
            : $"The V2 DP Replace profile is unavailable: {FormatV2DpReplaceIssues(display)}";
        return true;
    }

    private static string FormatV2DpReplaceCapacities(DpPerspectiveDpReplaceDisplay display)
    {
        return BuiltInV2Bundle.FormatCapacities(display.SupportedBaseCapacities);
    }

    private static string FormatV2DpReplaceFailureLabel(DpPerspectiveDpReplaceDisplay display)
    {
        return display.RequestedBaseCapacity is long requestedCapacity &&
            !display.SupportedBaseCapacities.Contains(requestedCapacity)
            ? $"Unsupported base BIN length {FormatHexLength(requestedCapacity)}"
            : "DP Replace profile unavailable";
    }

    private static bool IsV2DpReplaceUnsupportedLength(DpPerspectiveDpReplaceDisplay display)
    {
        return display.RequestedBaseCapacity is long requestedCapacity &&
            !display.SupportedBaseCapacities.Contains(requestedCapacity);
    }

    private static string FormatV2DpReplaceIssues(DpPerspectiveDpReplaceDisplay display)
    {
        return display.Issues.Count == 0
            ? "The V2 DP Replace profile did not produce an executable composition."
            : FormatIssues(display.Issues);
    }

    private static string V2DpReplaceBeforeSource(CompositionOperation operation)
    {
        return string.Equals(operation.SourceSpaceId, CompositionAddressSpaceIds.DpReplacement, StringComparison.Ordinal)
            ? "Base flash"
            : string.Equals(operation.SourceSpaceId, CompositionAddressSpaceIds.ReferenceBase, StringComparison.Ordinal)
            ? "DP replacement"
            : "Output image";
    }

    private static string V2DpReplaceAction(CompositionOperation operation)
    {
        return string.Equals(operation.SourceSpaceId, CompositionAddressSpaceIds.ReferenceBase, StringComparison.Ordinal)
            ? "Restore"
            : ActionLabel(operation.Kind);
    }

    private static string V2DpReplaceAfterSource(CompositionOperation operation)
    {
        return string.Equals(operation.SourceSpaceId, CompositionAddressSpaceIds.DpReplacement, StringComparison.Ordinal)
            ? "DP replacement"
            : string.Equals(operation.SourceSpaceId, CompositionAddressSpaceIds.ReferenceBase, StringComparison.Ordinal)
            ? "Base TP"
            : AddressSpaceLabel(operation.SourceSpaceId ?? operation.TargetSpaceId);
    }

    private static string FormatV2DpReplaceOperationDetail(CompositionOperation operation)
    {
        string sourceRange = operation.SourceRange is ByteRange range
            ? FormatDisplayRange(range)
            : "no source range";
        return $"Sequence {operation.Sequence}: {operation.Reason} Source {sourceRange} -> output {FormatDisplayRange(operation.TargetRange)}.";
    }

    private static string V2DpReplaceCoverageSourceLabel(CompositionOperation operation)
    {
        return string.Equals(operation.SourceSpaceId, CompositionAddressSpaceIds.DpReplacement, StringComparison.Ordinal)
            ? "Changed DP BIN"
            : string.Equals(operation.SourceSpaceId, CompositionAddressSpaceIds.ReferenceBase, StringComparison.Ordinal)
            ? "Base flash"
            : AddressSpaceLabel(operation.SourceSpaceId ?? operation.TargetSpaceId);
    }

    private static string FormatV2DpReplaceCoverageDetail(CompositionOperation operation)
    {
        return operation.SourceRange is ByteRange sourceRange
            ? $"{operation.Reason} Source {FormatDisplayRange(sourceRange)} -> output {FormatDisplayRange(operation.TargetRange)}."
            : $"{operation.Reason} Output {FormatDisplayRange(operation.TargetRange)}.";
    }
}

internal sealed record DpPerspectiveDpReplaceDisplay(
    long? RequestedBaseCapacity,
    IReadOnlyList<long> SupportedBaseCapacities,
    CompiledComposition? Composition,
    IReadOnlyList<CompositionIssue> Issues)
{
    internal bool IsLengthPending => RequestedBaseCapacity is null && Issues.Count == 0;
}
