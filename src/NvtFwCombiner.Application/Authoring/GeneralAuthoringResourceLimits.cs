using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Stable issue codes emitted by General occupancy and resource admission.</summary>
public static class GeneralAuthoringIssueCodes
{
    /// <summary>Two authored target ranges share at least one byte.</summary>
    public const string TargetIntersection = "general.admission.target-intersection";
    /// <summary>An authored target range exceeds its named output address space.</summary>
    public const string TargetOutOfBounds = "general.admission.target-out-of-bounds";
    /// <summary>The draft contains more mappings than its effective ceiling.</summary>
    public const string MappingCountExceeded = "general.admission.mapping-count-exceeded";
    /// <summary>The sum of authored write lengths exceeds its effective ceiling.</summary>
    public const string TotalWriteBytesExceeded = "general.admission.total-write-bytes-exceeded";
    /// <summary>The sum of authored write lengths cannot be represented safely.</summary>
    public const string TotalWriteBytesOverflow = "general.admission.total-write-bytes-overflow";
    /// <summary>A selected whole input file exceeds its effective ceiling.</summary>
    public const string FileSizeExceeded = "general.admission.file-size-exceeded";
    /// <summary>A mapping reads beyond the observed selected input length.</summary>
    public const string SourceOutOfBounds = "general.admission.source-out-of-bounds";
    /// <summary>A file-backed mapping has no observed resource identity.</summary>
    public const string InputResourceMissing = "general.admission.input-resource-missing";
    /// <summary>The exact Trusted Parent does not declare a selected file slot.</summary>
    public const string TrustedParentSlotMissing =
        "general.admission.trusted-parent-slot-missing";
    /// <summary>An observed input resource id is declared more than once.</summary>
    public const string InputResourceDuplicate = "general.admission.input-resource-duplicate";
    /// <summary>An observed input length violates its resolved slot contract.</summary>
    public const string SlotLengthRejected = "general.admission.slot-length-rejected";
    /// <summary>An inline source would exceed safe pre-execution materialization.</summary>
    public const string InlineMaterializationExceeded = "general.admission.inline-materialization-exceeded";
    /// <summary>A Saved Rule attempts to broaden its exact Trusted Parent.</summary>
    public const string SavedRuleBroadensParent = "general.admission.saved-rule-broadens-parent";
    /// <summary>A content-identified Saved Rule does not reference the exact resolved Parent.</summary>
    public const string SavedRuleParentMismatch =
        "general.admission.saved-rule-parent-mismatch";
    /// <summary>A content-identified Saved Rule is not one exact trusted publication.</summary>
    public const string SavedRuleExecutionNotTrustedPublished =
        "saved-rule.lifecycle.execution-not-trusted-published";
    /// <summary>Active resource layers accept no common value.</summary>
    public const string EffectiveLimitsEmpty = "general.admission.effective-limits-empty";
}

/// <summary>Application-owned global technical ceilings for General authoring.</summary>
public static class GeneralAuthoringTechnicalLimits
{
    /// <summary>
    /// Default ceilings used by current headless General consumers. Exact Trusted Parent and
    /// Saved Rule layers may only reduce these values.
    /// </summary>
    public static GeneralResourceLimits Default { get; } = new(
        maximumMappingCount: 4096,
        maximumTotalWriteBytes: int.MaxValue,
        maximumFileBytes: int.MaxValue,
        maximumSafeMaterializationBytes: 0x800000);
}

/// <summary>One whole-file length observed by the filesystem adapter for a General input slot.</summary>
public sealed record GeneralInputResource
{
    /// <summary>Creates an observed whole-file resource for a named slot.</summary>
    public GeneralInputResource(string slotId, long lengthBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentOutOfRangeException.ThrowIfNegative(lengthBytes);
        SlotId = slotId;
        LengthBytes = lengthBytes;
    }

    /// <summary>Stable General slot or mapping id.</summary>
    public string SlotId { get; }

    /// <summary>Observed whole-file length.</summary>
    public long LengthBytes { get; }
}

/// <summary>One Parent- or Saved Rule-declared length envelope for a named input slot.</summary>
public sealed record GeneralSlotLengthLimits
{
    private readonly long[] _allowedLengths;

