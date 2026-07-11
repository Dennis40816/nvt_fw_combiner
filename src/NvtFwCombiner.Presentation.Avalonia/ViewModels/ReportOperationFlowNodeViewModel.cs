namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One human-readable operation flow node in a report.</summary>
public sealed class ReportOperationFlowNodeViewModel
{
    /// <summary>Creates a flow node.</summary>
    public ReportOperationFlowNodeViewModel(
        string number,
        string title,
        string detail,
        string meta,
        string status,
        bool hasConnector = false)
    {
        Number = number;
        Title = title;
        Detail = detail;
        Meta = meta;
        Status = status;
        HasConnector = hasConnector;
    }

    /// <summary>Displayed sequence number.</summary>
    public string Number { get; }

    /// <summary>Node title.</summary>
    public string Title { get; }

    /// <summary>Node detail.</summary>
    public string Detail { get; }

    /// <summary>Small node metadata.</summary>
    public string Meta { get; }

    /// <summary>True when metadata has content.</summary>
    public bool HasMeta => !string.IsNullOrWhiteSpace(Meta);

    /// <summary>Node status.</summary>
    public string Status { get; }

    /// <summary>True when this node should draw a connector to the next node.</summary>
    public bool HasConnector { get; }
}
