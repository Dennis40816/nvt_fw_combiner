namespace NvtFwCombiner.Domain.Composition;

/// <summary>Executes validated composition plans against immutable input bytes.</summary>
public static partial class CompositionEngine
{
    /// <summary>Executes <paramref name="plan"/> and returns output bytes, mutation trace, or structured issues.</summary>
    public static CompositionExecutionResult Execute(CompositionPlan plan, CompositionExecutionInput input)
    {
        return ExecuteAsync(plan, input, externalProcessor: null, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>Executes <paramref name="plan"/> and invokes an application-owned hook for external operations.</summary>
    public static async ValueTask<CompositionExecutionResult> ExecuteAsync(
        CompositionPlan plan,
        CompositionExecutionInput input,
        CompositionExternalProcessor? externalProcessor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(input);

        List<CompositionIssue> issues = ValidateExecutionInputs(plan, input);
        if (issues.Count > 0)
        {
            return CompositionExecutionResult.Failed(issues);
        }

        NormalizedExecutionInputs normalizedInputs = NormalizeExecutionInputs(plan, input);
        Dictionary<string, byte[]> mutableBuffers = InitializeMutableBuffers(plan, normalizedInputs.InputBytes);
        List<MutationRecord> mutations = [];

        foreach (CompositionOperation operation in plan.OrderedOperations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] targetBuffer = mutableBuffers[operation.TargetSpaceId];
            byte[] before = ReadSlice(targetBuffer, operation.TargetRange);

            if (operation.Kind == CompositionOperationKind.RunExternalProcessor)
            {
                CompositionExecutionResult? externalFailure = await ApplyExternalProcessorAsync(
                        operation,
                        targetBuffer,
                        normalizedInputs.InputBytes,
                        externalProcessor,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (externalFailure is not null)
                {
                    return PrependIssues(externalFailure, normalizedInputs.Issues);
                }
            }
            else
            {
                ApplyHostOperation(operation, normalizedInputs.InputBytes, mutableBuffers);
            }

            byte[] after = ReadSlice(targetBuffer, operation.TargetRange);
            mutations.Add(CreateMutationRecord(operation, before, after));
        }

        return CompositionExecutionResult.Succeeded(
            mutableBuffers[plan.Initialization.TargetSpaceId],
            mutations,
            normalizedInputs.Issues);
    }

}
