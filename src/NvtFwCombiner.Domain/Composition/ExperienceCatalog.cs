namespace NvtFwCombiner.Domain.Composition;

/// <summary>Catalog of the approved composition experiences.</summary>
public static class ExperienceCatalog
{
    /// <summary>Fixed merge workflow for standard firmware composition.</summary>
    public static readonly ExperienceDescriptor StandardMerge =
        new(ExperienceIds.StandardMerge, CompositionKind.Merge, AudienceKind.System, LayoutPolicy.Fixed, InputPolicy.Fixed);

    /// <summary>Fixed merge workflow for A/B firmware composition.</summary>
    public static readonly ExperienceDescriptor AbMerge =
        new(ExperienceIds.AbMerge, CompositionKind.Merge, AudienceKind.System, LayoutPolicy.Fixed, InputPolicy.Fixed);

    /// <summary>Advanced merge workflow using explicit mappings over a blank image.</summary>
    public static readonly ExperienceDescriptor GeneralMerge =
        new(ExperienceIds.GeneralMerge, CompositionKind.Merge, AudienceKind.Advanced, LayoutPolicy.UserDefined, InputPolicy.Extensible);

    /// <summary>DP replace workflow over a reference image.</summary>
    public static readonly ExperienceDescriptor DpReplace =
        new(ExperienceIds.DpReplace, CompositionKind.Replace, AudienceKind.Dp, LayoutPolicy.Constrained, InputPolicy.Fixed);

    /// <summary>CtrlRAM replace workflow over a reference image.</summary>
    public static readonly ExperienceDescriptor CtrlRamReplace =
        new(ExperienceIds.CtrlRamReplace, CompositionKind.Replace, AudienceKind.CtrlRam, LayoutPolicy.Constrained, InputPolicy.Fixed);

    /// <summary>Advanced replace workflow using explicit mappings over a reference image.</summary>
    public static readonly ExperienceDescriptor GeneralReplace =
        new(ExperienceIds.GeneralReplace, CompositionKind.Replace, AudienceKind.Advanced, LayoutPolicy.UserDefined, InputPolicy.Extensible);

    /// <summary>All approved experiences in stable display order.</summary>
    public static IReadOnlyList<ExperienceDescriptor> All { get; } =
    [
        StandardMerge,
        AbMerge,
        GeneralMerge,
        DpReplace,
        CtrlRamReplace,
        GeneralReplace,
    ];

    /// <summary>Finds an approved experience by exact id.</summary>
    public static bool TryFind(string experienceId, out ExperienceDescriptor? experience)
    {
        experience = All.SingleOrDefault(item => string.Equals(item.ExperienceId, experienceId, StringComparison.Ordinal));
        return experience is not null;
    }
}
