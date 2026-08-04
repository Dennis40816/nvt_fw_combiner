using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>One immutable General occupancy and resource-admission result.</summary>
public sealed record GeneralAuthoringAdmissionResult
{
    private readonly GeneralOccupancySegment[] _occupancySegments;
    private readonly GeneralAuthoringAdmissionIssue[] _issues;
    private readonly GeneralInputResource[] _inputResources;

    /// <summary>Creates one defensively copied admission result.</summary>
    public GeneralAuthoringAdmissionResult(
        GeneralMappingDraftState draft,
        string trustedParentId,
        string? savedRuleId,
        GeneralResourceLimits? effectiveLimits,
        IEnumerable<GeneralInputResource> inputResources,
        IEnumerable<GeneralOccupancySegment> occupancySegments,
        IEnumerable<GeneralAuthoringAdmissionIssue> issues,
        SavedRuleExecutionIdentity? savedRule = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedParentId);
        ArgumentNullException.ThrowIfNull(occupancySegments);
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(inputResources);
        _inputResources = [.. inputResources];
        _occupancySegments = [.. occupancySegments];
        _issues = [.. issues];
        if (_inputResources.Any(static resource => resource is null) ||
            _occupancySegments.Any(static segment => segment is null) ||
            _issues.Any(static issue => issue is null))
        {
            throw new ArgumentException(
                "General admission resources, occupancy, and issues cannot contain null.");
        }

        Draft = draft;
        TrustedParentId = trustedParentId;
        SavedRuleId = string.IsNullOrWhiteSpace(savedRuleId)
            ? null
            : savedRuleId;
        if (savedRule is not null &&
            !StringComparer.Ordinal.Equals(SavedRuleId, savedRule.RuleId))
        {
            throw new ArgumentException(
                "Saved Rule id and exact execution identity must agree.",
                nameof(savedRule));
        }

