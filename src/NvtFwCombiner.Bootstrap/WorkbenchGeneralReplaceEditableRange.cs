namespace NvtFwCombiner.Bootstrap;

/// <summary>One profile-authorized General Replace range that can be selected by an authoring surface.</summary>
public sealed record WorkbenchGeneralReplaceEditableRange(
    string RegionId,
    string DisplayName,
    long Start,
    long EndInclusive,
    bool RequiresPostbuild,
    string Detail);