    /// <summary>Creates one interval with optional discrete accepted lengths.</summary>
    public GeneralSlotLengthLimits(
        string slotId,
        long minimumBytes,
        long maximumBytes,
        IEnumerable<long>? allowedLengths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, minimumBytes);

        _allowedLengths = allowedLengths is null
            ? []
            : [.. allowedLengths.Order()];
        if (_allowedLengths.Any(length =>
                length < minimumBytes || length > maximumBytes) ||
            _allowedLengths.Distinct().Count() != _allowedLengths.Length)
        {
            throw new ArgumentException(
                "Allowed slot lengths must be unique and inside the declared minimum/maximum range.",
                nameof(allowedLengths));
        }

        SlotId = slotId;
        MinimumBytes = minimumBytes;
        MaximumBytes = maximumBytes;
        AllowedLengths = Array.AsReadOnly(_allowedLengths);
    }

    /// <summary>Stable slot id declared by the exact Trusted Parent.</summary>
    public string SlotId { get; }

    /// <summary>Inclusive minimum accepted whole-file length.</summary>
    public long MinimumBytes { get; }

    /// <summary>Inclusive maximum accepted whole-file length.</summary>
    public long MaximumBytes { get; }

    /// <summary>Accepted discrete lengths, or empty when every length in the interval is accepted.</summary>
    public IReadOnlyList<long> AllowedLengths { get; }

    /// <summary>Returns whether a whole-file length is accepted by this slot envelope.</summary>
    public bool Accepts(long lengthBytes)
    {
        return lengthBytes >= MinimumBytes &&
            lengthBytes <= MaximumBytes &&
            (_allowedLengths.Length == 0 || Array.BinarySearch(_allowedLengths, lengthBytes) >= 0);
    }
}

/// <summary>
/// One immutable General resource layer. The Application layer owns technical ceilings; an exact
/// Trusted Parent supplies the semantic layer; a Saved Rule may supply a third narrowing layer.
/// </summary>
public sealed record GeneralResourceLimits
{
    private readonly GeneralSlotLengthLimits[] _slotLimits;

    /// <summary>Creates one validated immutable resource-limit layer.</summary>
    public GeneralResourceLimits(
        int maximumMappingCount,
        long maximumTotalWriteBytes,
        long maximumFileBytes,
        long maximumSafeMaterializationBytes,
        IEnumerable<GeneralSlotLengthLimits>? slotLimits = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMappingCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTotalWriteBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFileBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSafeMaterializationBytes);

        _slotLimits = slotLimits is null
            ? []
            : [.. slotLimits.OrderBy(static limit => limit.SlotId, StringComparer.Ordinal)];
        if (_slotLimits.Any(static limit => limit is null) ||
            _slotLimits.Select(static limit => limit.SlotId)
                .Distinct(StringComparer.Ordinal).Count() != _slotLimits.Length)
        {
            throw new ArgumentException(
                "General slot limits must be non-null and have unique ids.",
                nameof(slotLimits));
        }

        if (_slotLimits.Any(limit => limit.MaximumBytes > maximumFileBytes))
        {
            throw new ArgumentException(
                "General slot limits cannot exceed their layer's whole-file maximum.",
                nameof(slotLimits));
        }

        MaximumMappingCount = maximumMappingCount;
        MaximumTotalWriteBytes = maximumTotalWriteBytes;
        MaximumFileBytes = maximumFileBytes;
        MaximumSafeMaterializationBytes = maximumSafeMaterializationBytes;
        SlotLimits = Array.AsReadOnly(_slotLimits);
    }

    /// <summary>Maximum number of authored mapping rows.</summary>
    public int MaximumMappingCount { get; }

    /// <summary>Maximum sum of authored target lengths.</summary>
    public long MaximumTotalWriteBytes { get; }

    /// <summary>Maximum observed whole input file length.</summary>
    public long MaximumFileBytes { get; }

    /// <summary>Maximum bytes an inline overwrite or fill may materialize.</summary>
    public long MaximumSafeMaterializationBytes { get; }

    /// <summary>Named per-slot length limits in ordinal slot-id order.</summary>
    public IReadOnlyList<GeneralSlotLengthLimits> SlotLimits { get; }

    /// <summary>Finds one named slot limit.</summary>
    public bool TryGetSlot(string slotId, out GeneralSlotLengthLimits? limits)
    {
        limits = _slotLimits.FirstOrDefault(
            candidate => StringComparer.Ordinal.Equals(candidate.SlotId, slotId));
        return limits is not null;
    }
}

