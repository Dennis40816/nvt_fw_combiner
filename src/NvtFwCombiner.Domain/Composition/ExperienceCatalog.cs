namespace NvtFwCombiner.Domain.Composition;

/// <summary>Catalog of the approved composition experiences.</summary>
public static class ExperienceCatalog
{
    /// <summary>Fixed merge workflow for standard firmware composition.</summary>
    public static readonly ExperienceDescriptor StandardMerge =
        new("standard-merge", CompositionKind.Merge, AudienceKind.System, LayoutPolicy.Fixed, InputPolicy.Fixed);

    /// <summary>Fixed merge workflow for A/B firmware composition.</summary>
    public static readonly ExperienceDescriptor AbMerge =
        new("ab-merge", CompositionKind.Merge, AudienceKind.System, LayoutPolicy.Fixed, InputPolicy.Fixed);

    /// <summary>Advanced merge workflow using explicit mappings over a blank image.</summary>
    public static readonly ExperienceDescriptor GeneralMerge =
        new("general-merge", CompositionKind.Merge, AudienceKind.Advanced, LayoutPolicy.UserDefined, InputPolicy.Extensible);

    /// <summary>Display replace workflow over a reference image.</summary>
    public static readonly ExperienceDescriptor DisplayReplace =
        new("display-replace", CompositionKind.Replace, AudienceKind.Display, LayoutPolicy.Constrained, InputPolicy.Fixed);

    /// <summary>Touch-panel hardware replace workflow over a reference image.</summary>
    public static readonly ExperienceDescriptor TpHardwareReplace =
        new("tp-hw-replace", CompositionKind.Replace, AudienceKind.TpHardware, LayoutPolicy.Constrained, InputPolicy.Fixed);

    /// <summary>Touch-panel firmware replace workflow over a reference image.</summary>
    public static readonly ExperienceDescriptor TpFirmwareReplace =
        new("tp-fw-replace", CompositionKind.Replace, AudienceKind.TpFirmware, LayoutPolicy.Constrained, InputPolicy.Fixed);

    /// <summary>Advanced replace workflow using explicit mappings over a reference image.</summary>
    public static readonly ExperienceDescriptor GeneralReplace =
        new("general-replace", CompositionKind.Replace, AudienceKind.Advanced, LayoutPolicy.UserDefined, InputPolicy.Extensible);

    /// <summary>All approved experiences in stable display order.</summary>
    public static IReadOnlyList<ExperienceDescriptor> All { get; } =
    [
        StandardMerge,
        AbMerge,
        GeneralMerge,
        DisplayReplace,
        TpHardwareReplace,
        TpFirmwareReplace,
        GeneralReplace,
    ];
}
