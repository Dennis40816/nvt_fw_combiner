// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Protocol preparation and parsing only; staging custody and mutation admission belong to the host.</summary>
internal interface IStagedExternalProcessorAdapter
{
    string Kind { get; }

    CompositionIssue? ValidateRequest(ExternalProcessorRequest request);

    CompositionIssue? ValidatePaths(ExternalProcessorRequest request, string runDirectory);

    bool TryPrepare(
        ExternalProcessorRequest request,
        ExternalCombinerToolManifest manifest,
        out StagedExternalProcessorInvocation? invocation,
        out CompositionIssue? issue);

    ValueTask<CompositionIssue?> ExecuteAsync(
        StagedExternalProcessorHost.Session session,
        CancellationToken cancellationToken);

    string DescribeMissingOutput(ExternalProcessorRequest request);

    string DescribeWriteViolation(ExternalProcessorRequest request, ChangedRangeVerdict verdict);

    string DescribeProcessFailure(string? commandId, ExternalProcessResult result);

    bool RetainCommandsOnException(ExternalProcessorRequest request);
}

/// <summary>Adapter-selected staging layout, containing only host-relative paths and a resolved protocol template.</summary>
internal sealed record StagedExternalProcessorInvocation(
    string InputRelativePath,
    string OutputRelativePath,
    IReadOnlyList<string> Directories,
    ExternalCombinerInvocationProfile? ManifestInvocation = null);