/// <summary>
/// Exact Trusted Parent resource authority supplied to the Application admission use case.
/// Every file-backed mapping must have a named slot declaration in this policy.
/// </summary>
public sealed record GeneralTrustedParentResourcePolicy
{
    /// <summary>Creates one exact Parent policy with stable provenance.</summary>
    public GeneralTrustedParentResourcePolicy(
        string parentId,
        GeneralResourceLimits limits)
        : this(parentId, parentIdentity: null, limits)
    {
    }

    /// <summary>Creates one exact content-identified Parent policy.</summary>
    public GeneralTrustedParentResourcePolicy(
        SavedRuleParentIdentity parentIdentity,
        GeneralResourceLimits limits)
        : this(
            parentIdentity?.ProfileId ??
            throw new ArgumentNullException(nameof(parentIdentity)),
            parentIdentity,
            limits)
    {
    }

    private GeneralTrustedParentResourcePolicy(
        string parentId,
        SavedRuleParentIdentity? parentIdentity,
        GeneralResourceLimits limits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentId);
        ArgumentNullException.ThrowIfNull(limits);
        ParentId = parentId;
        ParentIdentity = parentIdentity;
        Limits = limits;
    }

    /// <summary>Exact trusted profile/bundle identity used for this admission.</summary>
    public string ParentId { get; }

    /// <summary>Exact content identity when this Parent admits a Saved Rule v2 revision.</summary>
    public SavedRuleParentIdentity? ParentIdentity { get; }

    /// <summary>Parent semantic ceilings and explicitly declared file slots.</summary>
    public GeneralResourceLimits Limits { get; }
}

/// <summary>Optional Saved Rule narrowing authority supplied to the same admission use case.</summary>
public sealed record GeneralSavedRuleResourcePolicy
{
    /// <summary>Creates one exact content-identified narrowing policy.</summary>
    public GeneralSavedRuleResourcePolicy(
        SavedRuleExecutionIdentity executionIdentity,
        GeneralResourceLimits limits)
        : this(
            executionIdentity?.RuleId ??
            throw new ArgumentNullException(nameof(executionIdentity)),
            executionIdentity,
            SavedRuleLifecycle.Import(executionIdentity),
            limits)
    {
    }

    /// <summary>Creates one independently resolved published Catalog narrowing policy.</summary>
    internal GeneralSavedRuleResourcePolicy(
        SavedRuleLifecycleSnapshot lifecycle,
        GeneralResourceLimits limits)
        : this(
            lifecycle?.Identity.RuleId ??
            throw new ArgumentNullException(nameof(lifecycle)),
            lifecycle.Identity,
            lifecycle,
            limits)
    {
    }

    private GeneralSavedRuleResourcePolicy(
        string ruleId,
        SavedRuleExecutionIdentity? executionIdentity,
        SavedRuleLifecycleSnapshot? lifecycle,
        GeneralResourceLimits limits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(limits);
        RuleId = ruleId;
        ExecutionIdentity = executionIdentity;
        Lifecycle = lifecycle;
        Limits = limits;
    }

    /// <summary>Saved Rule identity responsible for the narrowing layer.</summary>
    public string RuleId { get; }

    /// <summary>Exact rule revision and Parent identity for v2 consumption.</summary>
    public SavedRuleExecutionIdentity? ExecutionIdentity { get; }

    /// <summary>Host-resolved lifecycle authority for the exact execution identity.</summary>
    public SavedRuleLifecycleSnapshot? Lifecycle { get; }

    /// <summary>Limits that may only narrow the exact Parent.</summary>
    public GeneralResourceLimits Limits { get; }
}

/// <summary>One deterministic authored target occupancy segment.</summary>
public sealed record GeneralOccupancySegment(
    string MappingId,
    GeneralMappingSourceKind SourceKind,
    string TargetAddressSpaceId,
    ByteRange TargetRange);

/// <summary>Typed blocker returned before General compilation or allocation.</summary>
public sealed record GeneralAuthoringAdmissionIssue
{
    private readonly string[] _mappingIds;

