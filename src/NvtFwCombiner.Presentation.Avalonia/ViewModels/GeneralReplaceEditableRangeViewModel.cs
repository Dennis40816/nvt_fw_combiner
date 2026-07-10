namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One profile-authorized General Replace range offered by the hexadecimal editor.</summary>
public sealed record GeneralReplaceEditableRangeViewModel(
    string RegionId,
    string DisplayName,
    string RangeLabel,
    string StartAddress,
    string EndAddress,
    bool RequiresPostbuild,
    string Detail)
{
    /// <summary>Compact selection label for the range picker.</summary>
    public string SelectionLabel => $"{DisplayName}  {RangeLabel}";
}
