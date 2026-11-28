// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

public sealed partial class StagedExternalProcessorHost
{
    private async ValueTask<CompositionIssue?> RunProcessAsync(
        Session session,
        IReadOnlyList<string> arguments,
        string? commandId,
        CancellationToken cancellationToken)
    {
        ExternalProcessStartInfo startInfo = new(
            session.ExecutablePath,
            session.RunDirectory,
            arguments,
            TimeSpan.FromSeconds(session.Manifest.TimeoutSeconds));
        ExternalProcessResult result = await _processRunner.RunAsync(startInfo, cancellationToken).ConfigureAwait(false);
        session.ExecutedCommands.Add(startInfo.ToExecutedCommand());
        string subject = commandId is null ? "External processor" : $"External processor command '{commandId}'";
        if (result.TimedOut)
        {
            return new CompositionIssue(
                "external-tool.process.timeout",
                subject + " timed out." + ExternalProcessCleanupText.Suffix(result));
        }

        if (result.ExitCode != 0)
        {
            return new CompositionIssue(
                "external-tool.process.failed",
                session.Adapter.DescribeProcessFailure(commandId, result) + ExternalProcessCleanupText.Suffix(result));
        }

        // No adapter may read or normalize staged results while cleanup is incomplete.
        return result.Cleanup == ExternalProcessCleanup.Complete
            ? null
            : new CompositionIssue(
                ExternalProcessCleanupText.IssueCode,
                subject + " exited, but " + ExternalProcessCleanupText.Describe(result.Cleanup));
    }

    /// <summary>One sequential invocation's host-owned staging and audit custody; never shared between runs.</summary>
    internal sealed class Session(
        StagedExternalProcessorHost host,
        IStagedExternalProcessorAdapter adapter,
        ExternalProcessorRequest request,
        ExternalCombinerToolManifest manifest,
        StagedExternalProcessorInvocation invocation,
        string runDirectory,
        string executablePath)
    {
        internal IStagedExternalProcessorAdapter Adapter { get; } = adapter;

        internal ExternalProcessorRequest Request { get; } = request;

        internal ExternalCombinerToolManifest Manifest { get; } = manifest;

        internal StagedExternalProcessorInvocation Invocation { get; } = invocation;

        internal string RunDirectory { get; } = runDirectory;

        internal string ExecutablePath { get; } = executablePath;

        internal string InputPath => Path.Combine(RunDirectory, Invocation.InputRelativePath);

        internal string OutputPath => Path.Combine(RunDirectory, Invocation.OutputRelativePath);

        // Mutated only by the sequential host execution flow for this session.
        internal List<ExternalProcessInvocation> ExecutedCommands { get; } = [];

        internal IReadOnlyList<ExternalProcessInvocation> ExceptionCommands =>
            Adapter.RetainCommandsOnException(Request) ? [.. ExecutedCommands] : [];

        internal ValueTask<CompositionIssue?> RunAsync(
            IReadOnlyList<string> arguments,
            string? commandId,
            CancellationToken cancellationToken)
        {
            return host.RunProcessAsync(this, arguments, commandId, cancellationToken);
        }
    }
}