    internal GeneralAuthoringAdmissionIssue(
        string code,
        string issueId,
        string message,
        IEnumerable<string>? mappingIds = null,
        ByteRange? intersection = null,
        string? slotId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(issueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _mappingIds = mappingIds is null
            ? []
            : [.. mappingIds.Order(StringComparer.Ordinal)];
        Code = code;
        IssueId = issueId;
        Message = message;
        MappingIds = Array.AsReadOnly(_mappingIds);
        Intersection = intersection;
        SlotId = string.IsNullOrWhiteSpace(slotId) ? null : slotId;
    }

    /// <summary>Stable machine-readable blocker code.</summary>
    public string Code { get; }

    /// <summary>Stable row-order-independent identity.</summary>
    public string IssueId { get; }

    /// <summary>Human-readable blocker detail.</summary>
    public string Message { get; }

    /// <summary>Stable involved mapping ids in ordinal order.</summary>
    public IReadOnlyList<string> MappingIds { get; }

    /// <summary>Exact half-open intersection for occupancy blockers.</summary>
    public ByteRange? Intersection { get; }

    /// <summary>Named slot involved in a resource blocker.</summary>
    public string? SlotId { get; }

    /// <summary>Projects the typed blocker into the shared run-report issue model.</summary>
    public CompositionIssue ToCompositionIssue()
    {
        return new CompositionIssue(Code, Message, IssueId);
    }
}

/// <summary>Result of resolving technical, exact Parent, and optional Saved Rule limits.</summary>
public sealed record GeneralResourceResolutionResult
{
    private readonly GeneralAuthoringAdmissionIssue[] _issues;

    /// <summary>Creates one immutable resolution result.</summary>
    public GeneralResourceResolutionResult(
        GeneralResourceLimits? effectiveLimits,
        IEnumerable<GeneralAuthoringAdmissionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        _issues = [.. issues];
        if (_issues.Any(static issue => issue is null))
        {
            throw new ArgumentException(
                "General resource resolution issues cannot contain null.",
                nameof(issues));
        }

        EffectiveLimits = effectiveLimits;
        Issues = Array.AsReadOnly(_issues);
    }

    /// <summary>Resolved effective limits, or null when active layers conflict.</summary>
    public GeneralResourceLimits? EffectiveLimits { get; }

    /// <summary>Deterministic typed blockers.</summary>
    public IReadOnlyList<GeneralAuthoringAdmissionIssue> Issues { get; }

    /// <summary>Whether one non-empty, contradiction-free effective policy was resolved.</summary>
    public bool IsResolved => EffectiveLimits is not null && Issues.Count == 0;
}

/// <summary>Resolves General resource policy without copying firmware/profile definitions.</summary>
public static class GeneralResourceLimitResolver
{
    /// <summary>Intersects technical and Parent limits, then applies optional Saved Rule narrowing.</summary>
    public static GeneralResourceResolutionResult Resolve(
        GeneralResourceLimits technicalLimits,
        GeneralTrustedParentResourcePolicy trustedParent,
        GeneralSavedRuleResourcePolicy? savedRule)
    {
        ArgumentNullException.ThrowIfNull(technicalLimits);
        ArgumentNullException.ThrowIfNull(trustedParent);

        List<GeneralAuthoringAdmissionIssue> issues = [];
        GeneralResourceLimits? parentEffective = IntersectParent(
            technicalLimits,
            trustedParent.Limits,
            issues);
        if (parentEffective is null)
        {
            return new GeneralResourceResolutionResult(null, OrderIssues(issues));
        }

        if (savedRule is null)
        {
            return new GeneralResourceResolutionResult(parentEffective, []);
        }

        ValidateSavedRuleNarrowing(
            parentEffective,
            trustedParent.Limits,
            savedRule.Limits,
            issues);
        GeneralResourceLimits? effective = IntersectSavedRule(
            parentEffective,
            savedRule.Limits,
            issues);
        return issues.Count == 0
            ? new GeneralResourceResolutionResult(effective, [])
            : new GeneralResourceResolutionResult(null, OrderIssues(issues));
    }

