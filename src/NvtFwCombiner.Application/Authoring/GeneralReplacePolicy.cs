// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>
/// Pure policy owner for all-IC General Replace. The wiring wave replaces legacy General Replace
/// admission with this owner; this policy neither compiles operations nor executes processors.
/// </summary>
public static class GeneralReplacePolicy
{
    /// <summary>
    /// Evaluates slot-relative source reads and absolute output writes against declared IC facts and the
    /// immutable base length. Whole files may be larger than their selected input range. No padding or
    /// truncation is authorized. The default choice is Recompute; a missing rule resolves to WriteAsIs.
    /// </summary>
    public static GeneralReplacePolicyResult Evaluate(FirmwareImageMap icFacts,
        CompiledRegionAccessContract access, long baseImageLength, IntegrityRecomputeFacts recomputeFacts,
        IEnumerable<GeneralReplaceSegment> segments, IReadOnlyDictionary<string, long> sourceLengths,
        IntegrityRecomputeChoice choice = IntegrityRecomputeChoice.Recompute)
    {
        ArgumentNullException.ThrowIfNull(icFacts);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentOutOfRangeException.ThrowIfNegative(baseImageLength);
        ArgumentNullException.ThrowIfNull(recomputeFacts);
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(sourceLengths);
        if (!Enum.IsDefined(choice))
        {
            throw new ArgumentOutOfRangeException(nameof(choice));
        }

        GeneralReplaceSegment[] snapshot = [.. segments];
        if (snapshot.Any(static segment => segment is null))
        {
            throw new ArgumentException("Segments cannot contain null.", nameof(segments));
        }

        List<CompositionIssue> issues = [];
        List<(GeneralReplaceSegment Segment, FirmwareAddressedRange Output)> outputs = [];
        foreach (GeneralReplaceSegment segment in snapshot)
        {
            ValidateSegment(icFacts, baseImageLength, sourceLengths, segment, issues, outputs);
        }

        ValidateOverlap(outputs, issues);
        AddRegionWarnings(icFacts, GetProtectedRegions(icFacts, access), outputs, issues);
        ByteRange[] ranges = [.. outputs.Select(static output => output.Output.Range)];
        MemoryLayoutFactSet<IntegrityStorageHit> storageHits = recomputeFacts.FindStorageHits(ranges);
        MemoryLayoutFactSet<IntegrityWriteRangeHit> writeHits = recomputeFacts.FindWriteRangeHits(ranges);
        IntegrityRecomputeChoice effectiveChoice = recomputeFacts.Availability == IntegrityRecomputeAvailability.NoRecomputeRule
            ? IntegrityRecomputeChoice.WriteAsIs
            : choice;
        AddIntegrityIssues(icFacts, baseImageLength, recomputeFacts, effectiveChoice, outputs, writeHits, issues);
        List<FirmwareAddressedRange> declared = [.. outputs.Select(static output => output.Output)];
        if (effectiveChoice == IntegrityRecomputeChoice.Recompute &&
            recomputeFacts.Availability == IntegrityRecomputeAvailability.Declared)
        {
            declared.AddRange(recomputeFacts.WriteRanges.Values.Select(range =>
                new FirmwareAddressedRange(icFacts.AddressSpaceId, range.Range)));
        }

        return new(icFacts.AddressSpaceId, recomputeFacts, effectiveChoice, issues, declared.Distinct(), storageHits, writeHits);
    }

