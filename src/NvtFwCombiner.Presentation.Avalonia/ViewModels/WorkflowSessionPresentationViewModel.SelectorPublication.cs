using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    internal bool HasPublishedWorkflowAuthoringChoices(params string[] workflowIds)
    {
        ArgumentNullException.ThrowIfNull(workflowIds);
        return _selectorPublication is { } publication &&
            publication.IcIds.Any(icId => workflowIds.Any(workflowId =>
                publication.IsWorkflowAuthorable(icId, workflowId)));
    }

    internal IReadOnlyList<string> GetPublishedWorkflowIcChoices(string workflowId)
    {
        return _selectorPublication is { } publication
            ? GetPublishedWorkflowIcChoices(publication, workflowId)
            : [];
    }

    private static ReadOnlyCollection<string> GetPublishedWorkflowIcChoices(
        CapabilitySelectorPublication publication,
        string workflowId)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        return Array.AsReadOnly(
            publication.IcIds
                .Where(icId => publication.IsWorkflowAuthorable(icId, workflowId))
                .ToArray());
    }
}
