using System.Globalization;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReplacePresentationViewModel
{
    private readonly IFirmwareInspection _firmwareInspection;
    private ActiveSessionSnapshot? _ctrlRamFirmwareVersionAcceptedSession;
    private CompiledInputVersionObservation? _ctrlRamFirmwareVersionObservation;
    private CtrlRamFirmwareVersionModalLease? _ctrlRamFirmwareVersionModalLease;
    private long _ctrlRamFirmwareVersionContextGeneration;

    /// <summary>True when the CtrlRAM Build firmware-version confirmation modal is open.</summary>
    public bool IsCtrlRamFirmwareVersionModalOpen { get; private set; }

    /// <summary>True when the pending CtrlRAM Build stages a new TP firmware version.</summary>
    public bool IsCtrlRamFirmwareVersionEditSelected { get; private set; }

    /// <summary>True when the pending CtrlRAM Build preserves the source TP firmware version.</summary>
    public bool IsCtrlRamFirmwareVersionPreserveSelected => !IsCtrlRamFirmwareVersionEditSelected;

    /// <summary>True when the selected base BIN contains valid NVT Backup version metadata.</summary>
    public bool CanEditCtrlRamFirmwareVersion =>
        _ctrlRamFirmwareVersionObservation?.IsKnown == true;

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
    public string CtrlRamFirmwareVersionCurrentValue => _ctrlRamFirmwareVersionObservation is
    { Major: { } version, Minor: { } subVersion }
            ? FormattableString.Invariant($"{version:X2} / {subVersion:X2}")
            : "-- / --";

    /// <summary>Gets the current NVT Backup metadata status for the modal.</summary>
    public string CtrlRamFirmwareVersionMetadataDetail => CanEditCtrlRamFirmwareVersion
        ? Text.CtrlRamFirmwareVersionSourceDetail
        : Text.CtrlRamFirmwareVersionEditUnavailableDetail;

    public string CtrlRamFirmwareVersionValidationDetail { get; private set; } = string.Empty;

    public bool HasCtrlRamFirmwareVersionValidation => !string.IsNullOrWhiteSpace(CtrlRamFirmwareVersionValidationDetail);

    public bool CanConfirmCtrlRamFirmwareVersion =>
        IsCtrlRamFirmwareVersionModalLeaseContextCurrent() &&
        (!IsCtrlRamFirmwareVersionEditSelected || CanEditCtrlRamFirmwareVersion);

    /// <summary>Opens CtrlRAM Build confirmation from the exact accepted immutable session.</summary>
    public Task<bool> TryOpenCtrlRamFirmwareVersionModalAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<bool>(cancellationToken);
        }

        ActiveSessionSnapshot? acceptedSession = _ctrlRamReplaceSession.CurrentSnapshot;
        if (!IsCtrlRamReplaceModeSelected || !CanRunReplace() || acceptedSession is null)
        {
            return Task.FromResult(false);
        }

        CompiledInputVersionObservation? observation = _compositionServices.CtrlRamAuthoring
            .ProjectFirmwareVersionConfirmationLease(acceptedSession);
        if (observation is null ||
            !ReferenceEquals(acceptedSession, _ctrlRamReplaceSession.CurrentSnapshot) ||
            !IsCtrlRamReplaceModeSelected ||
            !CanRunReplace())
        {
            return Task.FromResult(false);
        }

        _ctrlRamFirmwareVersionAcceptedSession = acceptedSession;
        _ctrlRamFirmwareVersionObservation = observation;
        _ctrlRamFirmwareVersionModalLease = new CtrlRamFirmwareVersionModalLease(
            Volatile.Read(ref _ctrlRamFirmwareVersionContextGeneration),
            acceptedSession);
        IsCtrlRamFirmwareVersionEditSelected = false;
        CtrlRamFirmwareVersionText = _ctrlRamFirmwareVersionObservation?.Major is { } version
            ? FormattableString.Invariant($"{version:X2}")
            : string.Empty;
        CtrlRamFirmwareSubVersionText = _ctrlRamFirmwareVersionObservation?.Minor is { } subVersion
            ? FormattableString.Invariant($"{subVersion:X2}")
            : string.Empty;
        ClearCtrlRamFirmwareVersionValidation();
        IsCtrlRamFirmwareVersionModalOpen = true;
        NotifyCtrlRamFirmwareVersionState();
        return Task.FromResult(true);
    }

    /// <summary>Validates the accepted-session lease and creates the typed CtrlRAM version-edit request.</summary>
    public Task<(bool Succeeded, CtrlRamFirmwareVersionDraftState? Edit)>
        TryCreateCtrlRamFirmwareVersionEditAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<(bool Succeeded, CtrlRamFirmwareVersionDraftState? Edit)>(
                cancellationToken);
        }

        if (!IsCtrlRamFirmwareVersionModalOpen ||
            !IsCtrlRamReplaceModeSelected)
        {
            return Task.FromResult<(bool, CtrlRamFirmwareVersionDraftState?)>((false, null));
        }

        if (!IsCtrlRamFirmwareVersionModalLeaseContextCurrent())
        {
            CloseCtrlRamFirmwareVersionModal();
            return Task.FromResult<(bool, CtrlRamFirmwareVersionDraftState?)>((false, null));
        }

        if (!IsCtrlRamFirmwareVersionEditSelected)
        {
            bool preserveLeaseCurrent = ValidateCtrlRamFirmwareVersionModalLease();
            if (!preserveLeaseCurrent)
            {
                CloseCtrlRamFirmwareVersionModal();
            }

            return Task.FromResult<(bool, CtrlRamFirmwareVersionDraftState?)>(
                (preserveLeaseCurrent, null));
        }

        if (!CanEditCtrlRamFirmwareVersion)
        {
            CtrlRamFirmwareVersionValidationDetail = Text.CtrlRamFirmwareVersionEditUnavailableDetail;
            OnPropertyChanged(nameof(CtrlRamFirmwareVersionValidationDetail));
            OnPropertyChanged(nameof(HasCtrlRamFirmwareVersionValidation));
            OnPropertyChanged(nameof(CanEditCtrlRamFirmwareVersion));
            OnPropertyChanged(nameof(CtrlRamFirmwareVersionCurrentValue));
            OnPropertyChanged(nameof(CtrlRamFirmwareVersionMetadataDetail));
            return Task.FromResult<(bool, CtrlRamFirmwareVersionDraftState?)>((false, null));
        }

        if (!TryParseHexByte(CtrlRamFirmwareVersionText, out byte firmwareVersion) ||
            !TryParseHexByte(CtrlRamFirmwareSubVersionText, out byte firmwareSubVersion))
        {
            CtrlRamFirmwareVersionValidationDetail = Text.CtrlRamFirmwareVersionInvalidByteDetail;
            OnPropertyChanged(nameof(CtrlRamFirmwareVersionValidationDetail));
            OnPropertyChanged(nameof(HasCtrlRamFirmwareVersionValidation));
            return Task.FromResult<(bool, CtrlRamFirmwareVersionDraftState?)>((false, null));
        }

        var edit = new CtrlRamFirmwareVersionDraftState(firmwareVersion, firmwareSubVersion);
        ClearCtrlRamFirmwareVersionValidation();
        return Task.FromResult<(bool, CtrlRamFirmwareVersionDraftState?)>((true, edit));
    }

    public Task<bool> IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        return cancellationToken.IsCancellationRequested
            ? Task.FromCanceled<bool>(cancellationToken)
            : Task.FromResult(ValidateCtrlRamFirmwareVersionModalLease());
    }

    /// <summary>Closes the CtrlRAM firmware-version confirmation without changing the source image.</summary>
    public void CloseCtrlRamFirmwareVersionModal()
    {
        _ctrlRamFirmwareVersionModalLease = null;
        _ctrlRamFirmwareVersionAcceptedSession = null;
        _ctrlRamFirmwareVersionObservation = null;
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

        IsCtrlRamFirmwareVersionEditSelected = true;
        ClearCtrlRamFirmwareVersionValidation();
        NotifyCtrlRamFirmwareVersionSelection();
    }

    private void InvalidateCtrlRamFirmwareVersionContext()
    {
        _ = Interlocked.Increment(ref _ctrlRamFirmwareVersionContextGeneration);
        _ctrlRamFirmwareVersionModalLease = null;
        _ctrlRamFirmwareVersionAcceptedSession = null;
        _ctrlRamFirmwareVersionObservation = null;
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
        ActiveSessionSnapshot? currentSession = _ctrlRamReplaceSession.CurrentSnapshot;
        return IsCtrlRamFirmwareVersionModalOpen &&
            IsCtrlRamReplaceModeSelected &&
            CanRunReplace() &&
            currentSession is not null &&
            _ctrlRamFirmwareVersionModalLease is { } lease &&
            lease.ContextGeneration == Volatile.Read(ref _ctrlRamFirmwareVersionContextGeneration) &&
            ReferenceEquals(_ctrlRamFirmwareVersionAcceptedSession, lease.AcceptedSession) &&
            _compositionServices.CtrlRamAuthoring.IsFirmwareVersionConfirmationLeaseCurrent(
                currentSession,
                lease.AcceptedSession);
    }

    private bool ValidateCtrlRamFirmwareVersionModalLease()
    {
        return IsCtrlRamFirmwareVersionModalLeaseContextCurrent();
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

    private readonly record struct CtrlRamFirmwareVersionModalLease(
        long ContextGeneration,
        ActiveSessionSnapshot AcceptedSession);

}
