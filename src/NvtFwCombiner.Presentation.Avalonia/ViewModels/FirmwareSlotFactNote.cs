namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Kind of a fact's always-present value icon (owner decisions 34 and 40).</summary>
internal enum FirmwareSlotFactNoteKind
{
    /// <summary>Outline circle i; the tooltip shows the parse result.</summary>
    Info,

    /// <summary>The existing outline warning triangle; the tooltip leads with the warning lines.</summary>
    Warning,
}

/// <summary>One "Label value" tooltip line of a value note; the label carries its own punctuation.</summary>
internal sealed record FirmwareSlotFactNoteRow(string Label, string Value);

/// <summary>One warning tooltip line drawn with the shared warning triangle in the warning text colour.</summary>
internal sealed record FirmwareSlotFactNoteWarning(string Text)
{
    public string IconPathData { get; } = FirmwareSlotFactViewModel.WarningIconPathData;
}

/// <summary>
/// Localized parse result and optional warnings shown by one value icon placed right after the value.
/// A note never restyles its fact: the fact keeps ordinary label and value presentation.
/// </summary>
internal sealed record FirmwareSlotFactNote
{
    public FirmwareSlotFactNote(
        IReadOnlyList<FirmwareSlotFactNoteRow> rows,
        IReadOnlyList<FirmwareSlotFactNoteWarning> warnings)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(warnings);
        if (rows.Count == 0 || rows.Any(static row => row is null ||
                string.IsNullOrWhiteSpace(row.Label) || string.IsNullOrWhiteSpace(row.Value)) ||
            warnings.Any(static warning => warning is null || string.IsNullOrWhiteSpace(warning.Text)))
        {
            throw new ArgumentException("A value note needs nonblank rows and warnings.", nameof(rows));
        }

        Rows = Array.AsReadOnly([.. rows]);
        Warnings = Array.AsReadOnly([.. warnings]);
    }

    /// <summary>Warning exactly when the note carries at least one warning line.</summary>
    public FirmwareSlotFactNoteKind Kind => Warnings.Count == 0
        ? FirmwareSlotFactNoteKind.Info
        : FirmwareSlotFactNoteKind.Warning;

    public IReadOnlyList<FirmwareSlotFactNoteRow> Rows { get; }

    /// <summary>Warning lines shown before the rows (decision 40).</summary>
    public IReadOnlyList<FirmwareSlotFactNoteWarning> Warnings { get; }

    public bool HasWarnings => Warnings.Count != 0;

    /// <summary>The same content for assistive technology, in tooltip order.</summary>
    public string AutomationText => string.Join(' ',
    [
        .. Warnings.Select(static warning => warning.Text),
        .. Rows.Select(static row => $"{row.Label} {row.Value}."),
    ]);

    public bool Equals(FirmwareSlotFactNote? other)
    {
        return other is not null && Rows.SequenceEqual(other.Rows) && Warnings.SequenceEqual(other.Warnings);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (FirmwareSlotFactNoteRow row in Rows)
        {
            hash.Add(row);
        }

        foreach (FirmwareSlotFactNoteWarning warning in Warnings)
        {
            hash.Add(warning);
        }

        return hash.ToHashCode();
    }
}