        SavedRule = savedRule;
        EffectiveLimits = effectiveLimits;
        OccupancySegments = Array.AsReadOnly(_occupancySegments);
        Issues = Array.AsReadOnly(_issues);
        InputResources = Array.AsReadOnly(_inputResources);
    }

    /// <summary>Resolved limits used for this admission, or null after resolution failure.</summary>
    public GeneralResourceLimits? EffectiveLimits { get; }

    /// <summary>Exact immutable draft evaluated by this result.</summary>
    public GeneralMappingDraftState Draft { get; }

    /// <summary>Exact Parent policy used by this result.</summary>
    public string TrustedParentId { get; }

    /// <summary>Optional Saved Rule narrowing identity.</summary>
    public string? SavedRuleId { get; }

    /// <summary>Exact content-identified Saved Rule revision, when available.</summary>
    public SavedRuleExecutionIdentity? SavedRule { get; }

    /// <summary>All authored writer segments in canonical target/range/id order.</summary>
    public IReadOnlyList<GeneralOccupancySegment> OccupancySegments { get; }

    /// <summary>Observed path-free whole-file resources used by compilation.</summary>
    public IReadOnlyList<GeneralInputResource> InputResources { get; }

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

    /// <summary>Returns the exact admitted draft or fails closed for a blocked result.</summary>
    public GeneralMappingDraftState RequireAdmittedDraft()
    {
        return IsAdmitted
            ? Draft
            : throw new InvalidOperationException(
                "A blocked General admission result cannot be compiled.");
    }

    /// <summary>Projects path-free admission provenance for reports and Preview identity.</summary>
    public GeneralAuthoringAdmissionSummary ToSummary()
    {
        return new GeneralAuthoringAdmissionSummary(
            TrustedParentId,
            SavedRuleId,
            EffectiveLimits,
            InputResources,
            OccupancySegments,
            Issues,
            SavedRule);
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
        GeneralTrustedParentResourcePolicy trustedParent,
        GeneralSavedRuleResourcePolicy? savedRule)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(targetAddressSpaceCapacities);
        ArgumentNullException.ThrowIfNull(inputResources);
        GeneralInputResource[] observedResources = [.. inputResources];

        GeneralResourceResolutionResult resolution = GeneralResourceLimitResolver.Resolve(
            technicalLimits,
            trustedParent,
            savedRule);
        List<GeneralAuthoringAdmissionIssue> issues = [.. resolution.Issues];
        SavedRuleExecutionIdentity? exactSavedRule = savedRule?.ExecutionIdentity;
        string? admittedSavedRuleId = savedRule?.RuleId;
        if (savedRule is not null &&
            (exactSavedRule is null ||
             savedRule.Lifecycle is null ||
             !SavedRuleLifecycle.IsExecutionAuthorized(
                 exactSavedRule,
                 savedRule.Lifecycle)))
        {
            issues.Add(CreateIssue(
                GeneralAuthoringIssueCodes.SavedRuleExecutionNotTrustedPublished,
                savedRule.RuleId,
                "Saved Rule execution requires the exact approved and evidenced immutable Trusted Catalog publication; an imported or edited Draft cannot Preview or Build."));
            exactSavedRule = null;
            admittedSavedRuleId = null;
        }
        else if (exactSavedRule is not null &&
            exactSavedRule.Parent != trustedParent.ParentIdentity)
        {
            issues.Add(CreateIssue(
                GeneralAuthoringIssueCodes.SavedRuleParentMismatch,
                exactSavedRule.RuleId,
                "Saved Rule exact Parent identity does not match the independently resolved Trusted Parent."));
            exactSavedRule = null;
            admittedSavedRuleId = null;
        }

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
            return CreateResult(
                draft,
                trustedParent.ParentId,
                admittedSavedRuleId,
                exactSavedRule,
                null,
                observedResources,
                occupancy,
                issues);
        }

        Dictionary<string, GeneralInputResource> resources = BuildResourceIndex(
            observedResources,
            issues);
        ValidateResourceUse(draft, targetAddressSpaceCapacities, resources, effective, issues);
        ValidateOccupancy(occupancy, issues);
        return CreateResult(
            draft,
            trustedParent.ParentId,
            admittedSavedRuleId,
            exactSavedRule,
            effective,
            observedResources,
            occupancy,
            issues);
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
                bool parsed = GeneralInlineSourceCodec.TryMeasure(
                    row.Source.InlineValue,
                    out long payloadBytes);
                long requiredBytes = row.Source.Kind == GeneralMappingSourceKind.HexFill
                    ? row.TargetRange.Length
                    : Math.Max(row.TargetRange.Length, payloadBytes);
                if (!parsed)
                {
                    issues.Add(CreateIssue(
                        GeneralAuthoringIssueCodes.InlineHexInvalid,
                        row.MappingId,
                        $"General inline mapping '{row.MappingId}' must contain complete hexadecimal byte pairs.",
                        [row.MappingId]));
                }
                else if (requiredBytes > limits.MaximumSafeMaterializationBytes)
                {
                    issues.Add(CreateIssue(
                        GeneralAuthoringIssueCodes.InlineMaterializationExceeded,
                        row.MappingId,
                        $"General inline mapping '{row.MappingId}' requires {requiredBytes} bytes, exceeding the safe materialization maximum {limits.MaximumSafeMaterializationBytes}.",
                        [row.MappingId]));
                }
                else if (row.Source.Kind == GeneralMappingSourceKind.HexOverwrite &&
                         payloadBytes != row.TargetRange.Length)
                {
                    issues.Add(CreateIssue(
                        GeneralAuthoringIssueCodes.InlineOverwriteLengthMismatch,
                        row.MappingId,
                        $"General overwrite '{row.MappingId}' supplies {payloadBytes} byte(s) for a {row.TargetRange.Length}-byte target range.",
                        [row.MappingId]));
                }
                else if (row.Source.Kind == GeneralMappingSourceKind.HexFill &&
                         payloadBytes != 1)
                {
                    issues.Add(CreateIssue(
                        GeneralAuthoringIssueCodes.InlineFillByteInvalid,
                        row.MappingId,
                        $"General fill '{row.MappingId}' must contain exactly one hexadecimal byte.",
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

        if (!limits.TryGetSlot(
                row.MappingId,
                out GeneralSlotLengthLimits? slotLimits))
        {
            issues.Add(CreateIssue(
                GeneralAuthoringIssueCodes.TrustedParentSlotMissing,
                row.MappingId,
                $"The exact Trusted Parent does not declare General input slot '{row.MappingId}'.",
                [row.MappingId],
                slotId: row.MappingId));
            return;
        }

        if (!slotLimits!.Accepts(resource.LengthBytes))
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
        GeneralMappingDraftState draft,
        string trustedParentId,
        string? savedRuleId,
        SavedRuleExecutionIdentity? savedRule,
        GeneralResourceLimits? effectiveLimits,
        IEnumerable<GeneralInputResource> inputResources,
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
            draft,
            trustedParentId,
            savedRuleId,
            effectiveLimits,
            inputResources,
            Array.AsReadOnly(occupancy),
            Array.AsReadOnly(orderedIssues),
            savedRule);
    }

    private static string FormatRange(ByteRange range)
    {
        return $"[0x{range.Start:X}, 0x{range.EndExclusive:X})";
    }
}

