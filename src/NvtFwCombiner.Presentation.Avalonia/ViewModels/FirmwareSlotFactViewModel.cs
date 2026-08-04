namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One typed firmware fact projected below a selected BIN file name.</summary>
public sealed record FirmwareSlotFactViewModel
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

    /// <summary>Localized fact label.</summary>
    public string Label { get; }

    /// <summary>Formatted fact value.</summary>
    public string Value { get; }

    /// <summary>Typed semantic state.</summary>
    public FirmwareSlotFactState State { get; }

    /// <summary>Localized semantic state label when the fact is not ordinary.</summary>
    public string? StateLabel { get; }

    /// <summary>Localized reason and next action when the fact is not ordinary.</summary>
    public string? StateDetail { get; }

    /// <summary>True when the fact is explicitly unknown.</summary>
    public bool IsUnknown => State == FirmwareSlotFactState.Unknown;

    /// <summary>True when the fact depends on another input.</summary>
    public bool IsPendingInput => State == FirmwareSlotFactState.PendingInput;

    /// <summary>True when the fact is excluded from the active projection.</summary>
    public bool IsNotApplicable => State == FirmwareSlotFactState.NotApplicable;

    /// <summary>True when the fact is a non-blocking warning.</summary>
    public bool IsWarning => State == FirmwareSlotFactState.Warning;

    /// <summary>True when the fact is blocking.</summary>
    public bool IsError => State == FirmwareSlotFactState.Error;

    /// <summary>True when the compact fact requires an explicit semantic marker.</summary>
    public bool HasStateIcon => State is not FirmwareSlotFactState.Ordinary and not FirmwareSlotFactState.NotApplicable;

    /// <summary>Vector marker for unknown, pending, warning, or error fact state.</summary>
    public string StateIconPathData => State switch
    {
        FirmwareSlotFactState.Unknown => "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M9.5 9A2.5 2.5 0 0 1 14.5 9C14.5 11 12 11 12 13 M12 17H12.01",
        FirmwareSlotFactState.PendingInput => "M12 3A9 9 0 1 0 21 12 M12 7V12L15 14",
        FirmwareSlotFactState.Warning => "M12 3L22 20H2L12 3 M12 9V14 M12 17H12.01",
        FirmwareSlotFactState.Error => "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M12 7V13 M12 17H12.01",
        FirmwareSlotFactState.Ordinary or FirmwareSlotFactState.NotApplicable => string.Empty,
        _ => string.Empty,
    };

    /// <summary>Localized state, reason, and next action exposed by pointer, keyboard, and assistive technology.</summary>
    public string StateAutomationText => State is FirmwareSlotFactState.Ordinary or FirmwareSlotFactState.NotApplicable
        ? $"{Label}: {Value}"
        : string.Join(": ", Label, Value, StateLabel, StateDetail);

}
