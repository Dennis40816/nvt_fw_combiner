namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Small UI summary for an application preview/build run.</summary>
internal sealed class UiRunResultViewModel
{
    public UiRunResultViewModel(string title, string detail, string output, bool succeeded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(output);

        Title = title;
        Detail = detail;
        Output = output;
        Succeeded = succeeded;
    }

    public string Title { get; }

    public string Detail { get; }

    public string Output { get; }

    public bool Succeeded { get; }
}
