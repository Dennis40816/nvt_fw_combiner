namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed class ReportOperationFlowNodeViewModel(
    string number,
    string title,
    string detail,
    string meta,
    string status,
    bool hasConnector = false)
{
    public string Number { get; } = number;

    public string Title { get; } = title;

    public string Detail { get; } = detail;

    public string Meta { get; } = meta;

    public bool HasMeta => !string.IsNullOrWhiteSpace(Meta);

    public string Status { get; } = status;

    public bool HasConnector { get; } = hasConnector;
}
