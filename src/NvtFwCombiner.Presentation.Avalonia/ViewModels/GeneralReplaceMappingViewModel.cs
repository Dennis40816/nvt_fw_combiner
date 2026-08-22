using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>General Replace row using one canonical source and Start + Length editor.</summary>
internal sealed class GeneralReplaceMappingViewModel : GeneralMappingRowViewModel
{
    private readonly string _inlineGuidance;

    public IReadOnlyList<GeneralReplaceSourceOption> SourceOptions { get; }

    public GeneralReplaceMappingViewModel(string mappingId, int index, ShellTextResources text)
        : base(mappingId, index, "No replacement BIN selected", text)
    {
        ArgumentNullException.ThrowIfNull(text);
        SourceOptions =
        [
            new(GeneralMappingSourceKind.FileArtifact, "BIN", text.GeneralFileSourceLabel),
            new(GeneralMappingSourceKind.HexOverwrite, "HEX", text.GeneralHexOverwriteSourceLabel),
            new(GeneralMappingSourceKind.HexFill, "FILL", text.GeneralHexFillSourceLabel),
        ];
        _inlineGuidance = text.GeneralInlineHexGuidance;
        SelectedSource = SourceOptions[0];
    }

    /// <summary>Selected canonical source kind.</summary>
    public GeneralReplaceSourceOption SelectedSource
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasSource));
                OnPropertyChanged(nameof(UsesFileSource));
                OnPropertyChanged(nameof(UsesInlineSource));
                OnPropertyChanged(nameof(SourceKindIcon));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(DisplayDetail));
                OnPropertyChanged(nameof(IsGuidanceVisible));
                OnPropertyChanged(nameof(IsFileSelectionPending));
            }
        }
    } = null!;

    /// <summary>Editable hexadecimal bytes or fill byte for an inline source.</summary>
    public string InlineValue
    {
        get;
        set
        {
            if (SetProperty(ref field, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasSource));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(DisplayDetail));
                OnPropertyChanged(nameof(IsGuidanceVisible));
            }
        }
    } = string.Empty;

    /// <inheritdoc />
    public override bool HasSource => UsesFileSource ? HasFile : !string.IsNullOrWhiteSpace(InlineValue);

    /// <inheritdoc />
    public override bool UsesFileSource => SelectedSource.Kind == GeneralMappingSourceKind.FileArtifact;

    /// <inheritdoc />
    public override bool UsesInlineSource => !UsesFileSource;

    /// <inheritdoc />
    public override string SourceKindIcon => SelectedSource.Icon;

    /// <inheritdoc />
    public override string DisplayName => UsesFileSource
        ? base.DisplayName
        : HasSource ? InlineValue : _inlineGuidance;

    /// <inheritdoc />
    public override string DisplayDetail => UsesFileSource ? base.DisplayDetail : SelectedSource.Label;
}

/// <summary>Display metadata for one canonical General Replace source kind.</summary>
internal sealed record GeneralReplaceSourceOption(
    GeneralMappingSourceKind Kind,
    string Icon,
    string Label);