    private static void ValidateSegment(FirmwareImageMap facts, long imageLength,
        IReadOnlyDictionary<string, long> sourceLengths, GeneralReplaceSegment segment,
        List<CompositionIssue> issues,
        List<(GeneralReplaceSegment Segment, FirmwareAddressedRange Output)> outputs)
    {
        if (segment.InputRange.Length <= 0 || segment.InputRange.Length != segment.Length ||
            !sourceLengths.TryGetValue(segment.SourceSlotId, out long sourceLength) ||
            sourceLength < segment.InputRange.EndExclusive)
        {
            issues.Add(new(CompositionPlanningIssueCodes.GeneralReplaceSourceLengthMismatch,
                $"Segment '{segment.SegmentId}' source '{segment.SourceSlotId}' {segment.InputRange} cannot supply exactly {segment.Length} bytes.",
                segment.SegmentId));
        }

        if (segment.GetOutputRange() is not { } output)
        {
            issues.Add(new(CompositionPlanningIssueCodes.GeneralReplaceSegmentRangeInvalid,
                $"Segment '{segment.SegmentId}' has invalid output coordinates in '{segment.OutputAddressSpaceId}': start {segment.OutputStart}, length {segment.Length}.",
                segment.SegmentId));
            return;
        }

        bool inAddressSpace = StringComparer.Ordinal.Equals(output.AddressSpaceId, facts.AddressSpaceId) &&
            output.Range.EndExclusive <= facts.CapacityBytes;
        if (!inAddressSpace)
        {
            issues.Add(new(CompositionPlanningIssueCodes.GeneralReplaceAddressSpaceOutOfBounds,
                $"Segment '{segment.SegmentId}' output {Format(output)} is outside '{facts.AddressSpaceId}' [0, {facts.CapacityBytes}).",
                segment.SegmentId));
        }
        if (output.Range.EndExclusive > imageLength)
        {
            issues.Add(new(CompositionPlanningIssueCodes.GeneralReplaceImageOutOfBounds,
                $"Segment '{segment.SegmentId}' output {Format(output)} exceeds the base image [0, {imageLength}).",
                segment.SegmentId));
        }
        if (inAddressSpace && output.Range.EndExclusive <= imageLength)
        {
            outputs.Add((segment, output));
        }
    }

    private static void ValidateOverlap(
        List<(GeneralReplaceSegment Segment, FirmwareAddressedRange Output)> outputs, List<CompositionIssue> issues)
    {
        (GeneralReplaceSegment Segment, FirmwareAddressedRange Output)[] ordered =
            [.. outputs.OrderBy(static output => output.Output.Range.Start)];
        for (int left = 0; left < ordered.Length; left++)
        {
            for (int right = left + 1; right < ordered.Length &&
                ordered[right].Output.Range.Start < ordered[left].Output.Range.EndExclusive; right++)
            {
                ByteRange overlap = ordered[left].Output.Range.Intersect(ordered[right].Output.Range)!.Value;
                issues.Add(new(CompositionPlanningIssueCodes.GeneralReplaceSegmentOverlap,
                    $"Segments '{ordered[left].Segment.SegmentId}' and '{ordered[right].Segment.SegmentId}' overlap in '{ordered[left].Output.AddressSpaceId}' {overlap}.",
                    ordered[right].Segment.SegmentId));
            }
        }
    }

    /// <summary>
    /// Regions that a built-in rule may not write: a physically forbidden region, or a region whose own
    /// profile access is hidden or read-only. <see cref="CompiledRegionAccessContract.GetReadOnlyRanges"/> is not
    /// used here: it lists bytes that no allowed target can reach, and a writable parent region covers its
    /// protected children, so it would report none of them.
    /// </summary>
    private static FirmwareRegion[] GetProtectedRegions(FirmwareImageMap facts, CompiledRegionAccessContract access)
    {
        HashSet<string> profileProtected = [.. access.Requirements
            .Where(static rule => rule.Access is RegionAccessKind.Hidden or RegionAccessKind.ReadOnly)
            .Select(static rule => rule.GoverningRegionChain[^1].RegionId)];
        return [.. facts.Regions.Where(region => region.WriteConstraint == FirmwareWriteConstraint.Forbidden ||
            profileProtected.Contains(region.RegionId))];
    }

    private static void AddRegionWarnings(FirmwareImageMap facts, FirmwareRegion[] protectedRegions,
        List<(GeneralReplaceSegment Segment, FirmwareAddressedRange Output)> outputs, List<CompositionIssue> issues)
    {
        foreach ((GeneralReplaceSegment segment, FirmwareAddressedRange output) in outputs)
        {
            foreach (FirmwareRegion region in facts.Regions)
            {
                if (output.Range.Intersect(region.Range) is { } overlap)
                {
                    issues.Add(Warning(CompositionPlanningIssueCodes.GeneralReplaceNamedRegionHit,
                        $"Segment '{segment.SegmentId}' touches region '{region.RegionId}' in '{output.AddressSpaceId}' {overlap}.", segment.SegmentId));
                }
            }
            foreach (FirmwareRegion protection in protectedRegions)
            {
                if (output.Range.Intersect(protection.Range) is { } overlap)
                {
                    issues.Add(Warning(CompositionPlanningIssueCodes.GeneralReplaceProtectedRegionHit,
                        $"Segment '{segment.SegmentId}' touches protected region '{protection.RegionId}' in '{output.AddressSpaceId}' {overlap}.",
                        segment.SegmentId));
                }
            }
        }
    }

