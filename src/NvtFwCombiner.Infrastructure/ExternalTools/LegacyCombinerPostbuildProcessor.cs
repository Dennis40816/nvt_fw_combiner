using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Runs IC-specific CtrlRAM postbuild command sequences through approved legacy Combiner.exe binaries.</summary>
public sealed partial class LegacyCombinerPostbuildProcessor : IExternalProcessor
{
    private const string BinDirectoryName = "BIN";
    private const string OutputDirectoryName = "output";
    private const string MapFileName = "map.txt";

    private readonly ExternalCombinerToolResolver _toolResolver;
    private readonly string _stagingRoot;
    private readonly IExternalProcessRunner _processRunner;

    /// <summary>Creates a staged postbuild processor with approved tool and IC command profiles.</summary>
    public LegacyCombinerPostbuildProcessor(
        ExternalCombinerToolRegistry registry,
        string toolRoot,
        string stagingRoot,
        IExternalProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);
        ArgumentNullException.ThrowIfNull(processRunner);

        _toolResolver = new ExternalCombinerToolResolver(registry, toolRoot);
        _stagingRoot = Path.GetFullPath(stagingRoot);
        _processRunner = processRunner;
    }

    /// <inheritdoc />
    public async ValueTask<ExternalProcessorResult> TransformAsync(
        ExternalProcessorRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ProtocolPlan is not { } commandPlan ||
            !StringComparer.Ordinal.Equals(
                commandPlan.ProtocolId,
                LegacyCombinerPostbuildPlanCompiler.ProtocolId))
        {
            return Fail(
                "legacy-combiner.compiled-plan.missing",
                "Legacy combiner execution requires the exact compiled protocol plan.");
        }

        if (!_toolResolver.TryResolve(
                request.ToolBindingId,
                out ExternalCombinerToolManifest? manifest,
                out string? executablePath,
                out CompositionIssue? toolIssue))
        {
            return ExternalProcessorResult.Failed([toolIssue!]);
        }

        ExternalCombinerToolManifest resolvedManifest = manifest!;

        string runDirectory = Path.GetFullPath(Path.Combine(_stagingRoot, request.RunId));
        if (!ExternalCombinerToolResolver.IsInsideDirectory(_stagingRoot, runDirectory))
        {
            return Fail(
                "external-tool.staging.path-escape",
                "External processor staging directory escapes the approved staging root.");
        }

        List<ExternalProcessInvocation> executedCommands = [];
        try
        {
            if (Directory.Exists(runDirectory))
            {
                return Fail("external-tool.staging.exists", "External processor staging directory already exists.");
            }

            string outputDirectory = Path.Combine(runDirectory, OutputDirectoryName);
            string binDirectory = Path.Combine(runDirectory, BinDirectoryName);
            _ = Directory.CreateDirectory(outputDirectory);
            _ = Directory.CreateDirectory(binDirectory);

            string firmwarePath = Path.Combine(outputDirectory, commandPlan.TargetFileName);
            ReadOnlyMemory<byte> inputBytes = request.InputBytes;
            await File.WriteAllBytesAsync(firmwarePath, inputBytes, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(Path.Combine(outputDirectory, MapFileName), [], cancellationToken)
                .ConfigureAwait(false);

            // Staged BIN files use selected replacement bytes as source material without
            // pre-writing those bytes into the firmware image given to Combiner.exe.
            byte[] stagedSourceBytes = inputBytes.ToArray();
            CompositionIssue? stagedSourceIssue = ApplyStagedSourceOverrides(stagedSourceBytes, request.StagedSources);
            if (stagedSourceIssue is not null)
            {
                return ExternalProcessorResult.Failed([stagedSourceIssue]);
            }

            CompositionIssue? stagedArtifactIssue = ValidateStagedArtifacts(commandPlan, request.StagedArtifacts);
            if (stagedArtifactIssue is not null)
            {
                return ExternalProcessorResult.Failed([stagedArtifactIssue]);
            }

            StagingTreePolicy stagingTreePolicy = CreateStagingTreePolicy(resolvedManifest, commandPlan);
            foreach (ExternalProcessorProtocolCommand command in commandPlan.Commands)
            {
                if (new FileInfo(firmwarePath).Length != inputBytes.Length)
                {
                    return Fail(
                        "external-tool.output-length.changed",
                        "External processor changed the firmware image length.",
                        executedCommands);
                }

                ResetDirectory(binDirectory);
                CompositionIssue? stagingIssue = MaterializeStagedBlockFiles(
                    command.Blocks,
                    stagedSourceBytes,
                    request.StagedArtifacts,
                    binDirectory);
                if (stagingIssue is not null)
                {
                    return ExternalProcessorResult.Failed([stagingIssue], executedCommands);
                }

                var startInfo = new ExternalProcessStartInfo(
                    executablePath!,
                    runDirectory,
                    ResolveProtocolArguments(command, firmwarePath, binDirectory),
                    TimeSpan.FromSeconds(resolvedManifest.TimeoutSeconds));
                ShortOutputTailSnapshot? shortOutputTail = await CaptureShortOutputTailAsync(
                        firmwarePath,
                        command,
                        inputBytes.Length,
                        cancellationToken)
                    .ConfigureAwait(false);
                ExternalProcessResult processResult = await _processRunner.RunAsync(startInfo, cancellationToken).ConfigureAwait(false);
                executedCommands.Add(startInfo.ToExecutedCommand());
                if (processResult.TimedOut)
                {
                    return Fail(
                        "external-tool.process.timeout",
                        $"External processor command '{command.CommandId}' timed out.",
                        executedCommands);
                }

                if (processResult.ExitCode != 0)
                {
                    return Fail(
                        "external-tool.process.failed",
                        $"External processor command '{command.CommandId}' exited with code {processResult.ExitCode}. {FormatProcessOutput(processResult)}",
                        executedCommands);
                }

                CompositionIssue? artifactMutationIssue = await VerifyStagedArtifactsUnchangedAsync(
                        command.Blocks,
                        request.StagedArtifacts,
                        binDirectory,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (artifactMutationIssue is not null)
                {
                    return ExternalProcessorResult.Failed([artifactMutationIssue], executedCommands);
                }

                CompositionIssue? lengthIssue = await NormalizeShortenedFirmwareAsync(
                        firmwarePath,
                        command,
                        inputBytes.Length,
                        shortOutputTail,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (lengthIssue is not null)
                {
                    return ExternalProcessorResult.Failed([lengthIssue], executedCommands);
                }

                CompositionIssue? perCommandUnexpectedFileIssue = ValidateStagingTree(runDirectory, stagingTreePolicy);
                if (perCommandUnexpectedFileIssue is not null)
                {
                    return ExternalProcessorResult.Failed([perCommandUnexpectedFileIssue], executedCommands);
                }
            }

            // Plans are nonempty, and the last per-command check follows every staging mutation.
            if (!File.Exists(firmwarePath))
            {
                return Fail(
                    "external-tool.output.missing",
                    "External processor did not leave the staged firmware file.",
                    executedCommands);
            }

            byte[] outputBytes = await File.ReadAllBytesAsync(firmwarePath, cancellationToken).ConfigureAwait(false);
            return outputBytes.LongLength != inputBytes.Length
                ? Fail(
                    "external-tool.output-length.changed",
                    "External processor changed the firmware image length.",
                    executedCommands)
                : CreateCheckedSuccess(inputBytes, outputBytes, request.AllowedWriteRanges, executedCommands);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Fail(
                "external-tool.staging.io-failed",
                $"External processor staging failed ({exception.GetType().Name}).",
                executedCommands);
        }
        finally
        {
            ExternalStagingDirectory.TryDelete(runDirectory);
        }
    }

    private static IReadOnlyList<string> ResolveProtocolArguments(
        ExternalProcessorProtocolCommand command,
        string firmwarePath,
        string binDirectory)
    {
        string stagedPrefix = ExternalProcessorProtocolArgumentTokens.StagedDirectory +
            Path.DirectorySeparatorChar;
        return [
            .. command.Arguments.Select(argument =>
                StringComparer.Ordinal.Equals(
                    argument,
                    ExternalProcessorProtocolArgumentTokens.TargetFile)
                    ? firmwarePath
                    : argument.StartsWith(stagedPrefix, StringComparison.Ordinal)
                        ? Path.Combine(binDirectory, argument[stagedPrefix.Length..])
                        : argument),
        ];
    }

}
