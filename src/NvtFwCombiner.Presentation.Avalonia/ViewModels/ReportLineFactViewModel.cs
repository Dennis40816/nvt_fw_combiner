namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Label/value fact shown in report evidence rows.</summary>
public sealed class ReportLineFactViewModel
{
    /// <summary>Creates a report fact.</summary>
    public ReportLineFactViewModel(string label, string value, bool isTechnical = false)
    {
        Label = label ?? string.Empty;
        Value = value ?? string.Empty;
        IsTechnical = isTechnical;
        IsPlainText = !isTechnical;
    }

    /// <summary>Short field label.</summary>
    public string Label { get; }

    /// <summary>Field value.</summary>
    public string Value { get; }

    /// <summary>True when the value should use fixed-width technical typography.</summary>
    public bool IsTechnical { get; }

    /// <summary>True when the value should use normal typography.</summary>
    public bool IsPlainText { get; }
}
