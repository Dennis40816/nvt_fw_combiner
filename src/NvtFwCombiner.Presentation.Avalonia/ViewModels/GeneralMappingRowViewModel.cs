using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal abstract partial class GeneralMappingRowViewModel : ObservableObject
{
    private readonly ShellTextResources _text;

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

    public string MappingId { get; }

    public int Index { get; private set; }

    public virtual string DisplayName => HasFile ? Path.GetFileName(FilePath!) : field;

    public virtual string DisplayDetail => HasFile ? FirmwarePathDisplay.Normalize(FilePath!) : string.Empty;

    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

    public virtual bool HasSource => HasFile;

    public virtual bool UsesFileSource => true;

    public virtual bool UsesInlineSource => false;

    public virtual string SourceKindIcon => "BIN";

    /// <summary>Accepted identity of the currently selected file bytes.</summary>
    public FileStamp? AcceptedFileStamp { get; private set; }

    public GeneralSelectedFileInspectionIssue? InspectionIssue { get; private set; }

    public bool CanSelectFile { get; private set; } = true;

    public string FileSelectionAvailabilityMessage { get; private set; } = string.Empty;

    public bool IsFileSelectionPending => UsesFileSource && !CanSelectFile;

    public bool HasInspectionIssue => InspectionIssue is not null;

    public string InspectionIssueMessage => InspectionIssue is { } issue
        ? _text.GetGeneralSelectedFileInspectionIssue(issue)
        : string.Empty;

    /// <summary>Application-owned draft or admission blocker for this mapping row.</summary>
    public string AuthoringIssueMessage { get; private set; } = string.Empty;

    public bool HasAuthoringIssue => !string.IsNullOrEmpty(AuthoringIssueMessage);

    public bool HasIssue => HasInspectionIssue || HasAuthoringIssue;

    public string IssueMessage => HasAuthoringIssue
        ? AuthoringIssueMessage
        : InspectionIssueMessage;

    public bool IsGuidanceVisible => !HasSource;

    [ObservableProperty]
    public partial string SourceStartAddress { get; set; } = "0x00000";

    [ObservableProperty]
    public partial string TargetStartAddress { get; set; } = "0x00000";

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyPropertyChangedFor(nameof(HasSource))]
    [NotifyPropertyChangedFor(nameof(IsGuidanceVisible))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(DisplayDetail))]
    public partial string? FilePath { get; set; }
}