    private static GeneralResourceLimits? IntersectParent(
        GeneralResourceLimits technical,
        GeneralResourceLimits parent,
        List<GeneralAuthoringAdmissionIssue> issues)
    {
        List<GeneralSlotLengthLimits> slots = [];
        foreach (GeneralSlotLengthLimits parentSlot in parent.SlotLimits)
        {
            bool hasTechnicalSlot = technical.TryGetSlot(
                parentSlot.SlotId,
                out GeneralSlotLengthLimits? technicalSlot);
            long minimum = hasTechnicalSlot
                ? Math.Max(
                    parentSlot.MinimumBytes,
                    technicalSlot!.MinimumBytes)
                : parentSlot.MinimumBytes;
            long maximum = Math.Min(
                parentSlot.MaximumBytes,
                Math.Min(parent.MaximumFileBytes, technical.MaximumFileBytes));
            if (hasTechnicalSlot)
            {
                maximum = Math.Min(
                    maximum,
                    technicalSlot!.MaximumBytes);
            }

            bool discreteConstraint =
                parentSlot.AllowedLengths.Count > 0 ||
                (hasTechnicalSlot &&
                 technicalSlot!.AllowedLengths.Count > 0);
            long[] allowed = hasTechnicalSlot
                ? IntersectAllowedLengths(
                    parentSlot,
                    technicalSlot!,
                    minimum,
                    maximum)
                :
                [
                    .. parentSlot.AllowedLengths.Where(length =>
                        length >= minimum && length <= maximum),
                ];
            if (minimum > maximum || (discreteConstraint && allowed.Length == 0))
            {
                issues.Add(CreateSimpleIssue(
                    GeneralAuthoringIssueCodes.EffectiveLimitsEmpty,
                    parentSlot.SlotId,
                    $"General input slot '{parentSlot.SlotId}' has no length accepted by every active limit layer.",
                    parentSlot.SlotId));
                continue;
            }

            slots.Add(new GeneralSlotLengthLimits(
                parentSlot.SlotId,
                minimum,
                maximum,
                discreteConstraint ? allowed : null));
        }

        return issues.Count > 0
            ? null
            : new GeneralResourceLimits(
                Math.Min(
                    technical.MaximumMappingCount,
                    parent.MaximumMappingCount),
                Math.Min(
                    technical.MaximumTotalWriteBytes,
                    parent.MaximumTotalWriteBytes),
                Math.Min(
                    technical.MaximumFileBytes,
                    parent.MaximumFileBytes),
                Math.Min(
                    technical.MaximumSafeMaterializationBytes,
                    parent.MaximumSafeMaterializationBytes),
                slots);
    }

    private static GeneralResourceLimits? IntersectSavedRule(
        GeneralResourceLimits parentEffective,
        GeneralResourceLimits savedRule,
        List<GeneralAuthoringAdmissionIssue> issues)
    {
        List<GeneralSlotLengthLimits> slots =
        [
            .. parentEffective.SlotLimits,
        ];
        foreach (GeneralSlotLengthLimits savedSlot in savedRule.SlotLimits)
        {
            if (!parentEffective.TryGetSlot(
                    savedSlot.SlotId,
                    out GeneralSlotLengthLimits? parentSlot))
            {
                continue;
            }

            long minimum = Math.Max(parentSlot!.MinimumBytes, savedSlot.MinimumBytes);
            long maximum = Math.Min(parentSlot.MaximumBytes, savedSlot.MaximumBytes);
            long[] allowed = IntersectAllowedLengths(
                parentSlot,
                savedSlot,
                minimum,
                maximum);
            bool discreteConstraint =
                parentSlot.AllowedLengths.Count > 0 ||
                savedSlot.AllowedLengths.Count > 0;
            if (minimum > maximum || (discreteConstraint && allowed.Length == 0))
            {
                issues.Add(CreateSimpleIssue(
                    GeneralAuthoringIssueCodes.EffectiveLimitsEmpty,
                    savedSlot.SlotId,
                    $"General input slot '{savedSlot.SlotId}' has no length accepted by every active limit layer.",
                    savedSlot.SlotId));
                continue;
            }

            int index = slots.FindIndex(slot =>
                StringComparer.Ordinal.Equals(
                    slot.SlotId,
                    savedSlot.SlotId));
            slots[index] = new GeneralSlotLengthLimits(
                savedSlot.SlotId,
                minimum,
                maximum,
                discreteConstraint ? allowed : null);
        }

        return issues.Count > 0
            ? null
            : new GeneralResourceLimits(
                Math.Min(
                    parentEffective.MaximumMappingCount,
                    savedRule.MaximumMappingCount),
                Math.Min(
                    parentEffective.MaximumTotalWriteBytes,
                    savedRule.MaximumTotalWriteBytes),
                Math.Min(
                    parentEffective.MaximumFileBytes,
                    savedRule.MaximumFileBytes),
                Math.Min(
                    parentEffective.MaximumSafeMaterializationBytes,
                    savedRule.MaximumSafeMaterializationBytes),
                slots);
    }

