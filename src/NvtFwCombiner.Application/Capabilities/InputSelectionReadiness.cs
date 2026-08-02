using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Stable issue codes for profile-owned input-selection readiness.</summary>
public static class InputSelectionReadinessIssueCodes
{
    /// <summary>A selection group has fewer selected applicable members than required.</summary>
    public const string SelectionPending = "capability.input-selection.pending";

    /// <summary>A selected member is unavailable for the resolved map.</summary>
    public const string SelectionNotApplicable = "capability.input-selection.not-applicable";

    /// <summary>A selection group exceeds its declared maximum.</summary>
    public const string SelectionCardinalityInvalid = "capability.input-selection.cardinality-invalid";

    /// <summary>A selected slot is not declared by any resolved selection group.</summary>
    public const string SelectionUnknown = "capability.input-selection.unknown";
}

/// <summary>Closed next actions shared by CLI and later Presentation adapters.</summary>
public enum InputSelectionNextActionKind
{
    /// <summary>Load one prerequisite artifact before independently selecting this slot.</summary>
    LoadArtifactFirst,

    /// <summary>Compatibility name retained for pre-contract Presentation consumers.</summary>
    LoadPrerequisite = LoadArtifactFirst,

    /// <summary>Select another applicable member to satisfy group cardinality.</summary>
    SelectMember,

    /// <summary>Remove or correct a selection rejected by the resolved group.</summary>
    CorrectSelection,
}

/// <summary>One typed operator action for an unresolved selection result.</summary>
public sealed record InputSelectionNextAction(
    InputSelectionNextActionKind Kind,
    string SubjectId,
    string? ArtifactBindingId = null);

/// <summary>One typed selection issue owned by the Application result.</summary>
public sealed record InputSelectionReadinessIssue(
    string Code,
    string SubjectId,
    string Message,
    InputSelectionNextAction NextAction);

/// <summary>Resolved state for one canonical selection-group member.</summary>
public sealed record InputSelectionMemberReadiness(
    string SlotId,
    bool IsSelected,
    ResolvedChildReadiness Readiness,
    bool CanSelect,
    string? Reason,
    InputSelectionNextAction? NextAction);

/// <summary>Resolved state and cardinality for one canonical input-selection group.</summary>
public sealed record InputSelectionGroupReadiness(
    string GroupId,
    int MinimumSelected,
    int MaximumSelected,
    int SelectedApplicableCount,
    ResolvedChildReadiness Readiness,
    IReadOnlyList<InputSelectionMemberReadiness> Members,
    InputSelectionReadinessIssue? Issue);

/// <summary>
/// Application-owned immutable selection result consumed by headless clients.
/// It references compiler-resolved groups and never redefines profile facts.
/// </summary>
public sealed class InputSelectionReadinessSnapshot
{
    private readonly InputSelectionGroupReadiness[] _groups;
    private readonly InputSelectionReadinessIssue[] _issues;

    internal InputSelectionReadinessSnapshot(
        AuthoringRevision authoringRevision,
        IEnumerable<InputSelectionGroupReadiness> groups,
        IEnumerable<InputSelectionReadinessIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(issues);
        _groups = [.. groups];
        _issues = [.. issues];
        if (_groups.Any(static group => group is null) ||
            _groups.Select(static group => group.GroupId).Distinct(StringComparer.Ordinal).Count() != _groups.Length ||
            _issues.Any(static issue => issue is null))
        {
            throw new ArgumentException("Selection readiness groups must be non-null and uniquely identified.");
        }

        AuthoringRevision = authoringRevision;
        Groups = Array.AsReadOnly(_groups);
        Issues = Array.AsReadOnly(_issues);
    }

    /// <summary>Authoring-input revision that owns this derived snapshot.</summary>
    public AuthoringRevision AuthoringRevision { get; }

    /// <summary>Resolved groups in stable ordinal order.</summary>
    public IReadOnlyList<InputSelectionGroupReadiness> Groups { get; }

    /// <summary>All current selection blockers in stable priority order.</summary>
    public IReadOnlyList<InputSelectionReadinessIssue> Issues { get; }

    /// <summary>True when every group satisfies its resolved applicability and cardinality.</summary>
    public bool CanBuild => _issues.Length == 0 &&
        _groups.All(static group => group.Readiness == ResolvedChildReadiness.Ready);

    /// <summary>Highest-priority issue, or null when selection readiness is complete.</summary>
    public InputSelectionReadinessIssue? PrimaryIssue => _issues.FirstOrDefault();
}

