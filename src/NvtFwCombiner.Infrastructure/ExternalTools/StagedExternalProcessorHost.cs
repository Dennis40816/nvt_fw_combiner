// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Owns staged external execution and the sole before/after mutation audit for every processor kind.</summary>
public sealed partial class StagedExternalProcessorHost : IExternalProcessor
{
    private readonly ExternalCombinerToolResolver _toolResolver;
    private readonly string _stagingRoot;
    private readonly IExternalProcessRunner _processRunner;
    private readonly ExternalRuntimeDeployment? _runtimeDeployment;
    private readonly Dictionary<string, IStagedExternalProcessorAdapter> _adapters;

    /// <summary>Creates a host with the approved Legacy Combiner protocol adapter.</summary>
    public StagedExternalProcessorHost(
        ExternalCombinerToolRegistry registry,
        string toolRoot,
        string stagingRoot,
        IExternalProcessRunner processRunner,
        IEnumerable<ExternalCombinerInvocationProfile> invocationProfiles)
        : this(
            registry,
            toolRoot,
            stagingRoot,
            processRunner,
            [new LegacyCombinerProcessorAdapter(invocationProfiles)],
            null)
    {
    }

    internal StagedExternalProcessorHost(
        ExternalCombinerToolRegistry registry,
        string toolRoot,
        string stagingRoot,
        IExternalProcessRunner processRunner,
        IEnumerable<IStagedExternalProcessorAdapter> adapters,
        ExternalRuntimeDeployment? runtimeDeployment)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(adapters);

