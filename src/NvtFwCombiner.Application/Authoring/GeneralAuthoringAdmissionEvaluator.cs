using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>One immutable General occupancy and resource-admission result.</summary>
public sealed record GeneralAuthoringAdmissionResult
{
    private readonly GeneralOccupancySegment[] _occupancySegments;
    private readonly GeneralAuthoringAdmissionIssue[] _issues;

    /// <summary>Creates one defensively copied admission result.</summary>
    public GeneralAuthoringAdmissionResult(
        GeneralResourceLimits? effectiveLimits,
        IEnumerable<GeneralOccupancySegment> occupancySegments,
        IEnumerable<GeneralAuthoringAdmissionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(occupancySegments);
        ArgumentNullException.ThrowIfNull(issues);
        _occupancySegments = [.. occupancySegments];
        _issues = [.. issues];
        if (_occupancySegments.Any(static segment => segment is null) ||
            _issues.Any(static issue => issue is null))
        {
            throw new ArgumentException(
                "General admission occupancy and issues cannot contain null.");
        }

        EffectiveLimits = effectiveLimits;
        OccupancySegments = Array.AsReadOnly(_occupancySegments);
        Issues = Array.AsReadOnly(_issues);
    }

    /// <summary>Resolved limits used for this admission, or null after resolution failure.</summary>
    public GeneralResourceLimits? EffectiveLimits { get; }

    /// <summary>All authored writer segments in canonical target/range/id order.</summary>
    public IReadOnlyList<GeneralOccupancySegment> OccupancySegments { get; }

    /// <summary>All deterministic typed blockers in stable issue-id order.</summary>
    public IReadOnlyList<GeneralAuthoringAdmissionIssue> Issues { get; }

    /// <summary>Whether compilation may proceed.</summary>
    public bool IsAdmitted => EffectiveLimits is not null && Issues.Count == 0;

    /// <summary>Projects all typed blockers into shared composition report issues.</summary>
    public IReadOnlyList<CompositionIssue> ToCompositionIssues()
    {
        return
        [
            .. Issues.Select(static issue => issue.ToCompositionIssue()),
        ];
    }
}

/// <summary>
/// Canonical Application-owned admission seam shared by General Merge, General Replace, CLI,
/// Saved Rules, memory projection, and compilation adapters.
/// </summary>
public static class GeneralAuthoringAdmission
{
    /// <summary>Resolves resource limits and validates authored target occupancy.</summary>
    public static GeneralAuthoringAdmissionResult Evaluate(
        GeneralMappingDraftState draft,
        IReadOnlyDictionary<string, long> targetAddressSpaceCapacities,
        IEnumerable<GeneralInputResource> inputResources,
        GeneralResourceLimits technicalLimits,
        GeneralResourceLimits trustedParentLimits,
        GeneralResourceLimits? savedRuleLimits)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(targetAddressSpaceCapacities);
        ArgumentNullException.ThrowIfNull(inputResources);

        GeneralResourceResolutionResult resolution = GeneralResourceLimitResolver.Resolve(
            technicalLimits,
            trustedParentLimits,
            savedRuleLimits);
        List<GeneralAuthoringAdmissionIssue> issues = [.. resolution.Issues];
        GeneralOccupancySegment[] occupancy =
        [
            .. draft.Rows
                .Select(static row => new GeneralOccupancySegment(
                    row.MappingId,
                    row.Source.Kind,
                    row.TargetAddressSpaceId,
                    row.TargetRange))
                .OrderBy(static segment => segment.TargetAddressSpaceId, StringComparer.Ordinal)
                .ThenBy(static segment => segment.TargetRange.Start)
                .ThenBy(static segment => segment.TargetRange.EndExclusive)
                .ThenBy(static segment => segment.MappingId, StringComparer.Ordinal),
        ];

        if (resolution.EffectiveLimits is not { } effective)
        {
            return CreateResult(null, occupancy, issues);
        }

