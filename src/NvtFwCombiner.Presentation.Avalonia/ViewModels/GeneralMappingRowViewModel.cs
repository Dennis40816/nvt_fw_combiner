using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Shared file and list state for one General mapping row.</summary>
public abstract partial class GeneralMappingRowViewModel : ObservableObject
{
    private readonly ShellTextResources _text;

    /// <summary>Creates shared mapping-row state.</summary>
    protected GeneralMappingRowViewModel(
        string mappingId,
        int index,
        string emptyDisplayName,
        ShellTextResources? text = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mappingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(emptyDisplayName);

        MappingId = mappingId;
        Index = index;
        DisplayName = emptyDisplayName;
        _text = text ?? ShellTextResources.For(ShellLanguage.English);
    }

    /// <summary>Stable row id used by browse/drop handlers.</summary>
    public string MappingId { get; }

    /// <summary>One-based row number displayed to the user.</summary>
    public int Index { get; private set; }

    /// <summary>Displayed file name or empty-slot state.</summary>
    public virtual string DisplayName => HasFile ? Path.GetFileName(FilePath!) : field;

    /// <summary>Displayed selected file path.</summary>
    public virtual string DisplayDetail => HasFile ? FirmwarePathDisplay.Normalize(FilePath!) : string.Empty;

    /// <summary>True when a local input file is selected.</summary>
    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

    /// <summary>True when this row has a file or inline source ready for validation.</summary>
    public virtual bool HasSource => HasFile;

    /// <summary>True when this row currently uses the shared BIN picker.</summary>
    public virtual bool UsesFileSource => true;

    /// <summary>True when this row currently uses an inline hexadecimal source.</summary>
    public virtual bool UsesInlineSource => false;

    /// <summary>Compact non-color source-kind marker.</summary>
    public virtual string SourceKindIcon => "BIN";

    /// <summary>Accepted identity of the currently selected file bytes.</summary>
    public FileStamp? AcceptedFileStamp { get; private set; }

    /// <summary>Current typed selected-file issue projected from Application.</summary>
    public GeneralSelectedFileInspectionIssue? InspectionIssue { get; private set; }

    /// <summary>True when the canonical workflow prerequisites permit selecting a BIN.</summary>
    public bool CanSelectFile { get; private set; } = true;

    /// <summary>Explains why BIN selection is currently unavailable.</summary>
    public string FileSelectionAvailabilityMessage { get; private set; } = string.Empty;

    /// <summary>True when a prerequisite keeps the BIN input pending.</summary>
    public bool IsFileSelectionPending => UsesFileSource && !CanSelectFile;

    /// <summary>True when Application rejected the selected file with a typed issue.</summary>
    public bool HasInspectionIssue => InspectionIssue is not null;

    /// <summary>Human-readable typed inspection failure projected from Application.</summary>
    public string InspectionIssueMessage => InspectionIssue is { } issue
        ? _text.GetGeneralSelectedFileInspectionIssue(issue)
        : string.Empty;

    /// <summary>Application-owned draft or admission blocker for this mapping row.</summary>
    public string AuthoringIssueMessage { get; private set; } = string.Empty;

    /// <summary>True when canonical draft or admission validation rejected this mapping.</summary>
    public bool HasAuthoringIssue => !string.IsNullOrEmpty(AuthoringIssueMessage);

    /// <summary>True when either input inspection or authoring admission blocks this row.</summary>
    public bool HasIssue => HasInspectionIssue || HasAuthoringIssue;

    /// <summary>Current canonical row issue, preferring authoring admission over file inspection.</summary>
    public string IssueMessage => HasAuthoringIssue
        ? AuthoringIssueMessage
        : InspectionIssueMessage;

    /// <summary>True while the row displays its empty-slot guidance.</summary>
    public bool IsGuidanceVisible => !HasSource;

    /// <summary>Editable source start; From File Start rows keep this at zero.</summary>
    [ObservableProperty]
    public partial string SourceStartAddress { get; set; } = "0x00000";

    /// <summary>Editable target start inside the output image.</summary>
    [ObservableProperty]
    public partial string TargetStartAddress { get; set; } = "0x00000";

    /// <summary>Editable positive byte length shared by Merge and Replace.</summary>
    [ObservableProperty]
    public partial string Length { get; set; } = "0x00000";

    /// <summary>Updates the one-based display index after rows are added or removed.</summary>
    public void SetIndex(int index)
    {
        Index = index;
        OnPropertyChanged(nameof(Index));
    }

    /// <summary>Projects the current Application-owned selected-file result.</summary>
    internal void ApplyFileInspection(
        FileStamp? acceptedFileStamp,
        GeneralSelectedFileInspectionIssue? issue)
    {
        if (AcceptedFileStamp == acceptedFileStamp && InspectionIssue == issue)
        {
            return;
        }

        AcceptedFileStamp = acceptedFileStamp;
        InspectionIssue = issue;
        OnPropertyChanged(nameof(AcceptedFileStamp));
        OnPropertyChanged(nameof(InspectionIssue));
        OnPropertyChanged(nameof(HasInspectionIssue));
        OnPropertyChanged(nameof(InspectionIssueMessage));
        OnPropertyChanged(nameof(HasIssue));
        OnPropertyChanged(nameof(IssueMessage));
    }

    internal void ApplyAuthoringIssue(string? message)
    {
        message ??= string.Empty;
        if (StringComparer.Ordinal.Equals(AuthoringIssueMessage, message))
        {
            return;
        }

        AuthoringIssueMessage = message;
        OnPropertyChanged(nameof(AuthoringIssueMessage));
        OnPropertyChanged(nameof(HasAuthoringIssue));
        OnPropertyChanged(nameof(HasIssue));
        OnPropertyChanged(nameof(IssueMessage));
    }

    internal void SetFileSelectionAvailability(bool canSelect, string unavailableMessage)
    {
        unavailableMessage = canSelect ? string.Empty : unavailableMessage;
        if (CanSelectFile == canSelect &&
            StringComparer.Ordinal.Equals(
                FileSelectionAvailabilityMessage,
                unavailableMessage))
        {
            return;
        }

        CanSelectFile = canSelect;
        FileSelectionAvailabilityMessage = unavailableMessage;
        OnPropertyChanged(nameof(CanSelectFile));
        OnPropertyChanged(nameof(FileSelectionAvailabilityMessage));
        OnPropertyChanged(nameof(IsFileSelectionPending));
    }

    internal void ApplyPreparation(GeneralAuthoringSessionPreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        AuthoringSlotState? slot = preparation.AcceptedSession?.Slots.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.DefinitionId, MappingId));
        GeneralSelectedFileInspectionIssue? issue =
            preparation.GetSelectedFileIssue(MappingId);
        ApplyFileInspection(
            slot?.FileStamp,
            issue);
    }

    partial void OnFilePathChanged(string? value)
    {
        InvalidateFileInspection();
    }

    internal void InvalidateFileInspection()
    {
        AcceptedFileStamp = null;
        InspectionIssue = null;
        OnPropertyChanged(nameof(AcceptedFileStamp));
        OnPropertyChanged(nameof(InspectionIssue));
        OnPropertyChanged(nameof(HasInspectionIssue));
        OnPropertyChanged(nameof(InspectionIssueMessage));
        OnPropertyChanged(nameof(HasIssue));
        OnPropertyChanged(nameof(IssueMessage));
    }

    /// <summary>Selected local input file path.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyPropertyChangedFor(nameof(HasSource))]
    [NotifyPropertyChangedFor(nameof(IsGuidanceVisible))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(DisplayDetail))]
    public partial string? FilePath { get; set; }
}