        _toolResolver = new ExternalCombinerToolResolver(registry, toolRoot);
        _stagingRoot = Path.GetFullPath(stagingRoot);
        _processRunner = processRunner;
        _runtimeDeployment = runtimeDeployment;
        _adapters = new Dictionary<string, IStagedExternalProcessorAdapter>(StringComparer.Ordinal);
        foreach (IStagedExternalProcessorAdapter adapter in adapters)
        {
            ArgumentNullException.ThrowIfNull(adapter);
            _adapters.Add(adapter.Kind, adapter);
        }
    }

    /// <inheritdoc />
    public ValueTask<ExternalProcessorResult> TransformAsync(
        ExternalProcessorRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TransformAsync(
            LegacyCombinerProcessorAdapter.ProcessorKind,
            request,
            request.AllowedWriteRanges,
            cancellationToken);
    }

    /// <summary>
    /// Runs one registered protocol kind. Declared write ranges are half-open offsets in this request's output
    /// image address space; they are supplied by the caller and are never inferred from protocol commands.
    /// </summary>
    internal async ValueTask<ExternalProcessorResult> TransformAsync(
        string processorKind,
        ExternalProcessorRequest request,
        IReadOnlyList<ByteRange> declaredWriteRanges,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processorKind);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(declaredWriteRanges);
        ByteRange[] writeRanges = [.. declaredWriteRanges];
        if (!_adapters.TryGetValue(processorKind, out IStagedExternalProcessorAdapter? adapter))
        {
            return Fail(
                "external-processor.protocol.unsupported",
                $"No external-processor adapter is registered for processor kind '{processorKind}'.");
        }

        CompositionIssue? requestIssue = adapter.ValidateRequest(request);
        if (requestIssue is not null)
        {
            return ExternalProcessorResult.Failed([requestIssue]);
        }

        if (!_toolResolver.TryResolve(
                request.ToolBindingId,
                out ExternalCombinerToolManifest? manifest,
                out string? executablePath,
                out CompositionIssue? toolIssue,
                cancellationToken))
        {
            return ExternalProcessorResult.Failed([toolIssue!]);
        }

        string runDirectory = Path.GetFullPath(Path.Combine(_stagingRoot, request.RunId));
        if (!ExternalCombinerToolResolver.IsInsideDirectory(_stagingRoot, runDirectory))
        {
            return Fail(
                "external-tool.staging.path-escape",
                "External processor staging directory escapes the approved staging root.");
        }

        CompositionIssue? pathIssue = adapter.ValidatePaths(request, runDirectory);
        return pathIssue is not null
            ? ExternalProcessorResult.Failed([pathIssue])
            : await PrepareAndExecuteAsync(
                adapter, request, manifest!, executablePath!, runDirectory, writeRanges, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ExternalProcessorResult> PrepareAndExecuteAsync(
        IStagedExternalProcessorAdapter adapter,
        ExternalProcessorRequest request,
        ExternalCombinerToolManifest resolvedManifest,
        string executablePath,
        string runDirectory,
        IReadOnlyList<ByteRange> declaredWriteRanges,
        CancellationToken cancellationToken)
    {
        using ExternalRuntimeDeploymentResult? deployment = _runtimeDeployment is null
            ? null
            : await _runtimeDeployment.PrepareAsync(executablePath, resolvedManifest.Sha256, cancellationToken).ConfigureAwait(false);
        if (deployment?.Issue is { } deploymentIssue)
        {
            return ExternalProcessorResult.Failed([deploymentIssue]);
        }

        if (!adapter.TryPrepare(request, resolvedManifest, out StagedExternalProcessorInvocation? invocation, out CompositionIssue? issue))
        {
            return ExternalProcessorResult.Failed([issue!]);
        }

        if (!HasConfinedLayout(runDirectory, invocation!))
        {
            return Fail(
                "external-tool.staging.path-escape",
                "External processor staging directory escapes the approved staging root.");
        }

        Session session = new(
            this,
            adapter,
            request,
            resolvedManifest,
            invocation!,
            runDirectory,
            deployment?.ExecutablePath ?? executablePath);
        return await ExecuteStagedAsync(session, declaredWriteRanges, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<ExternalProcessorResult> ExecuteStagedAsync(
        Session session,
        IReadOnlyList<ByteRange> declaredWriteRanges,
        CancellationToken cancellationToken)
    {
        try
        {
            using ExternalStagingDirectory? staging = ExternalStagingDirectory.TryAcquire(session.RunDirectory);
            if (staging is null)
            {
                return Fail("external-tool.staging.exists", "External processor staging directory already exists.");
            }

            foreach (string directory in session.Invocation.Directories)
            {
                _ = Directory.CreateDirectory(Path.Combine(session.RunDirectory, directory));
            }

            await File.WriteAllBytesAsync(session.InputPath, session.Request.InputBytes, cancellationToken).ConfigureAwait(false);
            CompositionIssue? executionIssue = await session.Adapter.ExecuteAsync(session, cancellationToken).ConfigureAwait(false);
            return executionIssue is not null
                ? ExternalProcessorResult.Failed([executionIssue], [.. session.ExecutedCommands])
                : await ImportOutputAsync(session, declaredWriteRanges, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ExternalProcessCleanupCapacityException exception)
        {
            return Fail(ExternalProcessCleanupText.CapacityIssueCode, ExternalProcessCleanupText.CapacityMessage(exception), session.ExceptionCommands);
        }
        catch (ExternalProcessStartFailedException exception)
        {
            return Fail(ExternalProcessCleanupText.StartFailedIssueCode, ExternalProcessCleanupText.StartFailedMessage(exception), session.ExceptionCommands);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Fail(
                "external-tool.staging.io-failed",
                $"External processor staging failed ({exception.GetType().Name}).",
                session.ExceptionCommands);
        }
    }

    private static async ValueTask<ExternalProcessorResult> ImportOutputAsync(
        Session session,
        IReadOnlyList<ByteRange> declaredWriteRanges,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(session.OutputPath))
        {
            return Fail("external-tool.output.missing", session.Adapter.DescribeMissingOutput(session.Request), session.ExecutedCommands);
        }

        byte[] outputBytes = await File.ReadAllBytesAsync(session.OutputPath, cancellationToken).ConfigureAwait(false);
        if (outputBytes.LongLength != session.Request.InputBytes.Length)
        {
            return Fail(
                "external-tool.output-length.changed",
                "External processor changed the firmware image length.",
                session.ExecutedCommands);
        }

        IReadOnlyList<ByteRange> changedRanges = ByteDiff.FindChangedRanges(session.Request.InputBytes.Span, outputBytes);
        ChangedRangeVerdict verdict = new ChangedRangePolicy(declaredWriteRanges).Evaluate(changedRanges);
        return verdict.IsAllowed
            ? ExternalProcessorResult.Success(outputBytes, changedRanges, [.. session.ExecutedCommands])
            : Fail(
                "external-tool.write-range.violation",
                session.Adapter.DescribeWriteViolation(session.Request, verdict),
                session.ExecutedCommands);
    }

    private static ExternalProcessorResult Fail(
        string code,
        string message,
        IReadOnlyList<ExternalProcessInvocation>? executedCommands = null)
    {
        return ExternalProcessorResult.Failed([new CompositionIssue(code, message)], [.. executedCommands ?? []]);
    }

    private static bool HasConfinedLayout(string runDirectory, StagedExternalProcessorInvocation invocation)
    {
        return invocation.Directories.Append(invocation.InputRelativePath).Append(invocation.OutputRelativePath)
            .All(path => !Path.IsPathRooted(path) &&
                ExternalCombinerToolResolver.IsInsideDirectory(runDirectory, Path.GetFullPath(Path.Combine(runDirectory, path))));
    }
}
