using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryCreateV2DpReplaceMemoryMapRows(
        string icId,
        long? baseCapacity,
        out IReadOnlyList<WorkbenchMemoryMapRow> rows)
    {
        if (!TryResolveBuiltInV2DpReplaceDisplay(icId, baseCapacity, out BuiltInV2DpReplaceDisplay? display))
        {
            rows = [];
            return false;
        }

        if (display.IsLengthPending)
        {
            rows =
            [
                new WorkbenchMemoryMapRow(
                    $"Reference FlashCode length: {FormatV2DpReplaceCapacities(display)}",
                    "Reference FlashCode",
                    "Select",
                    "DP replacement",
                    $"Select a complete Standard/Normal Merge FlashCode for {icId} to compile the V2 DP Replace plan."),
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
        if (!TryResolveBuiltInV2DpReplaceDisplay(icId, baseCapacity, out BuiltInV2DpReplaceDisplay? display))
        {
            rangeLabel = string.Empty;
            return false;
        }

        rangeLabel = display.IsLengthPending
            ? $"Reference FlashCode length: {FormatV2DpReplaceCapacities(display)}"
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
        if (!TryResolveBuiltInV2DpReplaceDisplay(icId, baseCapacity, out BuiltInV2DpReplaceDisplay? display))
        {
            segments = [];
            return false;
        }

        if (display.IsLengthPending)
        {
            segments =
            [
                new WorkbenchMemoryCoverageSegment(
                    "Reference length pending",
                    "Reference FlashCode required",
                    $"Select a complete Standard/Normal Merge FlashCode to resolve the actual DP Replace length ({FormatV2DpReplaceCapacities(display)}).",
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
                        "Unsupported reference",
                        $"This Reference FlashCode length is not approved for {icId} DP Replace; use {FormatV2DpReplaceCapacities(display)}.",
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
        if (!TryResolveBuiltInV2DpReplaceDisplay(icId, baseCapacity: null, out BuiltInV2DpReplaceDisplay? display))
        {
            description = string.Empty;
            return false;
        }

        if (display.Issues.Count != 0 ||
            display.SupportedBaseCapacities.Count == 0 ||
            !TryResolveBuiltInV2DpReplaceDisplay(
                icId,
                display.SupportedBaseCapacities[0],
                out BuiltInV2DpReplaceDisplay? resolved) ||
            resolved.Composition is not { } composition)
        {
            description = $"The V2 DP Replace profile is unavailable: {FormatV2DpReplaceIssues(display)}";
            return true;
        }

        AddressSpace replacement = composition.Plan.AddressSpaces.Single(static space =>
            space.AddressSpaceId == CompositionAddressSpaceIds.DpReplacement);
        bool restoresReference = composition.Plan.OrderedOperations.Any(static operation =>
            string.Equals(operation.SourceSpaceId, CompositionAddressSpaceIds.ReferenceBase, StringComparison.Ordinal));
        description = replacement.InputPaddingByte is not null && restoresReference
            ? $"Use a DP/FlashCode-shaped BIN no larger than the selected Reference FlashCode ({FormatV2DpReplaceCapacities(display)}); shorter input is zero-padded and the original TP range is restored from the reference."
            : $"Use a same-IC DP/FlashCode BIN containing the complete declared DP range ({FormatHexLength(replacement.Length)} bytes; expected outer length {FormatV2DpReplaceCapacities(display)}). Only declared DP ranges are copied; every other byte stays from Reference FlashCode.";
        return true;
    }

    private static string FormatV2DpReplaceCapacities(BuiltInV2DpReplaceDisplay display)
    {
        return BuiltInV2Bundle.FormatCapacities(display.SupportedBaseCapacities);
    }

    private static string FormatV2DpReplaceFailureLabel(BuiltInV2DpReplaceDisplay display)
    {
        return display.RequestedBaseCapacity is long requestedCapacity &&
            !display.SupportedBaseCapacities.Contains(requestedCapacity)
            ? $"Unsupported Reference FlashCode length {FormatHexLength(requestedCapacity)}"
            : "DP Replace profile unavailable";
    }

    private static bool IsV2DpReplaceUnsupportedLength(BuiltInV2DpReplaceDisplay display)
    {
        return display.RequestedBaseCapacity is long requestedCapacity &&
            !display.SupportedBaseCapacities.Contains(requestedCapacity);
    }

    private static string FormatV2DpReplaceIssues(BuiltInV2DpReplaceDisplay display)
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

internal sealed record BuiltInV2DpReplaceDisplay(
    long? RequestedBaseCapacity,
    IReadOnlyList<long> SupportedBaseCapacities,
    CompiledComposition? Composition,
    IReadOnlyList<CompositionIssue> Issues)
{
    internal bool IsLengthPending => RequestedBaseCapacity is null && Issues.Count == 0;
}
