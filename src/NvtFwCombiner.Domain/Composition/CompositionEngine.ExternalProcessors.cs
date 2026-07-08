namespace NvtFwCombiner.Domain.Composition;

public static partial class CompositionEngine
{
    private static async ValueTask<CompositionExecutionResult?> ApplyExternalProcessorAsync(
        CompositionOperation operation,
        byte[] targetBuffer,
        Dictionary<string, byte[]> input,
        CompositionExternalProcessor? externalProcessor,
        CancellationToken cancellationToken)
    {
        if (externalProcessor is null)
        {
            return CompositionExecutionResult.Failed([
                new CompositionIssue(
                    "execution.external-processor.unavailable",
                    $"Operation '{operation.OperationId}' requires an external processor adapter.",
                    operation.OperationId),
            ]);
        }

        byte[] processorInput = ReadSlice(targetBuffer, operation.TargetRange);
        List<ExternalProcessorStagedSource> stagedSources = BuildStagedSources(operation, input);
        CompositionExternalProcessorResult processorResult;
        try
        {
            processorResult = await externalProcessor(operation, processorInput, stagedSources, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return CompositionExecutionResult.Failed([
                new CompositionIssue(
                    "execution.external-processor.failed",
                    $"Operation '{operation.OperationId}' external processor failed ({exception.GetType().Name}).",
                    operation.OperationId),
            ]);
        }

        if (!processorResult.Succeeded)
        {
            return CompositionExecutionResult.Failed(processorResult.Issues);
        }

        if (processorResult.OutputBytes.Length != processorInput.Length)
        {
            return CompositionExecutionResult.Failed([
                new CompositionIssue(
                    "execution.external-processor.length-mismatch",
                    $"Operation '{operation.OperationId}' external processor changed the staged byte length.",
                    operation.OperationId),
            ]);
        }

        processorResult.OutputBytes.Span.CopyTo(targetBuffer.AsSpan(
            (int)operation.TargetRange.Start,
            (int)operation.TargetRange.Length));
        return null;
    }

    private static List<ExternalProcessorStagedSource> BuildStagedSources(
        CompositionOperation operation,
        Dictionary<string, byte[]> input)
    {
        ExternalProcessorInvocation invocation = operation.ExternalProcessorInvocation!;
        if (invocation.StagedSourceBindings.Count == 0)
        {
            return [];
        }

        List<ExternalProcessorStagedSource> stagedSources = [];
        foreach (ExternalProcessorStagedSourceBinding binding in invocation.StagedSourceBindings)
        {
            byte[] sourceBuffer = input[binding.SourceSpaceId];
            byte[] sourceBytes = ReadSlice(sourceBuffer, binding.SourceRange);
            stagedSources.Add(new ExternalProcessorStagedSource(binding.FirmwareRange, sourceBytes));
        }

        return stagedSources;
    }
}
