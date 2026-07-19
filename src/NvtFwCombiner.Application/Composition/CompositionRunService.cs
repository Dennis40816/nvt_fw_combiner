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
    public ValueTask<CompositionRunResult> PreviewAsync(
        CompositionRunRequest request,
        CancellationToken cancellationToken)
    {
        return RunAsync(
            request,
            commitOutput: false,
            requireApprovedPreviewToken: false,
            progress: null,
            cancellationToken);
    }

    /// <summary>Executes Preview and publishes bounded typed lifecycle phases.</summary>
    public ValueTask<CompositionRunResult> PreviewAsync(
        CompositionRunRequest request,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return RunWithProgressAsync(
            request,
            commitOutput: false,
            requireApprovedPreviewToken: false,
            progress,
            cancellationToken);
    }

    /// <summary>Executes the request and commits output when execution succeeds.</summary>
    public ValueTask<CompositionRunResult> BuildAsync(
        CompositionRunRequest request,
        CancellationToken cancellationToken)
    {
        _ = _outputWriter ?? throw new InvalidOperationException("Build requires an output writer.");

        return RunAsync(
            request,
            commitOutput: true,
            requireApprovedPreviewToken: true,
            progress: null,
            cancellationToken);
    }

    /// <summary>Executes approved-token Build and publishes bounded typed lifecycle phases.</summary>
    public ValueTask<CompositionRunResult> BuildAsync(
        CompositionRunRequest request,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        _ = _outputWriter ?? throw new InvalidOperationException("Build requires an output writer.");
        return RunWithProgressAsync(
            request,
            commitOutput: true,
            requireApprovedPreviewToken: true,
            progress,
            cancellationToken);
    }

    /// <summary>Executes once and commits that same validated output when automatic Build is requested.</summary>
    public ValueTask<CompositionRunResult> PreviewOrBuildAsync(
        CompositionRunRequest request,
        bool build,
        CancellationToken cancellationToken)
    {
        if (build)
        {
            _ = _outputWriter ?? throw new InvalidOperationException("Build requires an output writer.");
        }

        return RunAsync(
            request,
            commitOutput: build,
            requireApprovedPreviewToken: false,
            progress: null,
            cancellationToken);
    }

    /// <summary>Executes once, optionally commits, and publishes bounded typed lifecycle phases.</summary>
    public ValueTask<CompositionRunResult> PreviewOrBuildAsync(
        CompositionRunRequest request,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (build)
        {
            _ = _outputWriter ?? throw new InvalidOperationException("Build requires an output writer.");
        }

        return RunWithProgressAsync(
            request,
            commitOutput: build,
            requireApprovedPreviewToken: false,
            progress,
            cancellationToken);
    }

    private async ValueTask<CompositionRunResult> RunWithProgressAsync(
        CompositionRunRequest request,
        bool commitOutput,
        bool requireApprovedPreviewToken,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        progress.Start(request.RunId);
        try
        {
            return await RunAsync(
                    request,
                    commitOutput,
                    requireApprovedPreviewToken,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            progress.Complete();
        }
    }

    private async ValueTask<CompositionRunResult> RunAsync(
        CompositionRunRequest request,
        bool commitOutput,
        bool requireApprovedPreviewToken,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        DateTimeOffset startedAtUtc = _clock.UtcNow;
        var progressPublisher = new CompositionRunProgressPublisher(request, commitOutput, progress);
        progressPublisher.Report(CompositionRunPhase.Preparing);
        if (requireApprovedPreviewToken && request.ApprovedPreviewToken is null)
        {
            var previewRequired = CompositionExecutionResult.Failed([
                new CompositionIssue(
                    "build.preview-token.required",
                    "Build requires an approved preview token before output can be committed."),
            ]);
            DateTimeOffset failedAtUtc = _clock.UtcNow;
            progressPublisher.Report(CompositionRunPhase.PreparingReport);
            CompositionRunReport failedReport = CreateReport(
                request,
                previewRequired,
                [],
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

        progressPublisher.Report(CompositionRunPhase.ReadingInputs);
        BoundInputs boundInputs = await ReadInputsAsync(request, cancellationToken).ConfigureAwait(false);
        var executedCommandsByOperationId = new Dictionary<string, IReadOnlyList<ExternalProcessInvocation>>(StringComparer.Ordinal);
        CompositionExecutionResult execution;
        if (boundInputs.Issues.Count == 0)
        {
            progressPublisher.Report(CompositionRunPhase.ExecutingComposition);
            execution = await ExecutePlanAsync(
                    request,
                    boundInputs,
                    executedCommandsByOperationId,
                    progressPublisher,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            execution = CompositionExecutionResult.Failed(boundInputs.Issues);
        }

        List<FinalOutputValidationEvaluation> finalOutputValidations;
        if (execution.Status == CompositionExecutionStatus.Succeeded)
        {
            progressPublisher.Report(CompositionRunPhase.ValidatingOutput);
            finalOutputValidations = EvaluateFinalOutput(request.CompiledComposition, execution.OutputBytes);
        }
        else
        {
            finalOutputValidations = CreateSkippedFinalOutputValidations(request.CompiledComposition);
        }
        OutputDifferenceSummary[] outputDifferences = [
            .. CreateOutputDifferences(request, execution, boundInputs.InputBytes, execution.OutputBytes),
        ];
        List<CompositionIssue> runIssues = [
            .. finalOutputValidations
                .Where(static evaluation => evaluation.Issue is not null)
                .Select(static evaluation => evaluation.Issue!),
            .. CreateOutputDifferenceIssues(outputDifferences),
        ];
        bool finalOutputAccepted = !finalOutputValidations.Any(static evaluation => evaluation.BlocksPublication);
        bool outputDifferencesAccepted = outputDifferences.All(static difference => difference.IsAccepted);
        CompositionExecutionStatus runStatus = execution.Status == CompositionExecutionStatus.Succeeded &&
            finalOutputAccepted && outputDifferencesAccepted
            ? CompositionExecutionStatus.Succeeded
            : CompositionExecutionStatus.Failed;
        string? previewToken = runStatus == CompositionExecutionStatus.Succeeded
            ? CalculatePreviewToken(request, execution, boundInputs.InputSummaries)
            : null;

        string? committedOutputId = null;
        if (commitOutput && runStatus == CompositionExecutionStatus.Succeeded)
        {
            if (requireApprovedPreviewToken &&
                !string.Equals(request.ApprovedPreviewToken, previewToken, StringComparison.Ordinal))
            {
                runIssues.Add(new CompositionIssue(
                    "build.preview-token.mismatch",
                    "Build request does not match the approved preview token."));
                runStatus = CompositionExecutionStatus.Failed;
            }
            else
            {
                progressPublisher.Report(CompositionRunPhase.CommittingOutput);
                committedOutputId = await _outputWriter!
                    .CommitAsync(request.OutputFileName, execution.OutputBytes, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        DateTimeOffset completedAtUtc = _clock.UtcNow;
        progressPublisher.Report(CompositionRunPhase.PreparingReport, committedOutputId);
        CompositionRunReport report = CreateReport(
            request,
            execution,
            boundInputs.InputSummaries,
            outputDifferences,
            startedAtUtc,
            completedAtUtc,
            committedOutputId is not null,
            additionalIssues: runIssues,
            validations: [.. finalOutputValidations.Select(static evaluation => evaluation.Summary)],
            executedCommandsByOperationId: executedCommandsByOperationId);

        (string? inspectionOutputSpaceId, string? inspectionReferenceSpaceId, byte[]? inspectionReferenceBytes) = GetInspectionReference(
            request,
            runStatus,
            boundInputs.InputBytes,
            execution.OutputBytes.Length);
        return new CompositionRunResult(
            runStatus,
            runStatus == CompositionExecutionStatus.Succeeded ? execution.OutputBytes : ReadOnlyMemory<byte>.Empty,
            report,
            committedOutputId,
            commitOutput ? null : previewToken,
            inspectionOutputSpaceId,
            inspectionReferenceSpaceId,
            inspectionReferenceBytes);
    }
}
