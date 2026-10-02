namespace NvtFwCombiner.Bootstrap.Tests;

internal sealed class CliChoiceRecordingExecution(ICompositionExecution inner) : ICompositionExecution
{
    internal List<AcceptedCompositionExecutionRequest> Requests { get; } = [];

    public async ValueTask<CompositionRunResult> ExecuteAsync(AcceptedCompositionExecutionRequest request,
        CompositionRunProgressFeed progress, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return await inner.ExecuteAsync(request, progress, cancellationToken);
    }
}
