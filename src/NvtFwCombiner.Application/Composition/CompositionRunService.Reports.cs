using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static readonly string EmptySha256 = ToSha256Hex([]);

    private static CompositionRunReport CreateReport(
        CompositionRunRequest request,
        CompositionExecutionResult execution,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        bool committed,
        Dictionary<string, IReadOnlyList<ExternalProcessInvocation>>? executedCommandsByOperationId = null)
    {
        OperationRunSummary[] operations = [
            .. request.CompiledComposition.Plan.OrderedOperations.Select(operation =>
            {
                IReadOnlyList<ExternalProcessInvocation> executedCommands = executedCommandsByOperationId is not null &&
                    executedCommandsByOperationId.TryGetValue(
                        operation.OperationId,
                        out IReadOnlyList<ExternalProcessInvocation>? operationCommands)
                            ? operationCommands
                            : [];
                OperationRunStatus status = execution.Status == CompositionExecutionStatus.Succeeded
                    ? OperationRunStatus.Succeeded
                    : executedCommands.Count > 0
                        ? OperationRunStatus.Failed
                        : OperationRunStatus.Skipped;
                return ToOperationSummary(operation, status, executedCommands);
            }),
        ];

        byte[] outputBytes = execution.OutputBytes.ToArray();
        var output = new OutputArtifactSummary(
            request.OutputFileName,
            outputBytes.LongLength,
            outputBytes.Length == 0 ? EmptySha256 : ToSha256Hex(outputBytes),
            committed);

        MutationRunSummary[] mutations = [
            .. execution.Mutations.Select(ToMutationSummary),
        ];
        OutputDifferenceSummary[] outputDifferences = [
            .. CreateOutputDifferences(request, execution, inputBytes, outputBytes),
        ];
        CompositionIssue[] issues = [
            .. execution.Issues,
            .. CreateOutputDifferenceIssues(outputDifferences),
        ];

        return new CompositionRunReport(
            request.RunId,
            request.CompiledComposition.ProfileId,
            request.CompiledComposition.ProfileVersion,
            request.CompiledComposition.IcId,
            request.CompiledComposition.ModeId,
            request.CompiledComposition.ExperienceId,
            request.CompiledComposition.CompositionKind,
            startedAtUtc,
            completedAtUtc,
            inputSummaries,
            operations,
            mutations,
            issues,
            output,
            outputDifferences);
    }

    private static MutationRunSummary ToMutationSummary(MutationRecord mutation)
    {
        long changedByteCount = mutation.ChangedRanges.Sum(range => range.Length);
        return new MutationRunSummary(
            mutation.OperationId,
            mutation.OperationKind,
            mutation.TargetSpaceId,
            mutation.TargetRange,
            changedByteCount,
            mutation.BeforeSha256.ToLowerInvariant(),
            mutation.AfterSha256.ToLowerInvariant(),
            mutation.Reason);
    }

    private static OperationRunSummary ToOperationSummary(
        CompositionOperation operation,
        OperationRunStatus status,
        IReadOnlyList<ExternalProcessInvocation> executedCommands)
    {
        ExternalProcessorInvocation? invocation = operation.ExternalProcessorInvocation;
        return new OperationRunSummary(
            operation.OperationId,
            operation.Sequence,
            operation.Kind,
            status,
            operation.SourceSpaceId,
            operation.SourceRange,
            operation.TargetSpaceId,
            operation.TargetRange,
            operation.OverlapPolicy,
            invocation?.ProcessorId,
            invocation?.ToolBindingId,
            invocation?.AllowedReadRanges ?? [],
            invocation?.AllowedWriteRanges ?? [],
            operation.Reason,
            operation.Provenance,
            executedCommands);
    }
}
