using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Independent version editor for one inspected bank; contains no page or firmware logic.</summary>
internal sealed class CtrlRamFirmwareVersionEditorViewModel : ObservableObject
{
    internal CtrlRamFirmwareVersionEditorViewModel(string bankId, string title,
        CompiledInputVersionObservation? observation, CtrlRamFirmwareVersionDraftState? edit,
        ShellTextResources text)
    {
        BankId = bankId;
        Title = title;
        Text = text;
        CanEdit = observation?.IsKnown == true;
        CurrentValue = observation is { Major: { } major, Minor: { } minor }
            ? FormattableString.Invariant($"{major:X2} / {minor:X2}") : "-- / --";
        VersionText = (edit?.FirmwareVersion ?? observation?.Major)?.ToString("X2", CultureInfo.InvariantCulture) ?? string.Empty;
        SubVersionText = (edit?.FirmwareSubVersion ?? observation?.Minor)?.ToString("X2", CultureInfo.InvariantCulture) ?? string.Empty;
        IsEditSelected = edit is not null;
        PreserveCommand = new RelayCommand(() => SelectEdit(false));
        EditCommand = new RelayCommand(() => SelectEdit(true), () => CanEdit);
    }

    internal string BankId { get; }
    public string Title { get; }
    public string CurrentValue { get; }
    public bool CanEdit { get; }
    public ShellTextResources Text { get; private set; }
    public bool IsEditSelected { get; private set; }
    public bool IsPreserveSelected => !IsEditSelected;
    public string Detail => CanEdit ? Text.CtrlRamFirmwareVersionDetail : Text.CtrlRamFirmwareVersionEditUnavailableDetail;
    public string Validation { get; private set; } = string.Empty;
    public bool HasValidation => Validation.Length != 0;
    public IRelayCommand PreserveCommand { get; }
    public IRelayCommand EditCommand { get; }

    public string VersionText
    {
        get;
        set
        {
            field = value;
            ClearValidation();
            OnPropertyChanged();
        }
    }

    public string SubVersionText
    {
        get;
        set
        {
            field = value;
            ClearValidation();
            OnPropertyChanged();
        }
    }

    internal bool TryCreateDraft(out CtrlRamFirmwareVersionDraftState? edit)
    {
        edit = null;
        if (!IsEditSelected)
        {
            return true;
        }
        if (!CanEdit || !TryParseHexByte(VersionText, out byte version) ||
            !TryParseHexByte(SubVersionText, out byte subVersion))
        {
            Validation = CanEdit ? Text.CtrlRamFirmwareVersionInvalidByteDetail : Text.CtrlRamFirmwareVersionEditUnavailableDetail;
            OnPropertyChanged(nameof(Validation));
            OnPropertyChanged(nameof(HasValidation));
            return false;
        }
        edit = new CtrlRamFirmwareVersionDraftState(version, subVersion);
        return true;
    }

    internal static bool TryParseHexByte(string? text, out byte value)
    {
        value = 0;
        return text is { Length: 2 } &&
            byte.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
    }

    internal void ApplyText(ShellTextResources text)
    {
        Text = text;
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(Detail));
        if (HasValidation)
        {
            _ = TryCreateDraft(out _);
        }
    }

    private void SelectEdit(bool edit)
    {
        IsEditSelected = edit;
        ClearValidation();
        OnPropertyChanged(nameof(IsEditSelected));
        OnPropertyChanged(nameof(IsPreserveSelected));
    }

    private void ClearValidation()
    {
        Validation = string.Empty;
        OnPropertyChanged(nameof(Validation));
        OnPropertyChanged(nameof(HasValidation));
    }
}
