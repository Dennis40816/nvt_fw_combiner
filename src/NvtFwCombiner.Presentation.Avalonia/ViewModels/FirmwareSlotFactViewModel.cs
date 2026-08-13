namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One typed firmware fact projected below a selected BIN file name.</summary>
internal sealed record FirmwareSlotFactViewModel
{
    /// <summary>Creates one fact and requires localized help for every visible non-ordinary state.</summary>
    public FirmwareSlotFactViewModel(
        string label,
        string value,
        FirmwareSlotFactState state = FirmwareSlotFactState.Ordinary,
        string? stateLabel = null,
        string? stateDetail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Firmware fact state must be declared.");
        }

        if (state is not FirmwareSlotFactState.Ordinary and not FirmwareSlotFactState.NotApplicable)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stateLabel);
            ArgumentException.ThrowIfNullOrWhiteSpace(stateDetail);
        }

        Label = label;
        Value = value;
        State = state;
        StateLabel = stateLabel;
        StateDetail = stateDetail;
    }

    public string Label { get; }

    public string Value { get; }

    public FirmwareSlotFactState State { get; }

    public string? StateLabel { get; }

    public string? StateDetail { get; }

    public bool IsUnknown => State == FirmwareSlotFactState.Unknown;

    public bool IsPendingInput => State == FirmwareSlotFactState.PendingInput;

    public bool IsNotApplicable => State == FirmwareSlotFactState.NotApplicable;

    public bool IsWarning => State == FirmwareSlotFactState.Warning;

    public bool IsError => State == FirmwareSlotFactState.Error;

    public bool HasStateIcon => State is not FirmwareSlotFactState.Ordinary and not FirmwareSlotFactState.NotApplicable;

    public string StateIconPathData => State switch
    {
        FirmwareSlotFactState.Unknown => "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M9.5 9A2.5 2.5 0 0 1 14.5 9C14.5 11 12 11 12 13 M12 17H12.01",
        FirmwareSlotFactState.PendingInput => "M12 3A9 9 0 1 0 21 12 M12 7V12L15 14",
        FirmwareSlotFactState.Warning => "M12 3L22 20H2L12 3 M12 9V14 M12 17H12.01",
        FirmwareSlotFactState.Error => "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M12 7V13 M12 17H12.01",
        FirmwareSlotFactState.Ordinary or FirmwareSlotFactState.NotApplicable => string.Empty,
        _ => string.Empty,
    };

    public string StateAutomationText => State is FirmwareSlotFactState.Ordinary or FirmwareSlotFactState.NotApplicable
        ? $"{Label}: {Value}"
        : string.Join(": ", Label, Value, StateLabel, StateDetail);

}
