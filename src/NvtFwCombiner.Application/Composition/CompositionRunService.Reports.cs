using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static readonly string EmptySha256 = ToSha256Hex([]);

    private static CompositionRunReport CreateReport(
        CompositionRunRequest request,
        CompositionExecutionResult execution,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        IReadOnlyList<OutputDifferenceSummary> outputDifferences,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        bool committed,
        string? outputFileName = null,
        OutputNamingSummary? outputNaming = null,
        IReadOnlyList<DeliveryArtifactSummary>? deliveryArtifacts = null,
        IReadOnlyList<CompositionIssue>? additionalIssues = null,
        IReadOnlyList<ValidationRunSummary>? validations = null,
        Dictionary<string, IReadOnlyList<ExternalProcessInvocation>>? executedCommandsByOperationId = null,
        CompositionOutputBundleDeliverySummary? bundleDelivery = null)
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

        ReadOnlyMemory<byte> outputBytes = execution.OutputBytes;
        var output = new OutputArtifactSummary(
            outputFileName ?? request.OutputFileName,
            outputBytes.Length,
            outputBytes.Length == 0 ? EmptySha256 : ToSha256Hex(outputBytes.Span),
            committed);

        MutationRunSummary[] mutations = [
            .. execution.Mutations.Select(ToMutationSummary),
        ];
        CompositionIssue[] issues = [
            .. execution.Issues,
            .. additionalIssues ?? [],
        ];

        return new CompositionRunReport(
            request.RunId,
            request.CompiledComposition.V2Details.ProfileId,
            request.CompiledComposition.V2Details.ProfileVersion,
            request.CompiledComposition.V2Details.Provenance.Context.MemberId,
            request.CompiledComposition.V2Details.Provenance.Context.ModeId,
            request.CompiledComposition.V2Details.ExperienceId,
            request.CompiledComposition.V2Details.CompositionKind,
            startedAtUtc,
            completedAtUtc,
            inputSummaries,
            operations,
            mutations,
            issues,
            output,
            outputDifferences,
            request.CompiledComposition.CompilationFingerprint,
            validations,
            outputNaming,
            deliveryArtifacts,
            generalAdmission: request.GeneralAdmission,
            imageInitialization: StringComparer.Ordinal.Equals(
                request.CompiledComposition.V2Details.ExperienceId,
                ExperienceIds.GeneralMerge)
                    ? ImageInitializationSummary.FromCompiled(
                        request.CompiledComposition.Plan.OutputInitialization)
                    : null,
            bundleDelivery: bundleDelivery);
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

    internal static OperationRunSummary ToPlanningOperationSummary(
        CompositionOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return ToOperationSummary(operation, OperationRunStatus.Skipped, []);
    }
}