    private static long[] IntersectAllowedLengths(
        GeneralSlotLengthLimits left,
        GeneralSlotLengthLimits right,
        long minimum,
        long maximum)
    {
        IEnumerable<long> candidates = left.AllowedLengths.Count switch
        {
            > 0 when right.AllowedLengths.Count > 0 =>
                left.AllowedLengths.Intersect(right.AllowedLengths),
            > 0 => left.AllowedLengths,
            _ when right.AllowedLengths.Count > 0 => right.AllowedLengths,
            _ => [],
        };
        return
        [
            .. candidates
                .Where(length => length >= minimum && length <= maximum)
                .Distinct()
                .Order(),
        ];
    }

    private static void ValidateSavedRuleNarrowing(
        GeneralResourceLimits parentEffective,
        GeneralResourceLimits trustedParent,
        GeneralResourceLimits savedRule,
        List<GeneralAuthoringAdmissionIssue> issues)
    {
        if (savedRule.MaximumMappingCount > parentEffective.MaximumMappingCount ||
            savedRule.MaximumTotalWriteBytes > parentEffective.MaximumTotalWriteBytes ||
            savedRule.MaximumFileBytes > parentEffective.MaximumFileBytes ||
            savedRule.MaximumSafeMaterializationBytes >
                parentEffective.MaximumSafeMaterializationBytes)
        {
            issues.Add(CreateSimpleIssue(
                GeneralAuthoringIssueCodes.SavedRuleBroadensParent,
                "resource-ceilings",
                "Saved Rule resource ceilings must not exceed the effective Trusted Parent ceilings."));
        }

        foreach (GeneralSlotLengthLimits savedSlot in savedRule.SlotLimits)
        {
            GeneralSlotLengthLimits? parentSlot = null;
            bool parentDeclaresSlot =
                trustedParent.TryGetSlot(savedSlot.SlotId, out _) &&
                parentEffective.TryGetSlot(
                    savedSlot.SlotId,
                    out parentSlot);
            if (!parentDeclaresSlot || !IsSubset(savedSlot, parentSlot!))
            {
                issues.Add(CreateSimpleIssue(
                    GeneralAuthoringIssueCodes.SavedRuleBroadensParent,
                    savedSlot.SlotId,
                    $"Saved Rule slot '{savedSlot.SlotId}' must be a subset of an exact Trusted Parent slot declaration.",
                    savedSlot.SlotId));
            }
        }
    }

    private static bool IsSubset(
        GeneralSlotLengthLimits candidate,
        GeneralSlotLengthLimits parent)
    {
        return candidate.AllowedLengths.Count > 0
            ? candidate.AllowedLengths.All(parent.Accepts)
            : parent.AllowedLengths.Count == 0
                ? candidate.MinimumBytes >= parent.MinimumBytes &&
                  candidate.MaximumBytes <= parent.MaximumBytes
                : candidate.MinimumBytes == candidate.MaximumBytes &&
                  parent.Accepts(candidate.MinimumBytes);
    }

    private static GeneralAuthoringAdmissionIssue CreateSimpleIssue(
        string code,
        string identity,
        string message,
        string? slotId = null)
    {
        return new GeneralAuthoringAdmissionIssue(
            code,
            $"{code}:{identity}",
            message,
            slotId: slotId);
    }

    private static GeneralAuthoringAdmissionIssue[] OrderIssues(
        IEnumerable<GeneralAuthoringAdmissionIssue> issues)
    {
        return
        [
            .. issues.OrderBy(static issue => issue.IssueId, StringComparer.Ordinal),
        ];
    }
}
