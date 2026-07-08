using System.Security.Cryptography;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Runs preview and build through the shared composition engine.</summary>
public sealed partial class CompositionRunService
{
    private const int OutputDifferenceHexPreviewBytes = 32;
    private static readonly string EmptySha256 = ToSha256Hex([]);
    private const string DifferenceDeclaredReplacement = "DeclaredReplacement";
    private const string DifferencePostbuildCrcHeader = "PostbuildCrcHeader";
    private const string DifferencePreservedReference = "PreservedReference";
    private const string DifferenceUnexpected = "Unexpected";

    private readonly IArtifactReader _artifactReader;
    private readonly ISystemClock _clock;
    private readonly ICompositionOutputWriter? _outputWriter;
    private readonly IExternalProcessor? _externalProcessor;

    /// <summary>Creates a run service with read-only preview support.</summary>
    public CompositionRunService(IArtifactReader artifactReader, ISystemClock clock)
        : this(artifactReader, clock, null)
    {
    }

    /// <summary>Creates a run service with preview and build support.</summary>
    public CompositionRunService(
        IArtifactReader artifactReader,
        ISystemClock clock,
        ICompositionOutputWriter? outputWriter)
        : this(artifactReader, clock, outputWriter, null)
    {
    }

    /// <summary>Creates a run service with preview, build, and external processor support.</summary>
    public CompositionRunService(
        IArtifactReader artifactReader,
        ISystemClock clock,
        ICompositionOutputWriter? outputWriter,
        IExternalProcessor? externalProcessor)
    {
        ArgumentNullException.ThrowIfNull(artifactReader);
        ArgumentNullException.ThrowIfNull(clock);

        _artifactReader = artifactReader;
        _clock = clock;
        _outputWriter = outputWriter;
        _externalProcessor = externalProcessor;
    }

    /// <summary>Executes the request without committing output.</summary>
    public async ValueTask<CompositionRunResult> PreviewAsync(
        CompositionRunRequest request,
        CancellationToken cancellationToken)
    {
        return await RunAsync(request, commitOutput: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Executes the request and commits output when execution succeeds.</summary>
    public async ValueTask<CompositionRunResult> BuildAsync(
        CompositionRunRequest request,
        CancellationToken cancellationToken)
    {
        _ = _outputWriter ?? throw new InvalidOperationException("Build requires an output writer.");

        return await RunAsync(request, commitOutput: true, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<CompositionRunResult> RunAsync(
        CompositionRunRequest request,
        bool commitOutput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        DateTimeOffset startedAtUtc = _clock.UtcNow;
        if (commitOutput && request.ApprovedPreviewToken is null)
        {
            var previewRequired = CompositionExecutionResult.Failed([
                new CompositionIssue(
                    "build.preview-token.required",
                    "Build requires an approved preview token before output can be committed."),
            ]);
            DateTimeOffset failedAtUtc = _clock.UtcNow;
            CompositionRunReport failedReport = CreateReport(
                request,
                previewRequired,
                new Dictionary<string, byte[]>(StringComparer.Ordinal),
                [],
                startedAtUtc,
                failedAtUtc,
                committed: false);
            return new CompositionRunResult(
                previewRequired.Status,
                [],
                failedReport,
                committedOutputId: null);
        }

        BoundInputs boundInputs = await ReadInputsAsync(request, cancellationToken).ConfigureAwait(false);
        CompositionExecutionResult execution = boundInputs.Issues.Count == 0
            ? await ExecutePlanAsync(request, boundInputs, cancellationToken).ConfigureAwait(false)
            : CompositionExecutionResult.Failed(boundInputs.Issues);
        string? previewToken = execution.Status == CompositionExecutionStatus.Succeeded
            ? CalculatePreviewToken(request, execution, boundInputs.InputSummaries)
            : null;

        string? committedOutputId = null;
        if (commitOutput && execution.Status == CompositionExecutionStatus.Succeeded)
        {
            if (!string.Equals(request.ApprovedPreviewToken, previewToken, StringComparison.Ordinal))
            {
                execution = CompositionExecutionResult.Failed([
                    new CompositionIssue(
                        "build.preview-token.mismatch",
                        "Build request does not match the approved preview token."),
                ]);
            }
            else
            {
                committedOutputId = await _outputWriter!
                    .CommitAsync(request.OutputFileName, execution.OutputBytes, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        DateTimeOffset completedAtUtc = _clock.UtcNow;
        CompositionRunReport report = CreateReport(
            request,
            execution,
            boundInputs.InputBytes,
            boundInputs.InputSummaries,
            startedAtUtc,
            completedAtUtc,
            committedOutputId is not null);

        return new CompositionRunResult(
            execution.Status,
            execution.OutputBytes.ToArray(),
            report,
            committedOutputId,
            commitOutput ? null : previewToken);
    }

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

    private static CompositionRunReport CreateReport(
        CompositionRunRequest request,
        CompositionExecutionResult execution,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        bool committed)
    {
        OperationRunStatus status = execution.Status == CompositionExecutionStatus.Succeeded
            ? OperationRunStatus.Succeeded
            : OperationRunStatus.Skipped;
        OperationRunSummary[] operations = [
            .. request.Plan.OrderedOperations.Select(operation => ToOperationSummary(operation, status)),
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
            request.Profile.ProfileId,
            request.Profile.ProfileVersion,
            request.Profile.IcId,
            request.Profile.ModeId,
            request.Profile.ExperienceId,
            request.Profile.CompositionKind,
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
        OperationRunStatus status)
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
            operation.Provenance);
    }

    private static string ToSha256Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string ToSliceSha256Hex(byte[] bytes, ByteRange range)
    {
        return ToSha256Hex(bytes.AsSpan(checked((int)range.Start), checked((int)range.Length)));
    }

    private static string ToSliceHexPreview(byte[] bytes, ByteRange range)
    {
        int length = checked((int)Math.Min(range.Length, OutputDifferenceHexPreviewBytes));
        return ToHex(bytes.AsSpan(checked((int)range.Start), length));
    }
}
