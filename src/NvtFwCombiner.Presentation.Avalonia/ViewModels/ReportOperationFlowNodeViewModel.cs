namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One human-readable operation flow node in a report.</summary>
public sealed class ReportOperationFlowNodeViewModel(
    string number,
    string title,
    string detail,
    string meta,
    string status,
    bool hasConnector = false)
{
    /// <summary>Displayed sequence number.</summary>
    public string Number { get; } = number;

    /// <summary>Node title.</summary>
    public string Title { get; } = title;

    /// <summary>Node detail.</summary>
    public string Detail { get; } = detail;

    /// <summary>Small node metadata.</summary>
    public string Meta { get; } = meta;

    /// <summary>True when metadata has content.</summary>
    public bool HasMeta => !string.IsNullOrWhiteSpace(Meta);

    /// <summary>Node status.</summary>
    public string Status { get; } = status;

    /// <summary>True when this node should draw a connector to the next node.</summary>
    public bool HasConnector { get; } = hasConnector;
}
