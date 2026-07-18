using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Runs preview and build through the shared composition engine.</summary>
public sealed partial class CompositionRunService
{
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
        return await RunAsync(request, CompositionRunIntent.Preview, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Executes the request and commits output when execution succeeds.</summary>
    public async ValueTask<CompositionRunResult> BuildAsync(
        CompositionRunRequest request,
        CancellationToken cancellationToken)
    {
        _ = _outputWriter ?? throw new InvalidOperationException("Build requires an output writer.");

        return await RunAsync(request, CompositionRunIntent.ApprovedBuild, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Executes once and commits that same validated output without a separate preview pass.</summary>
    public async ValueTask<CompositionRunResult> AutomaticBuildAsync(
        CompositionRunRequest request,
        CancellationToken cancellationToken)
    {
        _ = _outputWriter ?? throw new InvalidOperationException("Build requires an output writer.");

        return await RunAsync(request, CompositionRunIntent.AutomaticBuild, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<CompositionRunResult> RunAsync(
        CompositionRunRequest request,
        CompositionRunIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool commitOutput = intent is CompositionRunIntent.ApprovedBuild or CompositionRunIntent.AutomaticBuild;
        DateTimeOffset startedAtUtc = _clock.UtcNow;
        if (intent == CompositionRunIntent.ApprovedBuild && request.ApprovedPreviewToken is null)
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
                committed: false,
                validations: [
                    .. CreateSkippedFinalOutputValidations(request.CompiledComposition)
                        .Select(static evaluation => evaluation.Summary),
                ]);
            return new CompositionRunResult(
                previewRequired.Status,
                [],
                failedReport,
                committedOutputId: null);
        }

        BoundInputs boundInputs = await ReadInputsAsync(request, cancellationToken).ConfigureAwait(false);
        var executedCommandsByOperationId = new Dictionary<string, IReadOnlyList<ExternalProcessInvocation>>(StringComparer.Ordinal);
        CompositionExecutionResult execution = boundInputs.Issues.Count == 0
            ? await ExecutePlanAsync(request, boundInputs, executedCommandsByOperationId, cancellationToken).ConfigureAwait(false)
            : CompositionExecutionResult.Failed(boundInputs.Issues);
        List<FinalOutputValidationEvaluation> finalOutputValidations =
            execution.Status == CompositionExecutionStatus.Succeeded
                ? EvaluateFinalOutput(request.CompiledComposition, execution.OutputBytes)
                : CreateSkippedFinalOutputValidations(request.CompiledComposition);
        List<CompositionIssue> runIssues = [
            .. finalOutputValidations
                .Where(static evaluation => evaluation.Issue is not null)
                .Select(static evaluation => evaluation.Issue!),
        ];
        bool finalOutputAccepted = !finalOutputValidations.Any(static evaluation => evaluation.BlocksPublication);
        CompositionExecutionStatus runStatus = execution.Status == CompositionExecutionStatus.Succeeded && finalOutputAccepted
            ? CompositionExecutionStatus.Succeeded
            : CompositionExecutionStatus.Failed;
        string? previewToken = runStatus == CompositionExecutionStatus.Succeeded
            ? CalculatePreviewToken(request, execution, boundInputs.InputSummaries)
            : null;

        string? committedOutputId = null;
        if (commitOutput && runStatus == CompositionExecutionStatus.Succeeded)
        {
            if (intent == CompositionRunIntent.ApprovedBuild &&
                !string.Equals(request.ApprovedPreviewToken, previewToken, StringComparison.Ordinal))
            {
                runIssues.Add(new CompositionIssue(
                    "build.preview-token.mismatch",
                    "Build request does not match the approved preview token."));
                runStatus = CompositionExecutionStatus.Failed;
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
            committedOutputId is not null,
            additionalIssues: runIssues,
            validations: [.. finalOutputValidations.Select(static evaluation => evaluation.Summary)],
            executedCommandsByOperationId: executedCommandsByOperationId);

        return new CompositionRunResult(
            runStatus,
            runStatus == CompositionExecutionStatus.Succeeded ? execution.OutputBytes.ToArray() : [],
            report,
            committedOutputId,
            intent == CompositionRunIntent.Preview ? previewToken : null);
    }

    private enum CompositionRunIntent
    {
        Preview,
        ApprovedBuild,
        AutomaticBuild,
    }
}
