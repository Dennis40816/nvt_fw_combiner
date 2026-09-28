namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Presentation priority declared where a typed observation is formatted.</summary>
internal enum FirmwareSlotFactPriority { Primary, Details }

/// <summary>One typed firmware fact projected below a selected BIN file name.</summary>
internal sealed record FirmwareSlotFactViewModel
{
    /// <summary>The existing outline warning triangle, shared by warning states and warning value notes.</summary>
    internal const string WarningIconPathData = "M12 3L22 20H2L12 3 M12 9V14 M12 17H12.01";

    /// <summary>Outline circle i of the existing issue-card information glyph, used by information value notes.</summary>
    internal const string InfoIconPathData = "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M12 11V17 M12 7H12.01";

    /// <summary>Creates one fact and requires localized help for every visible non-ordinary state.</summary>
    public FirmwareSlotFactViewModel(
        string label,
        string value,
        FirmwareSlotFactState state = FirmwareSlotFactState.Ordinary,
        string? stateLabel = null,
        string? stateDetail = null,
        FirmwareSlotFactPriority priority = FirmwareSlotFactPriority.Primary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Firmware fact state must be declared.");
        }

        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), priority, "Firmware fact priority must be declared.");
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
        Priority = priority;
    }

    public string Label { get; }

    public string Value { get; }

    public FirmwareSlotFactPriority Priority { get; }

    public bool IsPrimary => Priority == FirmwareSlotFactPriority.Primary ||
        State is FirmwareSlotFactState.Error or FirmwareSlotFactState.Warning or FirmwareSlotFactState.PendingInput;

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
        FirmwareSlotFactState.Warning => WarningIconPathData,
        FirmwareSlotFactState.Error => "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M12 7V13 M12 17H12.01",
        FirmwareSlotFactState.Ordinary or FirmwareSlotFactState.NotApplicable => string.Empty,
        _ => string.Empty,
    };

    public string StateAutomationText => Note is { } note
        ? $"{Label}: {Value}. {note.AutomationText}"
        : State is FirmwareSlotFactState.Ordinary or FirmwareSlotFactState.NotApplicable
            ? $"{Label}: {Value}"
            : string.Join(": ", Label, Value, StateLabel, StateDetail);

    /// <summary>
    /// Optional always-present value icon and tooltip (decisions 34 and 40). Only an Ordinary fact may carry
    /// one, so a note never changes the fact's state styling or the existing state icon.
    /// </summary>
    public FirmwareSlotFactNote? Note
    {
        get;
        init
        {
            if (value is not null && State != FirmwareSlotFactState.Ordinary)
            {
                throw new ArgumentException("Only an ordinary firmware fact can carry a value note.", nameof(value));
            }

            field = value;
        }
    }

    public bool HasNote => Note is not null;

    public bool IsNoteWarning => Note?.Kind == FirmwareSlotFactNoteKind.Warning;

    public string NoteIconPathData => Note is null
        ? string.Empty
        : IsNoteWarning ? WarningIconPathData : InfoIconPathData;
}
