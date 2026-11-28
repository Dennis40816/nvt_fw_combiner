// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>Verifies lossless preparation through the existing General parsers.</summary>
public sealed class GeneralMergeDraftPreparationTests
{
    /// <summary>Initializer failures retain the original reason and admission result.</summary>
    [Theory]
    [InlineData("0", "0xFF", GeneralMergeInitializerIssueCodes.CapacityInvalid,
        "General Merge output length must be a positive byte count.", "output-length")]
    [InlineData("2147483648", "0xFF", GeneralMergeInitializerIssueCodes.CapacityUnsupported,
        "General Merge output length exceeds the supported in-memory composition size.", "output-length")]
    [InlineData("16", "0x100", GeneralMergeInitializerIssueCodes.FillByteInvalid,
        "General Merge fill byte must be a hexadecimal or decimal value from 0x00 through 0xFF.", "fill-byte")]
    public void InvalidInitializerRetainsTypedRefusalAndLegacyDecision(
        string length, string fill, string code, string message, string subject)
    {
        GeneralMergeDraftPreparation result = GeneralMergeAuthoringUseCase.PrepareDraft(
            length, fill, [Mapping("copy", "0", "2")]);
        bool legacy = GeneralMergeAuthoringUseCase.TryResolveOutputInitializer(
            length, fill, out GeneralMergeInitializer? initializer);

        Assert.False(legacy);
        Assert.Null(initializer);
        Assert.False(result.Succeeded);
        Assert.Null(result.Draft);
        Assert.False(result.Initializer.Succeeded);
        Assert.Null(result.Initializer.Initializer);
        Assert.Equal([(code, message, subject, CompositionIssueSeverity.Error)], Facts(result.Issues));
        Assert.Same(result.Initializer.Issue, Assert.Single(result.Issues));
        Assert.Empty(result.DraftIssues);
    }

    /// <summary>Every invalid mapping is retained with the draft owner's unchanged blocking code.</summary>
    [Fact]
    public void InvalidRangesRetainAllDraftIssuesAndLegacyDecision()
    {
        AuthoringMappingState[] states = [Mapping("first", "bad", "2"), Mapping("second", "0", "0")];

        GeneralMergeDraftPreparation result = GeneralMergeAuthoringUseCase.PrepareDraft("16", "0xFF", states);
        bool legacy = GeneralAuthoringMappingUseCase.TryCreateGeneralMergeAuthoringDraft(
            states, out GeneralMappingDraftState? draft, out IReadOnlyList<CompositionIssue> issues);

        Assert.False(legacy);
        Assert.Null(draft);
        Assert.False(result.Succeeded);
        Assert.Null(result.Draft);
        Assert.True(result.Initializer.Succeeded);
        Assert.Equal(Facts(issues), Facts(result.Issues));
        Assert.Equal(
            [
                (CompositionPlanningIssueCodes.GeneralMergeRangeInvalid,
                    $"General Merge mapping 'first' is invalid: {states[0].Issue!.Message}",
                    "first", CompositionIssueSeverity.Error),
                (CompositionPlanningIssueCodes.GeneralMergeRangeInvalid,
                    $"General Merge mapping 'second' is invalid: {states[1].Issue!.Message}",
                    "second", CompositionIssueSeverity.Error),
            ],
            Facts(result.DraftIssues));
    }

    /// <summary>Independent mapping refusals survive an initializer refusal without admitting a draft.</summary>
    [Fact]
    public void InitializerAndMappingRefusalsRemainOrderedAndBlocking()
    {
        AuthoringMappingState state = Mapping("copy", "bad", "2");

        GeneralMergeDraftPreparation result = GeneralMergeAuthoringUseCase.PrepareDraft("0", "0", [state]);

        Assert.False(result.Succeeded);
        Assert.Null(result.Draft);
        Assert.Equal(
            [
                (GeneralMergeInitializerIssueCodes.CapacityInvalid,
                    "General Merge output length must be a positive byte count.",
                    "output-length", CompositionIssueSeverity.Error),
                (CompositionPlanningIssueCodes.GeneralMergeRangeInvalid,
                    $"General Merge mapping 'copy' is invalid: {state.Issue!.Message}",
                    "copy", CompositionIssueSeverity.Error),
            ],
            Facts(result.Issues));
        Assert.Same(result.Initializer.Issue, result.Issues[0]);
        Assert.Same(Assert.Single(result.DraftIssues), result.Issues[1]);
    }

    /// <summary>Valid text produces the same initializer and mapping draft as the original API.</summary>
    [Theory]
    [InlineData(null, 0)]
    [InlineData("0xFF", 255)]
    public void ValidPreparationPreservesDraftAndLegacyDecisions(string? fill, int expectedFill)
    {
        AuthoringMappingState state = Mapping("copy", "0", "2");

        GeneralMergeDraftPreparation result = GeneralMergeAuthoringUseCase.PrepareDraft("16", fill, [state]);
        bool initializerAccepted = GeneralMergeAuthoringUseCase.TryResolveOutputInitializer(
            "16", fill, out GeneralMergeInitializer? initializer);
        bool mappingsAccepted = GeneralAuthoringMappingUseCase.TryCreateGeneralMergeAuthoringDraft(
            [state], out GeneralMappingDraftState? mappings, out IReadOnlyList<CompositionIssue> issues);

        Assert.True(initializerAccepted);
        Assert.True(mappingsAccepted);
        Assert.True(result.Succeeded);
        Assert.True(result.Initializer.Succeeded);
        Assert.Null(result.Initializer.Issue);
        Assert.Empty(result.Issues);
        Assert.Empty(result.DraftIssues);
        Assert.Empty(issues);
        Assert.Equal(16, result.Draft!.OutputInitializer.Capacity);
        Assert.Equal(expectedFill, result.Draft.OutputInitializer.FillByte);
        Assert.Equal(initializer!.Value, result.Draft.OutputInitializer);
        Assert.Equal(mappings!.Rows, result.Draft.Mappings.Rows);
    }

    private static AuthoringMappingState Mapping(string id, string start, string length)
    {
        return GeneralAuthoringMappingUseCase.CreateGeneralMergeAuthoringState(
            id, "synthetic.bin", start, "0", length);
    }

    private static (string Code, string Message, string? Subject, string Severity)[] Facts(
        IReadOnlyList<CompositionIssue> issues)
    {
        return [.. issues.Select(static issue => (issue.Code, issue.Message, issue.OperationId, issue.Severity))];
    }
}
