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

    internal AuthoringPublicationLease? CapturePrebindingLease(
        AuthoringSessionState session,
        string path)
    {
        return session.SetSlotFile(MappingId, path, fileStamp: null).Succeeded
            ? session.CapturePublicationLease(AuthoringDerivedResultKind.Inspection)
            : null;
    }

    internal bool TryCacheInspection(
        AuthoringSessionState session,
        AuthoringPublicationLease lease,
        GeneralSelectedFileInspectionResult result)
    {
        bool current = result.Succeeded
            ? session.TryCacheGeneralSelectedFileInspection(lease, result.Inspection!).Succeeded
            : session.IsPublicationCurrent(lease);
        if (current)
        {
            ApplyFileInspection(null, result.Issue);
        }
        return current;
    }

    internal bool TryAcceptCachedInspection(
        AuthoringSessionState session,
        Func<GeneralSelectedFileInspection, AuthoringSlotInspectionStartResult> begin)
    {
        if (FilePath is null ||
            !session.TryGetCachedGeneralSelectedFileInspection(
                MappingId,
                FilePath,
                out GeneralSelectedFileInspection? cached))
        {
            return false;
        }

        AuthoringSlotInspectionStartResult started = begin(cached);
        if (!started.Succeeded)
        {
            return false;
        }

        var current = new GeneralSelectedFileInspection(
            cached.DefinitionId,
            started.Snapshot!.AuthoringRevision,
            cached.SelectedPathHint,
            cached.FileStamp,
            cached.DisplayNameHint,
            cached.LastWriteTimeUtcHint);
        if (!session.TryAcceptSlotFileInspection(started.Lease!, current).Succeeded)
        {
            return false;
        }

        ApplyFileInspection(current.FileStamp, issue: null);
        return true;
    }

    internal bool IsInspectionVerified(AuthoringSessionState session)
    {
        return session.CurrentSnapshot?.Slots.Any(slot =>
            StringComparer.Ordinal.Equals(slot.DefinitionId, MappingId) &&
            slot.Lifecycle == AuthoringSlotLifecycle.Verified) == true;
    }

    internal bool TryPublishInspection(
        AuthoringSessionState session,
        AuthoringSlotInspectionLease lease,
        GeneralSelectedFileInspectionResult result)
    {
        AuthoringSessionTransitionResult transition = result.Succeeded
            ? session.TryAcceptSlotFileInspection(lease, result.Inspection!)
            : session.TryRejectSlotFileInspection(lease, result.Issue!);
        if (!transition.Succeeded)
        {
            return false;
        }

        ApplyFileInspection(result.Inspection?.FileStamp, result.Issue);
        return true;
    }

    partial void OnFilePathChanged(string? value)
    {
        InvalidateFileInspection();
    }

    private void InvalidateFileInspection()
    {
        AcceptedFileStamp = null;
        InspectionIssue = null;
        OnPropertyChanged(nameof(AcceptedFileStamp));
        OnPropertyChanged(nameof(InspectionIssue));
        OnPropertyChanged(nameof(HasInspectionIssue));
        OnPropertyChanged(nameof(InspectionIssueMessage));
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
