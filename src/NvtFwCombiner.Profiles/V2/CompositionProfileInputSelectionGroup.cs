namespace NvtFwCombiner.Profiles.V2;

/// <summary>Checked selection constraint over existing zero-or-one profile input slots.</summary>
internal sealed class CompositionProfileInputSelectionGroup
{
    private readonly string[] _memberSlotIds;

    internal CompositionProfileInputSelectionGroup(
        string groupId,
        IEnumerable<string> memberSlotIds,
        int minimumSelected,
        int maximumSelected)
    {
        GroupId = CompositionProfileValueRules.RequireId(groupId, nameof(groupId));
        _memberSlotIds = CompositionProfileValueRules.SnapshotIds(
            memberSlotIds,
            nameof(memberSlotIds),
            requireValue: true);
        if (minimumSelected < 0 ||
            maximumSelected < minimumSelected ||
            maximumSelected > _memberSlotIds.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumSelected),
                "Selection bounds must satisfy 0 <= minimum <= maximum <= member count.");
        }

        MinimumSelected = minimumSelected;
        MaximumSelected = maximumSelected;
        MemberSlotIds = Array.AsReadOnly(_memberSlotIds);
    }

    internal string GroupId { get; }

    internal IReadOnlyList<string> MemberSlotIds { get; }

    internal int MinimumSelected { get; }

    internal int MaximumSelected { get; }
}