/// <summary>Derives one typed selection result from compiler-owned resolved groups.</summary>
public static class InputSelectionReadinessResolver
{
    /// <summary>
    /// Projects one declared metadata dependency onto its owning input slot.
    /// The profile-resolved prerequisite remains the sole source of readiness
    /// and next-action identity.
    /// </summary>
    public static InputSelectionMemberReadiness ProjectMetadataDependency(
        MetadataInspectionResult dependency,
        bool isSelected)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        if (!Enum.IsDefined(dependency.Readiness))
        {
            throw new ArgumentOutOfRangeException(
                nameof(dependency),
                dependency.Readiness,
                "Unknown metadata dependency readiness.");
        }

        string slotId = dependency.PlanEntry.Definition.SlotId;
        if ((dependency.Readiness is
                ResolvedChildReadiness.PendingInput or
                ResolvedChildReadiness.Blocked) &&
            dependency.Resolution?.Prerequisite is null)
        {
            throw new ArgumentException(
                "Metadata dependency projection requires a declared prerequisite.",
                nameof(dependency));
        }

        InputSelectionNextAction? nextAction = dependency.NextAction switch
        {
            null => null,
            { Kind: ResolvedPrerequisiteActionKind.LoadArtifactFirst } action =>
                new InputSelectionNextAction(
                    InputSelectionNextActionKind.LoadArtifactFirst,
                    action.SlotId,
                    action.ArtifactBindingId),
            _ => throw new ArgumentOutOfRangeException(
                nameof(dependency),
                dependency.NextAction,
                "Unknown metadata prerequisite action."),
        };
        if (dependency.Readiness == ResolvedChildReadiness.PendingInput &&
            nextAction is null)
        {
            throw new ArgumentException(
                "A pending metadata dependency must name its prerequisite action.",
                nameof(dependency));
        }

