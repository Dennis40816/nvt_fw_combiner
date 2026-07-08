using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private async ValueTask<CompositionExecutionResult> ExecutePlanAsync(
        CompositionRunRequest request,
        BoundInputs boundInputs,
        CancellationToken cancellationToken)
    {
        var input = new CompositionExecutionInput(boundInputs.InputBytes);
        return _externalProcessor is null
            ? CompositionEngine.Execute(request.Plan, input)
            : await CompositionEngine.ExecuteAsync(
                request.Plan,
                input,
                (operation, inputBytes, stagedSources, token) =>
                    TransformExternalProcessorAsync(request, operation, inputBytes, stagedSources, token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<CompositionExternalProcessorResult> TransformExternalProcessorAsync(
        CompositionRunRequest request,
        CompositionOperation operation,
        ReadOnlyMemory<byte> inputBytes,
        IReadOnlyList<ExternalProcessorStagedSource> stagedSources,
        CancellationToken cancellationToken)
    {
        ExternalProcessorInvocation invocation = operation.ExternalProcessorInvocation!;
        ExternalProcessorRequest processorRequest;
        try
        {
            processorRequest = new ExternalProcessorRequest(
                $"{request.RunId}.{operation.OperationId}",
                invocation.ProcessorId,
                invocation.ToolBindingId,
                inputBytes,
                invocation.AllowedWriteRanges,
                request.IcNumberSelection,
                stagedSources);
        }
        catch (ArgumentException exception)
        {
            return CompositionExternalProcessorResult.Failed([
                new CompositionIssue(
                    "external-processor.request.invalid",
                    exception.Message,
                    operation.OperationId),
            ]);
        }

        ExternalProcessorResult processorResult = await _externalProcessor!
            .TransformAsync(processorRequest, cancellationToken)
            .ConfigureAwait(false);
        return processorResult.Succeeded
            ? CompositionExternalProcessorResult.Success(processorResult.OutputBytes)
            : CompositionExternalProcessorResult.Failed(processorResult.Issues);
    }
}
