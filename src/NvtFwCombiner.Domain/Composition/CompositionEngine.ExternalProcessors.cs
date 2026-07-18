namespace NvtFwCombiner.Domain.Composition;

public static partial class CompositionEngine
{
    private static async ValueTask<CompositionExecutionResult?> ApplyExternalProcessorAsync(
        CompositionOperation operation,
        byte[] targetBuffer,
        Dictionary<string, byte[]> input,
        Dictionary<string, byte[]> mutableBuffers,
        CompositionExternalProcessor? externalProcessor,
        CancellationToken cancellationToken)
    {
        if (externalProcessor is null)
        {
            return CompositionExecutionResult.Failed([
                new CompositionIssue(
                    CompositionIssueCodes.ExecutionExternalProcessorUnavailable,
                    $"Operation '{operation.OperationId}' requires an external processor adapter.",
                    operation.OperationId),
            ]);
        }

        byte[] processorInput = ReadSlice(targetBuffer, operation.TargetRange);
        List<ExternalProcessorStagedSource> stagedSources = BuildStagedSources(operation, input);
        List<ExternalProcessorStagedArtifact> stagedArtifacts = BuildStagedArtifacts(operation, input, mutableBuffers);
        CompositionExternalProcessorResult processorResult;
        try
        {
            processorResult = await externalProcessor(operation, processorInput, stagedSources, stagedArtifacts, cancellationToken)
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
                    CompositionIssueCodes.ExecutionExternalProcessorFailed,
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
                    CompositionIssueCodes.ExecutionExternalProcessorLengthMismatch,
                    $"Operation '{operation.OperationId}' external processor changed the staged byte length.",
                    operation.OperationId),
            ]);
        }

        CompositionIssue? postconditionIssue = ValidateExternalProcessorOutputAssertions(operation, processorResult.OutputBytes);
        if (postconditionIssue is not null)
        {
            return CompositionExecutionResult.Failed([postconditionIssue]);
        }

        processorResult.OutputBytes.Span.CopyTo(targetBuffer.AsSpan(
            (int)operation.TargetRange.Start,
            (int)operation.TargetRange.Length));
        return null;
    }

    private static CompositionIssue? ValidateExternalProcessorOutputAssertions(
        CompositionOperation operation,
        ReadOnlyMemory<byte> outputBytes)
    {
        foreach (ExternalProcessorOutputAssertion assertion in operation.ExternalProcessorInvocation!.OutputAssertions)
        {
            ReadOnlySpan<byte> actual = outputBytes.Span.Slice(
                checked((int)assertion.Range.Start),
                checked((int)assertion.Range.Length));
            if (!actual.SequenceEqual(assertion.ExpectedBytes.Span))
            {
                return new CompositionIssue(
                    CompositionIssueCodes.ExecutionExternalProcessorPostconditionFailed,
                    $"Operation '{operation.OperationId}' external processor output did not satisfy the declared postcondition at {assertion.Range}.",
                    operation.OperationId);
            }
        }

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
            stagedSources.Add(ExternalProcessorStagedSource.FromOwnedBytes(binding.FirmwareRange, sourceBytes));
        }

        return stagedSources;
    }

    private static List<ExternalProcessorStagedArtifact> BuildStagedArtifacts(
        CompositionOperation operation,
        Dictionary<string, byte[]> input,
        Dictionary<string, byte[]> mutableBuffers)
    {
        ExternalProcessorInvocation invocation = operation.ExternalProcessorInvocation!;
        if (invocation.StagedArtifactBindings.Count == 0)
        {
            return [];
        }

        List<ExternalProcessorStagedArtifact> stagedArtifacts = [];
        foreach (ExternalProcessorStagedArtifactBinding binding in invocation.StagedArtifactBindings)
        {
            byte[] sourceBuffer = mutableBuffers.TryGetValue(binding.SourceSpaceId, out byte[]? mutableSource)
                ? mutableSource
                : input[binding.SourceSpaceId];
            stagedArtifacts.Add(ExternalProcessorStagedArtifact.FromOwnedBytes(
                binding.ArtifactId,
                ReadSlice(sourceBuffer, binding.SourceRange)));
        }

        return stagedArtifacts;
    }
}
