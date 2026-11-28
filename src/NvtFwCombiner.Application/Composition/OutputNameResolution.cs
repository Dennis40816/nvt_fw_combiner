// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Resolved runtime output name with optional naming provenance and typed diagnostics.</summary>
internal sealed record OutputNameResolution(
    string FileName,
    OutputNamingSummary? Summary,
    IReadOnlyList<CompositionIssue> Issues)
{
    internal OutputNameResolution WithExplicitOverride(CompiledComposition composition, string fileName)
    {
        CompositionRunRequest.ValidateRuntimeOutputName(composition, fileName, outputFileNameIsOverride: true);
        return new OutputNameResolution(
            fileName,
            Summary is null ? null : new OutputNamingSummary(
                Summary.RendererKind, Summary.Template, Summary.AutomaticFileName, fileName,
                true, Summary.DateSource, Summary.ResolvedAtUtc, Summary.Tokens, Summary.Admission),
            Issues);
    }

    internal static OutputNameResolution Static(string fileName)
    {
        return new OutputNameResolution(fileName, null, []);
    }
}
