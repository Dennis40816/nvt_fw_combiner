// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

internal sealed partial class LegacyCombinerProcessorAdapter
{
    private static async ValueTask<CompositionIssue?> ExecuteManifestAsync(
        StagedExternalProcessorHost.Session session,
        CancellationToken cancellationToken)
    {
        ExternalProcessorRequest request = session.Request;
        ExternalCombinerInvocationProfile invocation = session.Invocation.ManifestInvocation!;
        Dictionary<string, string> stagedArtifactPaths = await MaterializeStagedArtifactsAsync(
            request,
            session.RunDirectory,
            cancellationToken).ConfigureAwait(false);
        if (!TryExpandArguments(
                invocation.ArgumentTemplate,
                session.InputPath,
                Path.Combine(session.RunDirectory, OutputFileName),
                session.RunDirectory,
                stagedArtifactPaths,
                request.StagedArtifacts,
                out IReadOnlyList<string>? arguments,
                out CompositionIssue? argumentIssue))
        {
            return argumentIssue;
        }

        CompositionIssue? processIssue = await session.RunAsync(arguments!, null, cancellationToken).ConfigureAwait(false);
        if (processIssue is not null)
        {
            return processIssue;
        }

        CompositionIssue? unexpectedFileIssue = FindUnexpectedStagingFileIssue(
            session.RunDirectory,
            invocation.InputMode,
            session.Manifest,
            stagedArtifactPaths.Values);
        return unexpectedFileIssue ?? await VerifyStagedArtifactsUnchangedAsync(
            request.StagedArtifacts,
            stagedArtifactPaths,
            cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, ExternalCombinerInvocationProfile> CreateInvocationIndex(
        IEnumerable<ExternalCombinerInvocationProfile> invocationProfiles)
    {
        Dictionary<string, ExternalCombinerInvocationProfile> byProcessorId = new(StringComparer.Ordinal);
        foreach (ExternalCombinerInvocationProfile invocation in invocationProfiles)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            if (!byProcessorId.TryAdd(invocation.ProcessorId, invocation))
            {
                throw new ArgumentException(
                    $"External Combiner invocation processor id '{invocation.ProcessorId}' is declared more than once.",
                    nameof(invocationProfiles));
            }
        }

        return byProcessorId;
    }

    private bool TryResolveInvocation(
        ExternalProcessorRequest request,
        ExternalCombinerToolManifest manifest,
        out ExternalCombinerInvocationProfile invocation,
        out CompositionIssue? issue)
    {
        invocation = _invocationsByProcessorId.TryGetValue(request.ProcessorId, out ExternalCombinerInvocationProfile? selected) &&
            selected is not null
            ? selected
            : new ExternalCombinerInvocationProfile(
                request.ProcessorId,
                manifest.ToolBindingId,
                manifest.InputMode,
                manifest.ArgumentTemplate);

        if (!string.Equals(invocation.ToolBindingId, request.ToolBindingId, StringComparison.Ordinal))
        {
            issue = new CompositionIssue(
                "external-tool.invocation.binding-mismatch",
                "External processor invocation does not permit the profile-selected tool binding.");
            return false;
        }

        if (invocation.InputMode is not ("in-place" or "input-output-file"))
        {
            issue = new CompositionIssue(
                "external-tool.invocation.input-mode.invalid",
                "External processor invocation declares an unsupported staging input mode.");
            return false;
        }

        issue = null;
        return true;
    }
}
