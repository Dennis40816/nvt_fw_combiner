using System.Globalization;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private WorkbenchFirmwareConfigMetadata? _ctrlRamFirmwareVersionMetadata;

    /// <summary>True when the CtrlRAM Build firmware-version confirmation modal is open.</summary>
    public bool IsCtrlRamFirmwareVersionModalOpen { get; private set; }

    /// <summary>True when the pending CtrlRAM Build stages a new TP firmware version.</summary>
    public bool IsCtrlRamFirmwareVersionEditSelected { get; private set; }

    /// <summary>True when the pending CtrlRAM Build preserves the source TP firmware version.</summary>
    public bool IsCtrlRamFirmwareVersionPreserveSelected => !IsCtrlRamFirmwareVersionEditSelected;

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
    public bool CanConfirmCtrlRamFirmwareVersion => !IsCtrlRamFirmwareVersionEditSelected || CanEditCtrlRamFirmwareVersion;

    /// <summary>Opens the CtrlRAM Build confirmation when the selected base and replacement inputs are ready.</summary>
    public bool TryOpenCtrlRamFirmwareVersionModal()
    {
        if (!IsCtrlRamReplaceModeSelected || !CanBuildReplace)
        {
            return false;
        }

        _ctrlRamFirmwareVersionMetadata = ReadCtrlRamFirmwareVersionMetadata();
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

    /// <summary>Creates the typed CtrlRAM version-edit request selected in the modal.</summary>
    public bool TryCreateCtrlRamFirmwareVersionEdit(out WorkbenchCtrlRamFirmwareVersionEdit? edit)
    {
        edit = null;
        if (!IsCtrlRamFirmwareVersionModalOpen || !IsCtrlRamReplaceModeSelected)
        {
            return false;
        }

        if (!IsCtrlRamFirmwareVersionEditSelected)
        {
            return true;
        }

        _ctrlRamFirmwareVersionMetadata = ReadCtrlRamFirmwareVersionMetadata();
        if (!CanEditCtrlRamFirmwareVersion)
        {
            CtrlRamFirmwareVersionValidationDetail = Text.CtrlRamFirmwareVersionEditUnavailableDetail;
            OnPropertyChanged(nameof(CtrlRamFirmwareVersionValidationDetail));
            OnPropertyChanged(nameof(HasCtrlRamFirmwareVersionValidation));
            OnPropertyChanged(nameof(CanEditCtrlRamFirmwareVersion));
            OnPropertyChanged(nameof(CtrlRamFirmwareVersionCurrentValue));
            OnPropertyChanged(nameof(CtrlRamFirmwareVersionMetadataDetail));
            return false;
        }

        if (!TryParseHexByte(CtrlRamFirmwareVersionText, out byte firmwareVersion) ||
            !TryParseHexByte(CtrlRamFirmwareSubVersionText, out byte firmwareSubVersion))
        {
            CtrlRamFirmwareVersionValidationDetail = Text.CtrlRamFirmwareVersionInvalidByteDetail;
            OnPropertyChanged(nameof(CtrlRamFirmwareVersionValidationDetail));
            OnPropertyChanged(nameof(HasCtrlRamFirmwareVersionValidation));
            return false;
        }

        edit = new WorkbenchCtrlRamFirmwareVersionEdit(firmwareVersion, firmwareSubVersion);
        ClearCtrlRamFirmwareVersionValidation();
        return true;
    }

    /// <summary>Closes the CtrlRAM firmware-version confirmation without changing the source image.</summary>
    public void CloseCtrlRamFirmwareVersionModal()
    {
        if (!IsCtrlRamFirmwareVersionModalOpen)
        {
            return;
        }

        IsCtrlRamFirmwareVersionModalOpen = false;
        ClearCtrlRamFirmwareVersionValidation();
        OnPropertyChanged(nameof(IsCtrlRamFirmwareVersionModalOpen));
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

    private WorkbenchFirmwareConfigMetadata? ReadCtrlRamFirmwareVersionMetadata()
    {
        string? basePath = ReplaceBaseSlot.FilePath;
        return !string.IsNullOrWhiteSpace(basePath)
            ? UiCompositionRunner.TryReadFirmwareConfigMetadata(SelectedIc, basePath)
            : null;
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
}