    private static void AddIntegrityIssues(FirmwareImageMap facts, long imageLength,
        IntegrityRecomputeFacts recompute, IntegrityRecomputeChoice choice,
        List<(GeneralReplaceSegment Segment, FirmwareAddressedRange Output)> outputs,
        MemoryLayoutFactSet<IntegrityWriteRangeHit> hits, List<CompositionIssue> issues)
    {
        switch (recompute.Availability)
        {
            case IntegrityRecomputeAvailability.Unknown:
                // Unknown facts expose unavailable write ranges, so absence of a hit cannot establish safety.
                // Preserve the original diagnostic on RecomputeFacts.Issue; never downgrade it to no-rule.
                issues.Add(new(CompositionPlanningIssueCodes.GeneralReplaceRecomputeUnknown,
                    $"Recompute writes in '{facts.AddressSpaceId}' could not be resolved for the base image."));
                return;
            case IntegrityRecomputeAvailability.NoRecomputeRule:
                issues.Add(Warning(CompositionPlanningIssueCodes.GeneralReplaceNoRecomputeRule,
                    $"No recompute rule is declared for '{facts.AddressSpaceId}' [0, {imageLength})."));
                break;
            case IntegrityRecomputeAvailability.Declared:
                if (choice == IntegrityRecomputeChoice.Recompute)
                {
                    foreach (IntegrityRecomputeWriteRange write in recompute.WriteRanges.Values)
                    {
                        if (write.Range.EndExclusive > imageLength || write.Range.EndExclusive > facts.CapacityBytes)
                        {
                            issues.Add(new(CompositionPlanningIssueCodes.ReplaceGeneralPostbuildWriteRangeMissing,
                                $"Recompute section '{write.SectionId}' in '{facts.AddressSpaceId}' {write.Range} exceeds the image or address space."));
                        }
                    }
                    foreach (IntegrityWriteRangeHit hit in hits.Values)
                    {
                        issues.Add(Warning(CompositionPlanningIssueCodes.GeneralReplaceRecomputedRangeHit,
                            $"Recompute section '{hit.Range.SectionId}' may overwrite user bytes in '{facts.AddressSpaceId}' {hit.Overlap}.",
                            hit.Range.SectionId));
                    }
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(recompute));
        }

        if (choice != IntegrityRecomputeChoice.WriteAsIs)
        {
            return;
        }
        foreach (IntegrityRecomputeWriteRange write in recompute.WriteRanges.Values)
        {
            issues.Add(Warning(CompositionPlanningIssueCodes.GeneralReplaceWriteAsIs,
                $"Write as is skips recompute of section '{write.SectionId}' in '{facts.AddressSpaceId}' {write.Range}.",
                $"{facts.AddressSpaceId}:{write.SectionId}:{write.Range}"));
        }
        if (recompute.WriteRanges.Status == MemoryLayoutFactStatus.Unavailable)
        {
            foreach ((GeneralReplaceSegment segment, FirmwareAddressedRange output) in outputs)
            {
                issues.Add(Warning(CompositionPlanningIssueCodes.GeneralReplaceWriteAsIs,
                    $"Segment '{segment.SegmentId}' writes {Format(output)} as is; recompute ranges are not declared.", segment.SegmentId));
            }
        }
    }

    private static CompositionIssue Warning(string code, string message, string? operationId = null)
    {
        return new(code, message, operationId, CompositionIssueSeverity.Warning);
    }

    private static string Format(FirmwareAddressedRange range)
    {
        return $"'{range.AddressSpaceId}' {range.Range}";
    }
}
