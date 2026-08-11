namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Visual tone for one reusable accessible disclosure status.</summary>
public enum DisclosureStatusTone
{
    /// <summary>Quiet empty or informational state.</summary>
    Neutral,

    /// <summary>Transient work or current informational state.</summary>
    Info,

    /// <summary>Usable retained state that needs operator attention.</summary>
    Warning,

    /// <summary>Blocking state with no usable current snapshot.</summary>
    Error,
}

/// <summary>Reusable accessible loading, empty, warning, or error disclosure.</summary>
public sealed class DisclosureStatusViewModel
{
    /// <summary>Creates one localized disclosure status.</summary>
    public DisclosureStatusViewModel(
        DisclosureStatusTone tone,
        string title,
        string detail,
        string? technicalDetail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        Tone = tone;
        Title = title;
        Detail = detail;
        TechnicalDetail = technicalDetail ?? string.Empty;
        AccessibleLabel = string.IsNullOrWhiteSpace(TechnicalDetail)
            ? $"{Title}. {Detail}"
            : $"{Title}. {Detail} {TechnicalDetail}";
    }

    /// <summary>Semantic visual tone.</summary>
    public DisclosureStatusTone Tone { get; }

    /// <summary>Short operator outcome.</summary>
    public string Title { get; }

    /// <summary>Impact and next-action detail.</summary>
    public string Detail { get; }

    /// <summary>Optional stable technical issue detail.</summary>
    public string TechnicalDetail { get; }

    /// <summary>Whether stable technical issue detail is present.</summary>
    public bool HasTechnicalDetail => !string.IsNullOrWhiteSpace(TechnicalDetail);

    /// <summary>Combined text announced by assistive technology.</summary>
    public string AccessibleLabel { get; }

    /// <summary>True for quiet empty state styling.</summary>
    public bool IsNeutral => Tone == DisclosureStatusTone.Neutral;

    /// <summary>True for transient informational styling.</summary>
    public bool IsInfo => Tone == DisclosureStatusTone.Info;

    /// <summary>True for retained warning styling.</summary>
    public bool IsWarning => Tone == DisclosureStatusTone.Warning;

    /// <summary>True for blocking error styling.</summary>
    public bool IsError => Tone == DisclosureStatusTone.Error;
}
