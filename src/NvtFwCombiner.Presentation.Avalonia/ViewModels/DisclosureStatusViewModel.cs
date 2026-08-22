namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal enum DisclosureStatusTone
{
    Neutral,

    Info,

    /// <summary>Usable retained state that needs operator attention.</summary>
    Warning,

    Error,
}

internal sealed class DisclosureStatusViewModel
{
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

    public DisclosureStatusTone Tone { get; }

    public string Title { get; }

    public string Detail { get; }

    public string TechnicalDetail { get; }

    public bool HasTechnicalDetail => !string.IsNullOrWhiteSpace(TechnicalDetail);

    public string AccessibleLabel { get; }

    public bool IsNeutral => Tone == DisclosureStatusTone.Neutral;

    public bool IsInfo => Tone == DisclosureStatusTone.Info;

    public bool IsWarning => Tone == DisclosureStatusTone.Warning;

    public bool IsError => Tone == DisclosureStatusTone.Error;
}