/// <summary>
/// Path-free immutable General admission provenance carried by reports and
/// Preview/Build identity.
/// </summary>
public sealed record GeneralAuthoringAdmissionSummary
{
    private readonly GeneralOccupancySegment[] _occupancySegments;
    private readonly GeneralAuthoringAdmissionIssue[] _issues;
    private readonly GeneralInputResource[] _inputResources;

    /// <summary>Creates one immutable projection from an admission result.</summary>
    public GeneralAuthoringAdmissionSummary(
        string trustedParentId,
        string? savedRuleId,
        GeneralResourceLimits? effectiveLimits,
        IEnumerable<GeneralInputResource> inputResources,
        IEnumerable<GeneralOccupancySegment> occupancySegments,
        IEnumerable<GeneralAuthoringAdmissionIssue> issues,
        SavedRuleExecutionIdentity? savedRule = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedParentId);
        ArgumentNullException.ThrowIfNull(occupancySegments);
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(inputResources);
        TrustedParentId = trustedParentId;
        SavedRuleId = string.IsNullOrWhiteSpace(savedRuleId)
            ? null
            : savedRuleId;
        if (savedRule is not null &&
            !StringComparer.Ordinal.Equals(SavedRuleId, savedRule.RuleId))
        {
            throw new ArgumentException(
                "Saved Rule id and exact execution identity must agree.",
                nameof(savedRule));
        }

        SavedRule = savedRule;
        EffectiveLimits = effectiveLimits;
        _inputResources = [.. inputResources];
        _occupancySegments = [.. occupancySegments];
        _issues = [.. issues];
        if (_inputResources.Any(static resource => resource is null) ||
            _occupancySegments.Any(static segment => segment is null) ||
            _issues.Any(static issue => issue is null))
        {
            throw new ArgumentException(
                "General admission summary collections cannot contain null.");
        }

        OccupancySegments = Array.AsReadOnly(_occupancySegments);
        Issues = Array.AsReadOnly(_issues);
        InputResources = Array.AsReadOnly(_inputResources);
    }

    /// <summary>Exact Parent identity.</summary>
    public string TrustedParentId { get; }

    /// <summary>Optional Saved Rule narrowing identity.</summary>
    public string? SavedRuleId { get; }

    /// <summary>Exact content-identified Saved Rule revision, when available.</summary>
    public SavedRuleExecutionIdentity? SavedRule { get; }

    /// <summary>Resolved effective limits, or null for failed resolution.</summary>
    public GeneralResourceLimits? EffectiveLimits { get; }

    /// <summary>Observed whole-file lengths without host paths.</summary>
    public IReadOnlyList<GeneralInputResource> InputResources { get; }

    /// <summary>Canonical authored occupancy.</summary>
    public IReadOnlyList<GeneralOccupancySegment> OccupancySegments { get; }

    /// <summary>Canonical blockers.</summary>
    public IReadOnlyList<GeneralAuthoringAdmissionIssue> Issues { get; }
}