        Dictionary<string, GeneralInputResource> resources = BuildResourceIndex(
            inputResources,
            issues);
        ValidateResourceUse(draft, targetAddressSpaceCapacities, resources, effective, issues);
        ValidateOccupancy(occupancy, issues);
        return CreateResult(effective, occupancy, issues);
    }

    private static Dictionary<string, GeneralInputResource> BuildResourceIndex(
        IEnumerable<GeneralInputResource> inputResources,
        List<GeneralAuthoringAdmissionIssue> issues)
    {
        Dictionary<string, GeneralInputResource> resources = new(StringComparer.Ordinal);
        foreach (GeneralInputResource resource in inputResources)
        {
            if (!resources.TryAdd(resource.SlotId, resource))
            {
                issues.Add(CreateIssue(
                    GeneralAuthoringIssueCodes.InputResourceDuplicate,
                    resource.SlotId,
                    $"General input resource '{resource.SlotId}' is declared more than once.",
                    slotId: resource.SlotId));
            }
        }

        return resources;
    }

    private static void ValidateResourceUse(
        GeneralMappingDraftState draft,
        IReadOnlyDictionary<string, long> targetCapacities,
        IReadOnlyDictionary<string, GeneralInputResource> resources,
        GeneralResourceLimits limits,
        List<GeneralAuthoringAdmissionIssue> issues)
    {
        if (draft.Rows.Count > limits.MaximumMappingCount)
        {
            issues.Add(CreateIssue(
                GeneralAuthoringIssueCodes.MappingCountExceeded,
                "draft",
                $"General mapping count {draft.Rows.Count} exceeds the effective maximum {limits.MaximumMappingCount}."));
        }

        long totalWriteBytes = 0;
        bool totalOverflow = false;
        foreach (GeneralMappingDraftRow row in draft.Rows)
        {
            try
            {
                totalWriteBytes = checked(totalWriteBytes + row.TargetRange.Length);
            }
            catch (OverflowException)
            {
                totalOverflow = true;
            }

            if (!targetCapacities.TryGetValue(
                    row.TargetAddressSpaceId,
                    out long targetCapacity) ||
                targetCapacity < 0 ||
                row.TargetRange.EndExclusive > targetCapacity)
            {
                issues.Add(CreateIssue(
                    GeneralAuthoringIssueCodes.TargetOutOfBounds,
                    row.MappingId,
                    $"General mapping '{row.MappingId}' target {FormatRange(row.TargetRange)} is outside address space '{row.TargetAddressSpaceId}'.",
                    [row.MappingId]));
            }

            if (row.Source.Kind == GeneralMappingSourceKind.FileArtifact)
            {
                ValidateFileResource(row, resources, limits, issues);
            }
            else
            {
                long requiredBytes = GetInlineMaterializationLength(row);
                if (requiredBytes > limits.MaximumSafeMaterializationBytes)
                {
                    issues.Add(CreateIssue(
                        GeneralAuthoringIssueCodes.InlineMaterializationExceeded,
                        row.MappingId,
                        $"General inline mapping '{row.MappingId}' requires {requiredBytes} bytes, exceeding the safe materialization maximum {limits.MaximumSafeMaterializationBytes}.",
                        [row.MappingId]));
                }
            }
        }

        if (totalOverflow)
        {
            issues.Add(CreateIssue(
                GeneralAuthoringIssueCodes.TotalWriteBytesOverflow,
                "draft",
                "General total authored write bytes overflowed the supported address size."));
        }
        else if (totalWriteBytes > limits.MaximumTotalWriteBytes)
        {
            issues.Add(CreateIssue(
                GeneralAuthoringIssueCodes.TotalWriteBytesExceeded,
                "draft",
                $"General authored writes total {totalWriteBytes} bytes, exceeding the effective maximum {limits.MaximumTotalWriteBytes}."));
        }
    }

    private static void ValidateFileResource(
        GeneralMappingDraftRow row,
        IReadOnlyDictionary<string, GeneralInputResource> resources,
        GeneralResourceLimits limits,
        List<GeneralAuthoringAdmissionIssue> issues)
    {
        if (!resources.TryGetValue(row.MappingId, out GeneralInputResource? resource))
        {
            issues.Add(CreateIssue(
                GeneralAuthoringIssueCodes.InputResourceMissing,
                row.MappingId,
                $"General file mapping '{row.MappingId}' has no observed input resource.",
                [row.MappingId],
                slotId: row.MappingId));
            return;
        }

        if (resource.LengthBytes > limits.MaximumFileBytes)
        {
            issues.Add(CreateIssue(
                GeneralAuthoringIssueCodes.FileSizeExceeded,
                row.MappingId,
                $"General input '{row.MappingId}' is {resource.LengthBytes} bytes, exceeding the effective whole-file maximum {limits.MaximumFileBytes}.",
                [row.MappingId],
                slotId: row.MappingId));
        }

        if (row.SourceRange.EndExclusive > resource.LengthBytes)
        {
            issues.Add(CreateIssue(
                GeneralAuthoringIssueCodes.SourceOutOfBounds,
                row.MappingId,
                $"General mapping '{row.MappingId}' source {FormatRange(row.SourceRange)} exceeds its {resource.LengthBytes}-byte input.",
                [row.MappingId],
                slotId: row.MappingId));
        }

        if (limits.TryGetSlot(row.MappingId, out GeneralSlotLengthLimits? slotLimits) &&
            !slotLimits!.Accepts(resource.LengthBytes))
        {
            issues.Add(CreateIssue(
                GeneralAuthoringIssueCodes.SlotLengthRejected,
                row.MappingId,
                $"General input '{row.MappingId}' length {resource.LengthBytes} is outside its resolved slot admission.",
                [row.MappingId],
                slotId: row.MappingId));
        }
    }

    private static void ValidateOccupancy(
        GeneralOccupancySegment[] occupancy,
        List<GeneralAuthoringAdmissionIssue> issues)
    {
        for (int leftIndex = 0; leftIndex < occupancy.Length; leftIndex++)
        {
            GeneralOccupancySegment left = occupancy[leftIndex];
            for (int rightIndex = leftIndex + 1; rightIndex < occupancy.Length; rightIndex++)
            {
                GeneralOccupancySegment right = occupancy[rightIndex];
                if (!StringComparer.Ordinal.Equals(
                        left.TargetAddressSpaceId,
                        right.TargetAddressSpaceId))
                {
                    break;
                }

                if (right.TargetRange.Start >= left.TargetRange.EndExclusive)
                {
                    break;
                }

                ByteRange? intersection = left.TargetRange.Intersect(right.TargetRange);
                if (intersection is not { } exact)
                {
                    continue;
                }

                string[] ids =
                [
                    .. new[] { left.MappingId, right.MappingId }
                        .Order(StringComparer.Ordinal),
                ];
                string identity =
                    $"{ids[0]}:{ids[1]}:{left.TargetAddressSpaceId}:{exact.Start:X}-{exact.EndExclusive:X}";
                issues.Add(new GeneralAuthoringAdmissionIssue(
                    GeneralAuthoringIssueCodes.TargetIntersection,
                    $"{GeneralAuthoringIssueCodes.TargetIntersection}:{identity}",
                    $"General mappings '{ids[0]}' and '{ids[1]}' intersect in '{left.TargetAddressSpaceId}' at {FormatRange(exact)}.",
                    ids,
                    exact));
            }
        }
    }

    private static long GetInlineMaterializationLength(GeneralMappingDraftRow row)
    {
        if (row.Source.Kind == GeneralMappingSourceKind.HexFill)
        {
            return row.TargetRange.Length;
        }

        int hexadecimalCharacterCount = 0;
        foreach (char character in row.Source.InlineValue!)
        {
            if (!char.IsWhiteSpace(character) &&
                character is not '-' and not ',' and not '_')
            {
                hexadecimalCharacterCount++;
            }
        }

        long payloadBytes = (hexadecimalCharacterCount + 1L) / 2L;
        return Math.Max(row.TargetRange.Length, payloadBytes);
    }

    private static GeneralAuthoringAdmissionIssue CreateIssue(
        string code,
        string identity,
        string message,
        IEnumerable<string>? mappingIds = null,
        string? slotId = null)
    {
        return new GeneralAuthoringAdmissionIssue(
            code,
            $"{code}:{identity}",
            message,
            mappingIds,
            slotId: slotId);
    }

    private static GeneralAuthoringAdmissionResult CreateResult(
        GeneralResourceLimits? effectiveLimits,
        GeneralOccupancySegment[] occupancy,
        IEnumerable<GeneralAuthoringAdmissionIssue> issues)
    {
        GeneralAuthoringAdmissionIssue[] orderedIssues =
        [
            .. issues
                .GroupBy(static issue => issue.IssueId, StringComparer.Ordinal)
                .Select(static group => group.First())
                .OrderBy(static issue => issue.IssueId, StringComparer.Ordinal),
        ];
        return new GeneralAuthoringAdmissionResult(
            effectiveLimits,
            Array.AsReadOnly(occupancy),
            Array.AsReadOnly(orderedIssues));
    }

    private static string FormatRange(ByteRange range)
    {
        return $"[0x{range.Start:X}, 0x{range.EndExclusive:X})";
    }
}