        string? reason = dependency.Readiness switch
        {
            ResolvedChildReadiness.Ready => null,
            ResolvedChildReadiness.PendingInput =>
                $"Load {nextAction!.SubjectId} first to resolve {slotId}.",
            ResolvedChildReadiness.Blocked =>
                $"A declared prerequisite or artifact blocks {slotId}.",
            ResolvedChildReadiness.NotApplicable =>
                $"The current capability does not apply {slotId}.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(dependency),
                dependency.Readiness,
                "Unknown metadata dependency readiness."),
        };
        return new InputSelectionMemberReadiness(
            slotId,
            isSelected,
            dependency.Readiness,
            CanSelect: dependency.Readiness == ResolvedChildReadiness.Ready,
            reason,
            nextAction);
    }

    /// <summary>
    /// Resolves current selections. When map applicability still depends on a
    /// prerequisite, every dependent member remains PendingInput and cannot be
    /// selected through an independent transition.
    /// </summary>
    public static InputSelectionReadinessSnapshot Resolve(
        AuthoringRevision authoringRevision,
        IEnumerable<CompiledInputSelectionGroup> groups,
        IEnumerable<string> selectedSlotIds,
        string? unresolvedApplicabilityPrerequisiteSlotId = null)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(selectedSlotIds);
        CompiledInputSelectionGroup[] resolvedGroups =
        [
            .. groups.OrderBy(static group => group.GroupId, StringComparer.Ordinal),
        ];
        if (resolvedGroups.Any(static group => group is null) ||
            resolvedGroups.Select(static group => group.GroupId)
                .Distinct(StringComparer.Ordinal).Count() != resolvedGroups.Length)
        {
            throw new ArgumentException(
                "Resolved input-selection groups must be non-null and unique.",
                nameof(groups));
        }

        string[] selected = [.. selectedSlotIds];
        if (selected.Any(string.IsNullOrWhiteSpace) ||
            selected.Distinct(StringComparer.Ordinal).Count() != selected.Length)
        {
            throw new ArgumentException(
                "Selected input slot ids must be non-empty and unique.",
                nameof(selectedSlotIds));
        }

        HashSet<string> selectedSet = selected.ToHashSet(StringComparer.Ordinal);
        var allMembers = resolvedGroups
            .SelectMany(static group => group.MemberSlotIds)
            .ToHashSet(StringComparer.Ordinal);
        List<InputSelectionReadinessIssue> issues =
        [
            .. selected
                .Where(slotId => !allMembers.Contains(slotId))
                .Order(StringComparer.Ordinal)
                .Select(slotId => new InputSelectionReadinessIssue(
                    InputSelectionReadinessIssueCodes.SelectionUnknown,
                    slotId,
                    $"Input selection names unknown slot '{slotId}'.",
                    new InputSelectionNextAction(
                        InputSelectionNextActionKind.CorrectSelection,
                        slotId))),
        ];
        List<InputSelectionGroupReadiness> projectedGroups = new(resolvedGroups.Length);
        foreach (CompiledInputSelectionGroup group in resolvedGroups)
        {
            InputSelectionGroupReadiness projection = ResolveGroup(
                group,
                selectedSet,
                unresolvedApplicabilityPrerequisiteSlotId);
            projectedGroups.Add(projection);
            if (projection.Issue is not null)
            {
                issues.Add(projection.Issue);
            }
        }

        return new InputSelectionReadinessSnapshot(
            authoringRevision,
            projectedGroups,
            issues);
    }

    private static InputSelectionGroupReadiness ResolveGroup(
        CompiledInputSelectionGroup group,
        HashSet<string> selectedSlotIds,
        string? unresolvedApplicabilityPrerequisiteSlotId)
    {
        bool applicabilityPending = !string.IsNullOrWhiteSpace(
            unresolvedApplicabilityPrerequisiteSlotId);
        InputSelectionMemberReadiness[] members =
        [
            .. group.MemberSlotIds.Select(slotId =>
            {
                bool selected = selectedSlotIds.Contains(slotId);
                bool unavailable = group.NotApplicableReasons.TryGetValue(
                    slotId,
                    out string? reason);
                return (applicabilityPending, unavailable) switch
                {
                    (true, _) => new InputSelectionMemberReadiness(
                        slotId,
                        selected,
                        ResolvedChildReadiness.PendingInput,
                        CanSelect: false,
                        $"Load {unresolvedApplicabilityPrerequisiteSlotId} first to determine whether {slotId} applies.",
                        new InputSelectionNextAction(
                            InputSelectionNextActionKind.LoadArtifactFirst,
                            unresolvedApplicabilityPrerequisiteSlotId!)),
                    (_, true) => new InputSelectionMemberReadiness(
                        slotId,
                        selected,
                        selected
                            ? ResolvedChildReadiness.Blocked
                            : ResolvedChildReadiness.NotApplicable,
                        CanSelect: false,
                        reason,
                        selected
                            ? new InputSelectionNextAction(
                                InputSelectionNextActionKind.CorrectSelection,
                                slotId)
                            : null),
                    _ => new InputSelectionMemberReadiness(
                        slotId,
                        selected,
                        ResolvedChildReadiness.Ready,
                        CanSelect: true,
                        Reason: null,
                        NextAction: null),
                };
            }),
        ];
        int selectedApplicable = members.Count(static member =>
            member.IsSelected && member.Readiness == ResolvedChildReadiness.Ready);
        InputSelectionMemberReadiness? selectedBlocked = members.FirstOrDefault(static member =>
            member.IsSelected && member.Readiness == ResolvedChildReadiness.Blocked);
        InputSelectionMemberReadiness? selectedPending = members.FirstOrDefault(static member =>
            member.IsSelected && member.Readiness == ResolvedChildReadiness.PendingInput);
        InputSelectionReadinessIssue? issue;
        ResolvedChildReadiness readiness;
        if (selectedBlocked is not null)
        {
            readiness = ResolvedChildReadiness.Blocked;
            issue = new InputSelectionReadinessIssue(
                InputSelectionReadinessIssueCodes.SelectionNotApplicable,
                selectedBlocked.SlotId,
                selectedBlocked.Reason!,
                selectedBlocked.NextAction!);
        }
        else if (applicabilityPending)
        {
            InputSelectionMemberReadiness pending = selectedPending ?? members[0];
            readiness = ResolvedChildReadiness.PendingInput;
            issue = new InputSelectionReadinessIssue(
                InputSelectionReadinessIssueCodes.SelectionPending,
                pending.SlotId,
                pending.Reason!,
                pending.NextAction!);
        }
        else if (selectedApplicable > group.MaximumSelected)
        {
            readiness = ResolvedChildReadiness.Blocked;
            issue = new InputSelectionReadinessIssue(
                InputSelectionReadinessIssueCodes.SelectionCardinalityInvalid,
                group.GroupId,
                $"Input selection group '{group.GroupId}' accepts at most {group.MaximumSelected} selections; found {selectedApplicable}.",
                new InputSelectionNextAction(
                    InputSelectionNextActionKind.CorrectSelection,
                    group.GroupId));
        }
        else if (selectedApplicable < group.MinimumSelected)
        {
            readiness = ResolvedChildReadiness.PendingInput;
            issue = new InputSelectionReadinessIssue(
                InputSelectionReadinessIssueCodes.SelectionPending,
                group.GroupId,
                $"Input selection group '{group.GroupId}' requires at least {group.MinimumSelected} applicable selection; found {selectedApplicable}.",
                new InputSelectionNextAction(
                    InputSelectionNextActionKind.SelectMember,
                    group.GroupId));
        }
        else
        {
            readiness = ResolvedChildReadiness.Ready;
            issue = null;
        }

        return new InputSelectionGroupReadiness(
            group.GroupId,
            group.MinimumSelected,
            group.MaximumSelected,
            selectedApplicable,
            readiness,
            Array.AsReadOnly(members),
            issue);
    }
}
