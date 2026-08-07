using System.Collections.ObjectModel;

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Domain-owned canonical selection constraint over optional input slots.</summary>
internal sealed class InputSelectionGroupDefinition
{
    private readonly string[] _memberSlotIds;

    internal InputSelectionGroupDefinition(
        string groupId,
        IEnumerable<string> memberSlotIds,
        int minimumSelected,
        int maximumSelected)
    {
        GroupId = CanonicalPolicyValueRules.RequireCanonicalId(groupId, nameof(groupId));
        ArgumentNullException.ThrowIfNull(memberSlotIds);
        _memberSlotIds = [.. memberSlotIds];
        foreach (string memberSlotId in _memberSlotIds)
        {
            _ = CanonicalPolicyValueRules.RequireCanonicalId(memberSlotId, nameof(memberSlotIds));
        }

        DomainInvariant.Reject(
            _memberSlotIds.Length == 0 ||
            _memberSlotIds.Distinct(StringComparer.Ordinal).Count() != _memberSlotIds.Length,
            "Selection-group member ids must be non-empty and ordinally unique.",
            nameof(memberSlotIds));

        DomainInvariant.Reject(
            minimumSelected < 0 ||
            maximumSelected < minimumSelected ||
            maximumSelected > _memberSlotIds.Length,
            "Selection bounds must satisfy 0 <= minimum <= maximum <= member count.",
            nameof(maximumSelected));

        Array.Sort(_memberSlotIds, StringComparer.Ordinal);
        MinimumSelected = minimumSelected;
        MaximumSelected = maximumSelected;
        MemberSlotIds = Array.AsReadOnly(_memberSlotIds);
    }

    internal string GroupId { get; }

    internal IReadOnlyList<string> MemberSlotIds { get; }

    internal int MinimumSelected { get; }

    internal int MaximumSelected { get; }
}

/// <summary>Resolved selection state for one profile-owned group of optional input slots.</summary>
public sealed class CompiledInputSelectionGroup
{
    private readonly InputSelectionGroupDefinition _definition;
    private readonly string[] _applicableMemberSlotIds;
    private readonly string[] _selectedSlotIds;
    private readonly IReadOnlyDictionary<string, string> _notApplicableReasons;

    internal CompiledInputSelectionGroup(
        string groupId,
        IEnumerable<string> memberSlotIds,
        IEnumerable<string> applicableMemberSlotIds,
        IEnumerable<string> selectedSlotIds,
        int minimumSelected,
        int maximumSelected,
        IReadOnlyDictionary<string, string>? notApplicableReasons = null)
        : this(
            new InputSelectionGroupDefinition(
                groupId,
                memberSlotIds,
                minimumSelected,
                maximumSelected),
            applicableMemberSlotIds,
            selectedSlotIds,
            maximumSelected,
            notApplicableReasons)
    {
    }

    internal CompiledInputSelectionGroup(
        InputSelectionGroupDefinition definition,
        IEnumerable<string> applicableMemberSlotIds,
        IEnumerable<string> selectedSlotIds,
        int maximumSelected,
        IReadOnlyDictionary<string, string>? notApplicableReasons = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _applicableMemberSlotIds = SnapshotIds(applicableMemberSlotIds, nameof(applicableMemberSlotIds));
        _selectedSlotIds = SnapshotIds(selectedSlotIds, nameof(selectedSlotIds));
        var members = definition.MemberSlotIds.ToHashSet(StringComparer.Ordinal);
        HashSet<string> applicable = _applicableMemberSlotIds.ToHashSet(StringComparer.Ordinal);
        var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string slotId, string reason) in notApplicableReasons ??
                     new Dictionary<string, string>(StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            DomainInvariant.Reject(
                !members.Contains(slotId) || applicable.Contains(slotId) || !reasons.TryAdd(slotId, reason),
                "Not-applicable reasons may name only unique, non-applicable selection-group members.",
                nameof(notApplicableReasons));
        }

        _notApplicableReasons = new ReadOnlyDictionary<string, string>(reasons);
        DomainInvariant.Reject(
            !applicable.IsSubsetOf(members) ||
            !_selectedSlotIds.All(applicable.Contains) ||
            maximumSelected < definition.MinimumSelected ||
            maximumSelected > applicable.Count ||
            _selectedSlotIds.Length < definition.MinimumSelected ||
            _selectedSlotIds.Length > maximumSelected,
            "Compiled selection groups require valid member/applicability subsets and selected-count bounds.");

        _definition = definition;
        MaximumSelected = maximumSelected;
        ApplicableMemberSlotIds = Array.AsReadOnly(_applicableMemberSlotIds);
        SelectedSlotIds = Array.AsReadOnly(_selectedSlotIds);
        NotApplicableReasons = _notApplicableReasons;
    }

    /// <summary>Stable profile-owned selection-group id.</summary>
    public string GroupId => _definition.GroupId;

    /// <summary>Canonical group members independent of one map resolution.</summary>
    public IReadOnlyList<string> MemberSlotIds => _definition.MemberSlotIds;

    /// <summary>Members applicable to the resolved map.</summary>
    public IReadOnlyList<string> ApplicableMemberSlotIds { get; }

    /// <summary>Applicable members selected for this compiled plan.</summary>
    public IReadOnlyList<string> SelectedSlotIds { get; }

    /// <summary>Profile-owned readiness reasons for members unavailable on the resolved map.</summary>
    public IReadOnlyDictionary<string, string> NotApplicableReasons { get; }

    /// <summary>Minimum selected applicable members.</summary>
    public int MinimumSelected => _definition.MinimumSelected;

    /// <summary>Maximum selected applicable members.</summary>
    public int MaximumSelected { get; }

    private static string[] SnapshotIds(IEnumerable<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);
        string[] result = [.. values];
        DomainInvariant.Reject(
            result.Any(string.IsNullOrWhiteSpace) ||
            result.Distinct(StringComparer.Ordinal).Count() != result.Length,
            "Selection-group ids must be non-empty and ordinally unique.",
            parameterName);

        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }
}
