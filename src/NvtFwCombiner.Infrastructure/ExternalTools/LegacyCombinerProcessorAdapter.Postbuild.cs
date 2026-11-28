// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

internal sealed partial class LegacyCombinerProcessorAdapter
{
    private static async ValueTask<CompositionIssue?> ExecutePostbuildAsync(
        StagedExternalProcessorHost.Session session,
        CancellationToken cancellationToken)
    {
        ExternalProcessorRequest request = session.Request;
        ExternalProcessorProtocolPlan commandPlan = request.ProtocolPlan!;
        string outputDirectory = Path.Combine(session.RunDirectory, OutputDirectoryName);
        ReadOnlyMemory<byte> inputBytes = request.InputBytes;
        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, MapFileName), [], cancellationToken).ConfigureAwait(false);

        // Staged BIN files use replacement bytes without pre-writing the image given to Combiner.exe.
        byte[] stagedSourceBytes = inputBytes.ToArray();
        CompositionIssue? stagedSourceIssue = ApplyStagedSourceOverrides(stagedSourceBytes, request.StagedSources);
        if (stagedSourceIssue is not null)
        {
            return stagedSourceIssue;
        }

        CompositionIssue? stagedArtifactIssue = ValidateStagedArtifacts(commandPlan, request.StagedArtifacts);
        if (stagedArtifactIssue is not null)
        {
            return stagedArtifactIssue;
        }

        StagingTreePolicy stagingTreePolicy = CreateStagingTreePolicy(session.Manifest, commandPlan);
        foreach (ExternalProcessorProtocolCommand command in commandPlan.Commands)
        {
            CompositionIssue? commandIssue = await ExecutePostbuildCommandAsync(
                session, command, stagedSourceBytes, stagingTreePolicy, cancellationToken).ConfigureAwait(false);
            if (commandIssue is not null)
            {
                return commandIssue;
            }
        }

        // The last per-command check follows every protocol staging mutation; the host now audits the final image.
        return null;
    }

    private static async ValueTask<CompositionIssue?> ExecutePostbuildCommandAsync(
        StagedExternalProcessorHost.Session session,
        ExternalProcessorProtocolCommand command,
        byte[] stagedSourceBytes,
        StagingTreePolicy stagingTreePolicy,
        CancellationToken cancellationToken)
    {
        string firmwarePath = session.InputPath;
        string binDirectory = Path.Combine(session.RunDirectory, BinDirectoryName);
        long expectedLength = session.Request.InputBytes.Length;
        if (new FileInfo(firmwarePath).Length != expectedLength)
        {
            return new CompositionIssue(
                "external-tool.output-length.changed",
                "External processor changed the firmware image length.");
        }

        ResetDirectory(binDirectory);
        CompositionIssue? stagingIssue = MaterializeStagedBlockFiles(
            command.Blocks, stagedSourceBytes, session.Request.StagedArtifacts, binDirectory);
        if (stagingIssue is not null)
        {
            return stagingIssue;
        }

        ShortOutputTailSnapshot? shortOutputTail = await CaptureShortOutputTailAsync(
            firmwarePath, command, expectedLength, cancellationToken).ConfigureAwait(false);
        CompositionIssue? processIssue = await session.RunAsync(
            ResolveProtocolArguments(command, firmwarePath, binDirectory), command.CommandId, cancellationToken).ConfigureAwait(false);
        if (processIssue is not null)
        {
            return processIssue;
        }

        CompositionIssue? artifactMutationIssue = await VerifyStagedArtifactsUnchangedAsync(
            command.Blocks, session.Request.StagedArtifacts, binDirectory, cancellationToken).ConfigureAwait(false);
        if (artifactMutationIssue is not null)
        {
            return artifactMutationIssue;
        }

        CompositionIssue? lengthIssue = await NormalizeShortenedFirmwareAsync(
            firmwarePath, command, expectedLength, shortOutputTail, cancellationToken).ConfigureAwait(false);
        return lengthIssue ?? ValidateStagingTree(session.RunDirectory, stagingTreePolicy);
    }

    private static string[] ResolveProtocolArguments(
        ExternalProcessorProtocolCommand command,
        string firmwarePath,
        string binDirectory)
    {
        string stagedPrefix = ExternalProcessorProtocolArgumentTokens.StagedDirectory + Path.DirectorySeparatorChar;
        return [
            .. command.Arguments.Select(argument =>
                StringComparer.Ordinal.Equals(argument, ExternalProcessorProtocolArgumentTokens.TargetFile)
                    ? firmwarePath
                    : argument.StartsWith(stagedPrefix, StringComparison.Ordinal)
                        ? Path.Combine(binDirectory, argument[stagedPrefix.Length..])
                        : argument),
        ];
    }
}
