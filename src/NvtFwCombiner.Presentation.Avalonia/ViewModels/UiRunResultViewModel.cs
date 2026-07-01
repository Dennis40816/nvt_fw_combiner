namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Small UI summary for an application preview/build run.</summary>
public sealed class UiRunResultViewModel
{
    /// <summary>Creates a run result summary.</summary>
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

    /// <summary>Status headline.</summary>
    public string Title { get; }

    /// <summary>Status details.</summary>
    public string Detail { get; }

    /// <summary>Output path or output file name.</summary>
    public string Output { get; }

    /// <summary>True when the underlying application run succeeded.</summary>
    public bool Succeeded { get; }
}
