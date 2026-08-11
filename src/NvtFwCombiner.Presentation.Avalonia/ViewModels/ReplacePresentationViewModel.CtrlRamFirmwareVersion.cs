using System.Globalization;
using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReplacePresentationViewModel
{
    private readonly Func<string, string, FirmwareConfigMetadataSnapshot?> _ctrlRamFirmwareVersionMetadataReader;
    private FirmwareConfigMetadataSnapshot? _ctrlRamFirmwareVersionMetadata;
    private CtrlRamFirmwareVersionModalLease? _ctrlRamFirmwareVersionModalLease;
    private long _ctrlRamFirmwareVersionContextGeneration;
    private long _ctrlRamFirmwareVersionMetadataGeneration;

    /// <summary>True when the CtrlRAM Build firmware-version confirmation modal is open.</summary>
    public bool IsCtrlRamFirmwareVersionModalOpen { get; private set; }

    /// <summary>True when the pending CtrlRAM Build stages a new TP firmware version.</summary>
    public bool IsCtrlRamFirmwareVersionEditSelected { get; private set; }

    /// <summary>True when the pending CtrlRAM Build preserves the source TP firmware version.</summary>
    public bool IsCtrlRamFirmwareVersionPreserveSelected => !IsCtrlRamFirmwareVersionEditSelected;

    /// <summary>True while CtrlRAM version metadata is read outside the UI dispatcher.</summary>
    public bool IsCtrlRamFirmwareVersionMetadataLoading { get; private set; }

    /// <summary>True when the selected base BIN contains valid NVT Backup version metadata.</summary>
    public bool CanEditCtrlRamFirmwareVersion => _ctrlRamFirmwareVersionMetadata is { IsFirmwareVersionBarValid: true };

    /// <summary>Gets or sets the staged TP firmware-version byte text.</summary>
    public string CtrlRamFirmwareVersionText
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            ClearCtrlRamFirmwareVersionValidation();
            OnPropertyChanged(nameof(CtrlRamFirmwareVersionText));
        }
    } = string.Empty;

    /// <summary>Gets or sets the staged TP firmware sub-version byte text.</summary>
    public string CtrlRamFirmwareSubVersionText
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            ClearCtrlRamFirmwareVersionValidation();
            OnPropertyChanged(nameof(CtrlRamFirmwareSubVersionText));
        }
    } = string.Empty;

    /// <summary>Gets the current TP firmware version decoded from the selected base BIN NVT Backup.</summary>
    public string CtrlRamFirmwareVersionCurrentValue => _ctrlRamFirmwareVersionMetadata is { } metadata
        ? FormattableString.Invariant($"{metadata.FirmwareVersion:X2} / {metadata.FirmwareSubVersion:X2}")
        : "-- / --";

    /// <summary>Gets the current NVT Backup metadata status for the modal.</summary>
    public string CtrlRamFirmwareVersionMetadataDetail => CanEditCtrlRamFirmwareVersion
        ? Text.CtrlRamFirmwareVersionSourceDetail
        : Text.CtrlRamFirmwareVersionEditUnavailableDetail;

    /// <summary>Gets the input-validation detail for the modal.</summary>
    public string CtrlRamFirmwareVersionValidationDetail { get; private set; } = string.Empty;

    /// <summary>True when the modal has an input-validation detail to show.</summary>
    public bool HasCtrlRamFirmwareVersionValidation => !string.IsNullOrWhiteSpace(CtrlRamFirmwareVersionValidationDetail);

    /// <summary>True when the modal can move from version confirmation to the output file picker.</summary>
    public bool CanConfirmCtrlRamFirmwareVersion =>
        !IsCtrlRamFirmwareVersionMetadataLoading &&
        IsCtrlRamFirmwareVersionModalLeaseContextCurrent() &&
        (!IsCtrlRamFirmwareVersionEditSelected || CanEditCtrlRamFirmwareVersion);

    /// <summary>Reads the selected base outside the dispatcher, then opens the CtrlRAM Build confirmation.</summary>
    public async Task<bool> TryOpenCtrlRamFirmwareVersionModalAsync(CancellationToken cancellationToken = default)
    {
        if (!IsCtrlRamReplaceModeSelected || !CanRunReplace() || IsCtrlRamFirmwareVersionMetadataLoading)
        {
            return false;
        }

        CtrlRamFirmwareVersionMetadataReadResult readResult =
            await ReadCtrlRamFirmwareVersionMetadataAsync(cancellationToken);
        if (!readResult.IsCurrent || !IsCtrlRamReplaceModeSelected || !CanRunReplace())
        {
            return false;
        }

        _ctrlRamFirmwareVersionMetadata = readResult.Metadata;
        _ctrlRamFirmwareVersionModalLease = new CtrlRamFirmwareVersionModalLease(
            Volatile.Read(ref _ctrlRamFirmwareVersionContextGeneration),
            readResult.Request);
        IsCtrlRamFirmwareVersionEditSelected = false;
        CtrlRamFirmwareVersionText = _ctrlRamFirmwareVersionMetadata is { } metadata
            ? FormattableString.Invariant($"{metadata.FirmwareVersion:X2}")
            : string.Empty;
        CtrlRamFirmwareSubVersionText = _ctrlRamFirmwareVersionMetadata is { } subVersionMetadata
            ? FormattableString.Invariant($"{subVersionMetadata.FirmwareSubVersion:X2}")
            : string.Empty;
        ClearCtrlRamFirmwareVersionValidation();
        IsCtrlRamFirmwareVersionModalOpen = true;
        NotifyCtrlRamFirmwareVersionState();
        return true;
    }

    /// <summary>Revalidates metadata outside the dispatcher and creates the typed CtrlRAM version-edit request.</summary>
    public async Task<(bool Succeeded, CtrlRamFirmwareVersionDraftState? Edit)>
        TryCreateCtrlRamFirmwareVersionEditAsync(CancellationToken cancellationToken = default)
    {
        if (!IsCtrlRamFirmwareVersionModalOpen ||
            !IsCtrlRamReplaceModeSelected ||
            IsCtrlRamFirmwareVersionMetadataLoading)
        {
            return (false, null);
        }

        if (!IsCtrlRamFirmwareVersionModalLeaseContextCurrent())
        {
            CloseCtrlRamFirmwareVersionModal();
            return (false, null);
        }

        if (!IsCtrlRamFirmwareVersionEditSelected)
        {
            bool preserveLeaseCurrent = await ValidateCtrlRamFirmwareVersionModalLeaseAsync(cancellationToken);
            if (!preserveLeaseCurrent)
            {
                CloseCtrlRamFirmwareVersionModal();
            }

            return (preserveLeaseCurrent, null);
        }

        CtrlRamFirmwareVersionMetadataReadResult readResult =
            await ReadCtrlRamFirmwareVersionMetadataAsync(cancellationToken);
        if (!readResult.IsCurrent ||
            !IsCtrlRamFirmwareVersionModalOpen ||
            !IsCtrlRamReplaceModeSelected ||
            !IsCtrlRamFirmwareVersionEditSelected ||
            !IsCtrlRamFirmwareVersionModalLeaseMatches(readResult.Request))
        {
            return (false, null);
        }

        _ctrlRamFirmwareVersionMetadata = readResult.Metadata;
        NotifyCtrlRamFirmwareVersionState();
        if (!CanEditCtrlRamFirmwareVersion)
        {
            CtrlRamFirmwareVersionValidationDetail = Text.CtrlRamFirmwareVersionEditUnavailableDetail;
            OnPropertyChanged(nameof(CtrlRamFirmwareVersionValidationDetail));
            OnPropertyChanged(nameof(HasCtrlRamFirmwareVersionValidation));
            OnPropertyChanged(nameof(CanEditCtrlRamFirmwareVersion));
            OnPropertyChanged(nameof(CtrlRamFirmwareVersionCurrentValue));
            OnPropertyChanged(nameof(CtrlRamFirmwareVersionMetadataDetail));
            return (false, null);
        }

        if (!TryParseHexByte(CtrlRamFirmwareVersionText, out byte firmwareVersion) ||
            !TryParseHexByte(CtrlRamFirmwareSubVersionText, out byte firmwareSubVersion))
        {
            CtrlRamFirmwareVersionValidationDetail = Text.CtrlRamFirmwareVersionInvalidByteDetail;
            OnPropertyChanged(nameof(CtrlRamFirmwareVersionValidationDetail));
            OnPropertyChanged(nameof(HasCtrlRamFirmwareVersionValidation));
            return (false, null);
        }

        var edit = new CtrlRamFirmwareVersionDraftState(firmwareVersion, firmwareSubVersion);
        ClearCtrlRamFirmwareVersionValidation();
        return (true, edit);
    }

    /// <summary>True when the modal confirmation still belongs to the current CtrlRAM inputs.</summary>
    public Task<bool> IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        return ValidateCtrlRamFirmwareVersionModalLeaseAsync(cancellationToken);
    }

    /// <summary>Closes the CtrlRAM firmware-version confirmation without changing the source image.</summary>
    public void CloseCtrlRamFirmwareVersionModal()
    {
        InvalidateCtrlRamFirmwareVersionMetadataRead();
        _ctrlRamFirmwareVersionModalLease = null;
        if (!IsCtrlRamFirmwareVersionModalOpen)
        {
            return;
        }

        IsCtrlRamFirmwareVersionModalOpen = false;
        ClearCtrlRamFirmwareVersionValidation();
        OnPropertyChanged(nameof(IsCtrlRamFirmwareVersionModalOpen));
        OnPropertyChanged(nameof(CanConfirmCtrlRamFirmwareVersion));
    }

    private void SelectCtrlRamFirmwareVersionPreserve()
    {
        InvalidateCtrlRamFirmwareVersionMetadataRead();
        IsCtrlRamFirmwareVersionEditSelected = false;
        ClearCtrlRamFirmwareVersionValidation();
        NotifyCtrlRamFirmwareVersionSelection();
    }

    private void SelectCtrlRamFirmwareVersionEdit()
    {
        if (!CanEditCtrlRamFirmwareVersion)
        {
            return;
        }

        InvalidateCtrlRamFirmwareVersionMetadataRead();
        IsCtrlRamFirmwareVersionEditSelected = true;
        ClearCtrlRamFirmwareVersionValidation();
        NotifyCtrlRamFirmwareVersionSelection();
    }

    private async Task<CtrlRamFirmwareVersionMetadataReadResult> ReadCtrlRamFirmwareVersionMetadataAsync(
        CancellationToken cancellationToken)
    {
        string icId = SelectedIc;
        string? basePath = ReplaceBaseSlot.FilePath;
        if (string.IsNullOrWhiteSpace(icId) || string.IsNullOrWhiteSpace(basePath))
        {
            return default;
        }

        long generation = Interlocked.Increment(ref _ctrlRamFirmwareVersionMetadataGeneration);
        SetCtrlRamFirmwareVersionMetadataLoading(true);
        try
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            CtrlRamFirmwareVersionMetadataWorkerResult workerResult = await Task.Run(
                () => ReadCtrlRamFirmwareVersionMetadata(icId, basePath),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            bool isCurrent = workerResult.IsFileIdentityStable &&
                generation == Volatile.Read(ref _ctrlRamFirmwareVersionMetadataGeneration) &&
                workerResult.Request.MatchesContext(SelectedIc, ReplaceBaseSlot.FilePath);
            return new CtrlRamFirmwareVersionMetadataReadResult(
                isCurrent,
                workerResult.Metadata,
                workerResult.Request);
        }
        finally
        {
            SetCtrlRamFirmwareVersionMetadataLoading(false);
        }
    }

    private CtrlRamFirmwareVersionMetadataWorkerResult ReadCtrlRamFirmwareVersionMetadata(
        string icId,
        string basePath)
    {
        var before = FirmwareFileIdentity.Capture(basePath);
        FirmwareConfigMetadataSnapshot? metadata = _ctrlRamFirmwareVersionMetadataReader(icId, basePath);
        var after = FirmwareFileIdentity.Capture(basePath);
        return new CtrlRamFirmwareVersionMetadataWorkerResult(
            before.Equals(after),
            metadata,
            new CtrlRamFirmwareVersionMetadataRequest(icId, basePath, before));
    }

    private void InvalidateCtrlRamFirmwareVersionMetadataRead()
    {
        _ = Interlocked.Increment(ref _ctrlRamFirmwareVersionMetadataGeneration);
    }

    private void InvalidateCtrlRamFirmwareVersionContext()
    {
        InvalidateCtrlRamFirmwareVersionMetadataRead();
        _ = Interlocked.Increment(ref _ctrlRamFirmwareVersionContextGeneration);
        _ctrlRamFirmwareVersionModalLease = null;
        if (!IsCtrlRamFirmwareVersionModalOpen)
        {
            OnPropertyChanged(nameof(CanConfirmCtrlRamFirmwareVersion));
            return;
        }

        IsCtrlRamFirmwareVersionModalOpen = false;
        ClearCtrlRamFirmwareVersionValidation();
        NotifyCtrlRamFirmwareVersionState();
    }

    private bool IsCtrlRamFirmwareVersionModalLeaseContextCurrent()
    {
        return IsCtrlRamFirmwareVersionModalOpen &&
            IsCtrlRamReplaceModeSelected &&
            CanRunReplace() &&
            _ctrlRamFirmwareVersionModalLease is { } lease &&
            lease.ContextGeneration == Volatile.Read(ref _ctrlRamFirmwareVersionContextGeneration);
    }

    private bool IsCtrlRamFirmwareVersionModalLeaseMatches(CtrlRamFirmwareVersionMetadataRequest request)
    {
        return IsCtrlRamFirmwareVersionModalLeaseContextCurrent() &&
            _ctrlRamFirmwareVersionModalLease is { } lease &&
            lease.Request.Equals(request);
    }

    private async Task<bool> ValidateCtrlRamFirmwareVersionModalLeaseAsync(
        CancellationToken cancellationToken)
    {
        if (IsCtrlRamFirmwareVersionMetadataLoading ||
            !IsCtrlRamFirmwareVersionModalLeaseContextCurrent() ||
            _ctrlRamFirmwareVersionModalLease is not { } lease)
        {
            return false;
        }

        long generation = Interlocked.Increment(ref _ctrlRamFirmwareVersionMetadataGeneration);
        SetCtrlRamFirmwareVersionMetadataLoading(true);
        try
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            FirmwareFileIdentity identity = await Task.Run(
                () => FirmwareFileIdentity.Capture(lease.Request.BasePath),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return generation == Volatile.Read(ref _ctrlRamFirmwareVersionMetadataGeneration) &&
                IsCtrlRamFirmwareVersionModalLeaseContextCurrent() &&
                _ctrlRamFirmwareVersionModalLease == lease &&
                lease.Request.FileIdentity.Equals(identity);
        }
        finally
        {
            SetCtrlRamFirmwareVersionMetadataLoading(false);
        }
    }

    private void SetCtrlRamFirmwareVersionMetadataLoading(bool isLoading)
    {
        if (IsCtrlRamFirmwareVersionMetadataLoading == isLoading)
        {
            return;
        }

        IsCtrlRamFirmwareVersionMetadataLoading = isLoading;
        OnPropertyChanged(nameof(IsCtrlRamFirmwareVersionMetadataLoading));
        OnPropertyChanged(nameof(CanBuildReplace));
        OnPropertyChanged(nameof(CanConfirmCtrlRamFirmwareVersion));
        BuildReplaceCommand.NotifyCanExecuteChanged();
    }

    private static bool TryParseHexByte(string? text, out byte value)
    {
        value = 0;
        return text is { Length: 2 } &&
            byte.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
    }

    private void ClearCtrlRamFirmwareVersionValidation()
    {
        if (string.IsNullOrEmpty(CtrlRamFirmwareVersionValidationDetail))
        {
            return;
        }

        CtrlRamFirmwareVersionValidationDetail = string.Empty;
        OnPropertyChanged(nameof(CtrlRamFirmwareVersionValidationDetail));
        OnPropertyChanged(nameof(HasCtrlRamFirmwareVersionValidation));
    }

    private void NotifyCtrlRamFirmwareVersionState()
    {
        OnPropertyChanged(nameof(IsCtrlRamFirmwareVersionModalOpen));
        OnPropertyChanged(nameof(IsCtrlRamFirmwareVersionEditSelected));
        OnPropertyChanged(nameof(IsCtrlRamFirmwareVersionPreserveSelected));
        OnPropertyChanged(nameof(CanEditCtrlRamFirmwareVersion));
        OnPropertyChanged(nameof(CtrlRamFirmwareVersionCurrentValue));
        OnPropertyChanged(nameof(CtrlRamFirmwareVersionMetadataDetail));
        OnPropertyChanged(nameof(CanConfirmCtrlRamFirmwareVersion));
    }

    private void NotifyCtrlRamFirmwareVersionSelection()
    {
        OnPropertyChanged(nameof(IsCtrlRamFirmwareVersionEditSelected));
        OnPropertyChanged(nameof(IsCtrlRamFirmwareVersionPreserveSelected));
        OnPropertyChanged(nameof(CanConfirmCtrlRamFirmwareVersion));
    }

    private readonly record struct CtrlRamFirmwareVersionMetadataReadResult(
        bool IsCurrent,
        FirmwareConfigMetadataSnapshot? Metadata,
        CtrlRamFirmwareVersionMetadataRequest Request);

    private readonly record struct CtrlRamFirmwareVersionMetadataWorkerResult(
        bool IsFileIdentityStable,
        FirmwareConfigMetadataSnapshot? Metadata,
        CtrlRamFirmwareVersionMetadataRequest Request);

    private readonly record struct CtrlRamFirmwareVersionModalLease(
        long ContextGeneration,
        CtrlRamFirmwareVersionMetadataRequest Request);

    private readonly record struct CtrlRamFirmwareVersionMetadataRequest(
        string IcId,
        string BasePath,
        FirmwareFileIdentity FileIdentity)
    {
        internal bool MatchesContext(string icId, string? basePath)
        {
            return string.Equals(IcId, icId, StringComparison.Ordinal) &&
                string.Equals(BasePath, basePath, StringComparison.Ordinal);
        }
    }

}
