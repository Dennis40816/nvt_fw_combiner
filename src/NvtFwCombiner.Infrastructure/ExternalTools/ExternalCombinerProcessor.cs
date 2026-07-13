using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Runs approved legacy combiner transforms inside a private staging directory.</summary>
public sealed partial class ExternalCombinerProcessor : IExternalProcessor
{
    private const string WorkFileName = "work.bin";
    private const string OutputFileName = "output.bin";

    private readonly ExternalCombinerToolRegistry _registry;
    private readonly string _toolRoot;
    private readonly string _stagingRoot;
    private readonly IExternalProcessRunner _processRunner;

    /// <summary>Creates a staged external combiner processor.</summary>
    public ExternalCombinerProcessor(
        ExternalCombinerToolRegistry registry,
        string toolRoot,
        string stagingRoot,
        IExternalProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);
        ArgumentNullException.ThrowIfNull(processRunner);

        _registry = registry;
        _toolRoot = Path.GetFullPath(toolRoot);
        _stagingRoot = Path.GetFullPath(stagingRoot);
        _processRunner = processRunner;
    }

    /// <inheritdoc />
    public async ValueTask<ExternalProcessorResult> TransformAsync(
        ExternalProcessorRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryResolveManifest(request, out ExternalCombinerToolManifest? manifest, out CompositionIssue? manifestIssue))
        {
            return ExternalProcessorResult.Failed([manifestIssue!]);
        }

        if (!TryResolveExecutable(manifest!, out string? executablePath, out CompositionIssue? executableIssue))
        {
            return ExternalProcessorResult.Failed([executableIssue!]);
        }

        string runDirectory = Path.Combine(_stagingRoot, request.RunId);
        try
        {
            if (Directory.Exists(runDirectory))
            {
                return Fail("external-tool.staging.exists", "External processor staging directory already exists.");
            }

            _ = Directory.CreateDirectory(runDirectory);
            string workBin = Path.Combine(runDirectory, WorkFileName);
            string outputBin = Path.Combine(runDirectory, OutputFileName);
            await File.WriteAllBytesAsync(workBin, request.InputBytes.ToArray(), cancellationToken).ConfigureAwait(false);
            IReadOnlyDictionary<string, string> stagedArtifactPaths = await MaterializeStagedArtifactsAsync(
                request,
                runDirectory,
                cancellationToken).ConfigureAwait(false);

            if (!TryExpandArguments(
                    manifest!.ArgumentTemplate,
                    workBin,
                    outputBin,
                    runDirectory,
                    stagedArtifactPaths,
                    request.StagedArtifacts,
                    out IReadOnlyList<string>? arguments,
                    out CompositionIssue? argumentIssue))
            {
                return ExternalProcessorResult.Failed([argumentIssue!]);
            }
            var startInfo = new ExternalProcessStartInfo(
                executablePath!,
                runDirectory,
                arguments!,
                TimeSpan.FromSeconds(manifest.TimeoutSeconds));
            ExternalProcessResult processResult = await _processRunner.RunAsync(startInfo, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ExternalProcessInvocation> executedCommands = [startInfo.ToExecutedCommand()];
            if (processResult.TimedOut)
            {
                return Fail("external-tool.process.timeout", "External processor timed out.", executedCommands);
            }

            if (processResult.ExitCode != 0)
            {
                return Fail(
                    "external-tool.process.failed",
                    $"External processor exited with code {processResult.ExitCode}.",
                    executedCommands);
            }

            CompositionIssue? unexpectedFileIssue = FindUnexpectedStagingFileIssue(
                runDirectory,
                manifest,
                stagedArtifactPaths.Values);
            if (unexpectedFileIssue is not null)
            {
                return ExternalProcessorResult.Failed([unexpectedFileIssue], executedCommands);
            }

            CompositionIssue? artifactMutationIssue = await VerifyStagedArtifactsUnchangedAsync(
                request.StagedArtifacts,
                stagedArtifactPaths,
                cancellationToken).ConfigureAwait(false);
            if (artifactMutationIssue is not null)
            {
                return ExternalProcessorResult.Failed([artifactMutationIssue], executedCommands);
            }

            string transformedPath = string.Equals(manifest.InputMode, "input-output-file", StringComparison.Ordinal)
                ? outputBin
                : workBin;
            if (!File.Exists(transformedPath))
            {
                return Fail(
                    "external-tool.output.missing",
                    "External processor did not produce the expected output file.",
                    executedCommands);
            }

            byte[] outputBytes = await File.ReadAllBytesAsync(transformedPath, cancellationToken).ConfigureAwait(false);
            if (outputBytes.LongLength != request.InputBytes.Length)
            {
                return Fail(
                    "external-tool.output-length.changed",
                    "External processor changed the firmware image length.",
                    executedCommands);
            }

            IReadOnlyList<ByteRange> changedRanges = ByteDiff.FindChangedRanges(request.InputBytes.Span, outputBytes);
            ChangedRangeVerdict verdict = new ChangedRangePolicy(request.AllowedWriteRanges).Evaluate(changedRanges);
            return verdict.IsAllowed
                ? ExternalProcessorResult.Success(outputBytes, changedRanges, executedCommands)
                : ExternalProcessorResult.Failed([
                    new CompositionIssue(
                        "external-tool.write-range.violation",
                        "External processor changed bytes outside declared write ranges."),
                ], executedCommands);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Fail(
                "external-tool.staging.io-failed",
                $"External processor staging failed ({exception.GetType().Name}).");
        }
        finally
        {
            TryDeleteDirectory(runDirectory);
        }
    }

    private static ExternalProcessorResult Fail(
        string code,
        string message,
        IReadOnlyList<ExternalProcessInvocation>? executedCommands = null)
    {
        return ExternalProcessorResult.Failed([new CompositionIssue(code, message)], executedCommands ?? []);
    }
}
