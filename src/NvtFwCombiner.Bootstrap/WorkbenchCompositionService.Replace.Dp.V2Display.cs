using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static WorkbenchMemoryDisplay? CreateV2DpReplaceMemoryDisplay(string icId, long? baseCapacity)
    {
        if (!TryResolveBuiltInV2DpReplaceDisplay(icId, baseCapacity, out BuiltInV2DpReplaceDisplay? display))
        {
            return null;
        }

        if (display.RequestedBaseCapacity is null && display.Issues.Count == 0)
        {
            string capacities = FormatV2DpReplaceCapacities(display);
            return CreateMessageDisplay(
                $"Reference FlashCode length: {capacities}",
                ($"Reference FlashCode length: {capacities}", "Reference FlashCode", "Select", "DP replacement", $"Select a complete Standard/Normal Merge FlashCode for {icId} to compile the V2 DP Replace plan."),
                ("Reference length pending", "Reference FlashCode required", $"Select a complete Standard/Normal Merge FlashCode to resolve the actual DP Replace length ({capacities}).", "#CBD5E1"));
        }

        if (display.Composition is not { } composition)
        {
            long requestedCapacity = display.RequestedBaseCapacity.GetValueOrDefault();
            bool unsupportedLength = display.RequestedBaseCapacity.HasValue &&
                !display.SupportedBaseCapacities.Contains(requestedCapacity);
            string failureLabel = unsupportedLength
                ? $"Unsupported Reference FlashCode length {FormatHexLength(requestedCapacity)}"
                : "DP Replace profile unavailable";
            string issues = FormatV2DpReplaceIssues(display);
            return CreateMessageDisplay(
                failureLabel,
                (failureLabel, unsupportedLength ? "Base flash" : "Profile", unsupportedLength ? "Replace" : "Blocked", unsupportedLength ? "DP replacement" : "No output", issues),
                unsupportedLength
                    ? ($"Unsupported {FormatHexLength(requestedCapacity)}", "Unsupported reference", $"This Reference FlashCode length is not approved for {icId} DP Replace; use {FormatV2DpReplaceCapacities(display)}.", "#FCA5A5")
                    : (failureLabel, "Invalid profile", issues, "#FCA5A5"));
        }

        List<WorkbenchMemoryMapRow> rows = [];
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
            (string before, string action, string after, string sourceLabel, bool isChanged) =
                operation.SourceSpaceId switch
                {
                    CompositionAddressSpaceIds.DpReplacement =>
                        ("Base flash", ActionLabel(operation.Kind), "DP replacement", "Changed DP BIN", true),
                    CompositionAddressSpaceIds.LdReplacement =>
                        ("Base flash", ActionLabel(operation.Kind), "LDC replacement", "Changed LDC BIN", true),
                    CompositionAddressSpaceIds.ReferenceBase =>
                        ("DP replacement", "Restore", "Base TP", "Base flash", false),
                    _ =>
                        ("Output image", ActionLabel(operation.Kind), AddressSpaceLabel(operation.SourceSpaceId ?? operation.TargetSpaceId), AddressSpaceLabel(operation.SourceSpaceId ?? operation.TargetSpaceId), true),
                };
            string targetRange = FormatDisplayRange(operation.TargetRange);
            string? sourceRange = operation.SourceRange is ByteRange range ? FormatDisplayRange(range) : null;
            rows.Add(new WorkbenchMemoryMapRow(
                targetRange,
                before,
                action,
                after,
                $"Sequence {operation.Sequence}: {operation.Reason} Source {sourceRange ?? "no source range"} -> output {targetRange}."));
            coverage = ApplyCoverageWrite(
                coverage,
                new CoverageSegment(
                    operation.TargetRange,
                    sourceLabel,
                    sourceRange is null
                        ? $"{operation.Reason} Output {targetRange}."
                        : $"{operation.Reason} Source {sourceRange} -> output {targetRange}.",
                    CoverageFill(sourceLabel),
                    isChanged));
        }

        return new WorkbenchMemoryDisplay(
            FormatFullRange(capacity),
            rows,
            ToWorkbenchCoverageSegments(coverage, capacity));
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

    private static string FormatV2DpReplaceIssues(BuiltInV2DpReplaceDisplay display)
    {
        return display.Issues.Count == 0
            ? "The V2 DP Replace profile did not produce an executable composition."
            : FormatIssues(display.Issues);
    }
}

internal sealed record BuiltInV2DpReplaceDisplay(
    long? RequestedBaseCapacity,
    IReadOnlyList<long> SupportedBaseCapacities,
    CompiledComposition? Composition,
    IReadOnlyList<CompositionIssue> Issues);
