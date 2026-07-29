using System.Collections.ObjectModel;

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Resolved selection state for one profile-owned group of optional input slots.</summary>
public sealed class CompiledInputSelectionGroup
{
    private readonly string[] _memberSlotIds;
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
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        _memberSlotIds = SnapshotIds(memberSlotIds, nameof(memberSlotIds), requireValue: true);
        _applicableMemberSlotIds = SnapshotIds(
            applicableMemberSlotIds,
            nameof(applicableMemberSlotIds),
            requireValue: false);
        _selectedSlotIds = SnapshotIds(selectedSlotIds, nameof(selectedSlotIds), requireValue: false);
        HashSet<string> members = _memberSlotIds.ToHashSet(StringComparer.Ordinal);
        HashSet<string> applicable = _applicableMemberSlotIds.ToHashSet(StringComparer.Ordinal);
        var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string slotId, string reason) in notApplicableReasons ??
                     new Dictionary<string, string>(StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            if (!members.Contains(slotId) || applicable.Contains(slotId) || !reasons.TryAdd(slotId, reason))
            {
                throw new ArgumentException(
                    "Not-applicable reasons may name only unique, non-applicable selection-group members.",
                    nameof(notApplicableReasons));
            }
        }

        _notApplicableReasons = new ReadOnlyDictionary<string, string>(reasons);
        if (!applicable.IsSubsetOf(members) ||
            !_selectedSlotIds.All(applicable.Contains) ||
            minimumSelected < 0 ||
            maximumSelected < minimumSelected ||
            maximumSelected > applicable.Count ||
            _selectedSlotIds.Length < minimumSelected ||
            _selectedSlotIds.Length > maximumSelected)
        {
            throw new ArgumentException(
                "Compiled selection groups require valid member/applicability subsets and selected-count bounds.");
        }

        GroupId = groupId;
        MinimumSelected = minimumSelected;
        MaximumSelected = maximumSelected;
        MemberSlotIds = Array.AsReadOnly(_memberSlotIds);
        ApplicableMemberSlotIds = Array.AsReadOnly(_applicableMemberSlotIds);
        SelectedSlotIds = Array.AsReadOnly(_selectedSlotIds);
        NotApplicableReasons = _notApplicableReasons;
    }

    /// <summary>Stable profile-owned selection-group id.</summary>
    public string GroupId { get; }

    /// <summary>Canonical group members independent of one map resolution.</summary>
    public IReadOnlyList<string> MemberSlotIds { get; }

    /// <summary>Members applicable to the resolved map.</summary>
    public IReadOnlyList<string> ApplicableMemberSlotIds { get; }

    /// <summary>Applicable members selected for this compiled plan.</summary>
    public IReadOnlyList<string> SelectedSlotIds { get; }

    /// <summary>Profile-owned readiness reasons for members unavailable on the resolved map.</summary>
    public IReadOnlyDictionary<string, string> NotApplicableReasons { get; }

    /// <summary>Minimum selected applicable members.</summary>
    public int MinimumSelected { get; }

    /// <summary>Maximum selected applicable members.</summary>
    public int MaximumSelected { get; }

    private static string[] SnapshotIds(
        IEnumerable<string> values,
        string parameterName,
        bool requireValue)
    {
        ArgumentNullException.ThrowIfNull(values);
        string[] result = [.. values];
        if ((requireValue && result.Length == 0) ||
            result.Any(string.IsNullOrWhiteSpace) ||
            result.Distinct(StringComparer.Ordinal).Count() != result.Length)
        {
            throw new ArgumentException(
                "Selection-group ids must be non-empty and ordinally unique.",
                parameterName);
        }

        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }
}
