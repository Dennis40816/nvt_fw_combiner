// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>Verifies complete per-slot findings independently of the retained admission summary.</summary>
[Collection(nameof(AuthoringInputSlotInspectionSerialGroup))]
public sealed class AuthoringInputSlotFindingTests
{
    /// <summary>
    /// All ranges and rules publish in stable priority and contract order. The compiled contract orders rules by
    /// ordinal rule id, so "advisory" precedes "primary" when both are warnings.
    /// </summary>
    [Theory]
    [InlineData(CompiledValidationSeverity.Warning, false, AuthoringSlotLifecycle.Warning)]
    [InlineData(CompiledValidationSeverity.Error, true, AuthoringSlotLifecycle.Error)]
    public void EveryEvaluableRangeIsPublishedWithoutChangingSummary(
        CompiledValidationSeverity severity, bool blocksBuild, AuthoringSlotLifecycle lifecycle)
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge,
            validationRequirements:
            [
                Uniform("advisory", CompiledValidationSeverity.Warning, "SECOND", new ByteRange(0, 2)),
                CompiledValidationRequirements.RejectUniformInputRanges(
                    "primary", severity, "PRIMARY", SourceSpace, [new ByteRange(0, 2), new ByteRange(2, 2)]),
            ]);

        AuthoringInputSlotStatus result = Inspect(capability, [0xAA, 0xAA, 0xBB, 0xBB]);
        AuthoringInputSlotStatus repeated = Inspect(capability, [0xAA, 0xAA, 0xBB, 0xBB]);
        CompiledInputArtifactInspectionFinding secondary = UniformFinding("SECOND", "advisory", 0, 0xAA);
        CompiledInputArtifactInspectionFinding first = UniformFinding("PRIMARY", "primary", 0, 0xAA,
            CompositionIssueSeverity.FromCompiled(severity));
        CompiledInputArtifactInspectionFinding second = UniformFinding("PRIMARY", "primary", 2, 0xBB,
            CompositionIssueSeverity.FromCompiled(severity));
        CompiledInputArtifactInspectionFinding[] expected = blocksBuild
            ? [first, second, secondary]
            : [secondary, first, second];

        Assert.Equal(expected.Select(Facts), result.InspectionFindings.Select(Facts));
        Assert.Equal(result.InspectionFindings.Select(Facts), repeated.InspectionFindings.Select(Facts));
        Assert.Equal(expected.Select(Facts), result.Inspection!.Findings.Select(Facts));
        Assert.Equal(blocksBuild, result.BlocksBuild);
        Assert.Equal(blocksBuild, result.Inspection.BlocksBuild);
        Assert.Equal(lifecycle, result.InspectionLifecycle);
        Assert.Equal(blocksBuild ? "PRIMARY" : "SECOND", result.InspectionIssueCode);
        Assert.Equal(blocksBuild ? CompiledInputArtifactInspectionSeverity.Blocking :
            CompiledInputArtifactInspectionSeverity.Warning, result.Inspection.Severity);
        Assert.Equal(CompiledInputArtifactInspectionNextAction.None, result.InspectionNextAction);
        Assert.Equal(blocksBuild ? first.DiagnosticEvidence : secondary.DiagnosticEvidence,
            result.Inspection.DiagnosticEvidence);
        Assert.Equal(blocksBuild ? "PRIMARY" : null, result.Inspection.AdmissionIssue?.Code);
    }

    /// <summary>An older envelope warning remains the summary while range warnings remain visible.</summary>
    [Theory]
    [InlineData(CompiledValidationSeverity.Warning, false)]
    [InlineData(CompiledValidationSeverity.Error, true)]
    public void ExistingWarningDoesNotHideOtherFindingsOrChangeSummary(
        CompiledValidationSeverity severity, bool blocksBuild)
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.CtrlRamReplace,
            validationRequirement: Uniform("content", severity, "CONTENT", new ByteRange(0, 2)));

        AuthoringInputSlotStatus result = Inspect(capability, [0xAA, 0xAA, 0x10, 0x20, 0x30, 0x40]);
        CompiledInputArtifactInspectionFinding trailing = new(
            new CompiledInputArtifactFindingIdentity(SourceSlot, SourceSpace,
                "APPLICATION_TEST_CTRLRAM_TRUNCATED", new ByteRange(4, 2), SourceSpace),
            CompositionIssueSeverity.Warning,
            CompiledInputArtifactInspectionNextAction.ReviewIgnoredTrailingBytes, null);

        CompiledInputArtifactInspectionFinding uniform = UniformFinding("CONTENT", "content", 0, 0xAA,
            CompositionIssueSeverity.FromCompiled(severity));
        CompiledInputArtifactInspectionFinding[] expected = [trailing, uniform];
        Assert.Equal(expected.Select(Facts), result.InspectionFindings.Select(Facts));
        Assert.Equal(blocksBuild, result.BlocksBuild);
        Assert.Equal(blocksBuild ? AuthoringSlotLifecycle.Error : AuthoringSlotLifecycle.Warning, result.InspectionLifecycle);
        Assert.Equal(blocksBuild ? "CONTENT" : "APPLICATION_TEST_CTRLRAM_TRUNCATED", result.InspectionIssueCode);
        Assert.Equal(blocksBuild ? CompiledInputArtifactInspectionSeverity.Blocking :
            CompiledInputArtifactInspectionSeverity.Warning, result.Inspection!.Severity);
        Assert.Equal(blocksBuild ? CompiledInputArtifactInspectionNextAction.None :
            CompiledInputArtifactInspectionNextAction.ReviewIgnoredTrailingBytes, result.InspectionNextAction);
        Assert.Equal(blocksBuild ? "CONTENT" : null, result.Inspection.AdmissionIssue?.Code);
        Assert.Equal(blocksBuild ? uniform.DiagnosticEvidence : null, result.Inspection.DiagnosticEvidence);
    }

    /// <summary>A short source publishes covered findings only, retaining its original structural blocker.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ShortSourceDoesNotInventFindingsForUnreadableRanges(int sourceLength)
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge,
            validationRequirement: CompiledValidationRequirements.RejectUniformInputRanges(
                "content", CompiledValidationSeverity.Warning, "CONTENT", SourceSpace,
                [new ByteRange(0, 2), new ByteRange(2, 2)]));

        AuthoringInputSlotStatus result = Inspect(capability, [.. Enumerable.Repeat((byte)0xAA, sourceLength)]);
        InputDiagnosticEvidence evidence = new(SourceSpace, sourceLength, 4, null, null);
        CompiledInputArtifactInspectionFinding structural = new(
            new CompiledInputArtifactFindingIdentity(SourceSlot, SourceSpace,
                CompositionIssueCodes.InputSourceViewIncomplete, null, SourceSpace),
            CompositionIssueSeverity.Error, CompiledInputArtifactInspectionNextAction.SelectCompatibleInput, evidence);

        CompiledInputArtifactInspectionFinding[] expected = sourceLength == 0
            ? [structural]
            : [structural, UniformFinding("CONTENT", "content", 0, 0xAA)];
        Assert.Equal(expected.Select(Facts), result.InspectionFindings.Select(Facts));
        Assert.True(result.BlocksBuild);
        Assert.Equal(AuthoringSlotLifecycle.Error, result.InspectionLifecycle);
        Assert.Equal(CompositionIssueCodes.InputSourceViewIncomplete, result.InspectionIssueCode);
        Assert.Equal(CompiledInputArtifactInspectionSeverity.Blocking, result.Inspection!.Severity);
        Assert.Equal(CompiledInputArtifactInspectionNextAction.SelectCompatibleInput, result.InspectionNextAction);
        Assert.Equal(evidence, result.Inspection.DiagnosticEvidence);
        Assert.Null(result.Inspection.AdmissionIssue);
        Assert.Null(result.Inspection.AcceptedSnapshotRange);
    }

    /// <summary>A passing range and an unreadable range cannot become placeholder findings.</summary>
    [Theory]
    [InlineData(CompiledValidationSeverity.Warning)]
    [InlineData(CompiledValidationSeverity.Error)]
    public void PassFailAndUnreadableRangesPublishOnlyTheViolation(CompiledValidationSeverity severity)
    {
        CompiledUniformInputRangeValidation requirement = CompiledValidationRequirements.RejectUniformInputRanges(
            "content", severity, "CONTENT", SourceSpace,
            [new ByteRange(3, 1), new ByteRange(2, 1), new ByteRange(0, 2)]);
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge,
            validationRequirement: requirement);
        byte[] bytes = [0x10, 0x20, 0xAA];

        AuthoringInputSlotStatus result = Inspect(capability, bytes);
        InputLoadValidationEvaluationResult evaluation = CompiledInputLoadValidationEvaluator.Evaluate(bytes, requirement);
        InputDiagnosticEvidence structuralEvidence = new(SourceSpace, 3, 4, null, null);
        ByteRange failedRange = new(2, 1);
        string issueSeverity = CompositionIssueSeverity.FromCompiled(severity);
        CompiledInputArtifactInspectionFinding violation = new(
            new CompiledInputArtifactFindingIdentity(SourceSlot, SourceSpace, "CONTENT", failedRange, "content"),
            issueSeverity, CompiledInputArtifactInspectionNextAction.None,
            new InputDiagnosticEvidence(SourceSpace, null, null, failedRange, 0xAA),
            new CompositionIssue("CONTENT",
                $"Input range {failedRange} in '{SourceSpace}' is a repeated-byte placeholder and cannot be used.",
                "content", issueSeverity));
        CompiledInputArtifactInspectionFinding[] expected =
        [
            new(new CompiledInputArtifactFindingIdentity(SourceSlot, SourceSpace,
                CompositionIssueCodes.InputSourceViewIncomplete, null, SourceSpace),
                CompositionIssueSeverity.Error, CompiledInputArtifactInspectionNextAction.SelectCompatibleInput,
                structuralEvidence),
            violation,
        ];

        Assert.Equal(expected.Select(Facts), result.InspectionFindings.Select(Facts));
        Assert.Equal(expected.Select(Facts), result.Inspection!.Findings.Select(Facts));
        InputLoadValidationRangeFinding rangeFinding = Assert.Single(evaluation.RangeFindings);
        Assert.Same(rangeFinding.Issue, evaluation.Issue);
        Assert.Equal(violation.DiagnosticEvidence, evaluation.DiagnosticEvidence);
        Assert.Same(rangeFinding.DiagnosticEvidence, evaluation.DiagnosticEvidence);
        Assert.True(result.BlocksBuild);
        Assert.Equal(AuthoringSlotLifecycle.Error, result.InspectionLifecycle);
        Assert.Equal(CompositionIssueCodes.InputSourceViewIncomplete, result.InspectionIssueCode);
        Assert.Equal(CompiledInputArtifactInspectionSeverity.Blocking, result.Inspection.Severity);
        Assert.Equal(CompiledInputArtifactInspectionNextAction.SelectCompatibleInput, result.InspectionNextAction);
        Assert.Equal(structuralEvidence, result.Inspection.DiagnosticEvidence);
        Assert.Null(result.Inspection.AdmissionIssue);
        Assert.Null(result.Inspection.AcceptedSnapshotRange);
    }

    /// <summary>A coverage refusal retains its original first-range evidence without inventing a finding.</summary>
    [Fact]
    public void UnreadableRangeKeepsLegacySummaryAndPublishesNoRangeFinding()
    {
        CompiledUniformInputRangeValidation requirement = CompiledValidationRequirements.RejectUniformInputRanges(
            "content", CompiledValidationSeverity.Error, "CONTENT", SourceSpace,
            [new ByteRange(0, 2), new ByteRange(2, 2), new ByteRange(4, 2)]);
        Dictionary<string, byte[]> inputs = new(StringComparer.Ordinal)
        {
            [SourceSpace] = [0x10, 0x20],
        };

        InputLoadValidationEvaluationResult result = CompiledInputLoadValidationEvaluator.Evaluate(inputs, requirement);

        Assert.Empty(result.RangeFindings);
        Assert.NotNull(result.Issue);
        Assert.Equal(("CONTENT", $"Input validation range {new ByteRange(2, 2)} is outside address space '{SourceSpace}'.",
                "content", CompositionIssueSeverity.Error),
            (result.Issue.Code, result.Issue.Message, result.Issue.OperationId, result.Issue.Severity));
        Assert.Equal(new InputDiagnosticEvidence(SourceSpace, 2, 4, new ByteRange(2, 2), null),
            result.DiagnosticEvidence);
    }

    /// <summary>An absent address space retains its path-free refusal and has no evaluable range.</summary>
    [Fact]
    public void MissingAddressSpaceKeepsLegacySummaryAndPublishesNoRangeFinding()
    {
        CompiledUniformInputRangeValidation requirement = Uniform(
            "content", CompiledValidationSeverity.Warning, "CONTENT", new ByteRange(0, 2));
        Dictionary<string, byte[]> inputs = new(StringComparer.Ordinal);

        InputLoadValidationEvaluationResult result = CompiledInputLoadValidationEvaluator.Evaluate(inputs, requirement);

        Assert.Empty(result.RangeFindings);
        Assert.Null(result.DiagnosticEvidence);
        Assert.NotNull(result.Issue);
        Assert.Equal(("CONTENT", $"Input validation cannot read required address space '{SourceSpace}'.",
                "content", CompositionIssueSeverity.Warning),
            (result.Issue.Code, result.Issue.Message, result.Issue.OperationId, result.Issue.Severity));
    }

    /// <summary>Occurrence identity includes every typed location component rather than the message or ordinal.</summary>
    [Fact]
    public void FindingIdentityChangesWithEachIdentityComponent()
    {
        CompiledInputArtifactFindingIdentity identity = new(SourceSlot, SourceSpace, "CONTENT", new ByteRange(0, 2), "rule");

        Assert.NotEqual(identity, identity with { SlotId = "another-slot" });
        Assert.NotEqual(identity, identity with { AddressSpaceId = "another-space" });
        Assert.NotEqual(identity, identity with { IssueCode = "ANOTHER" });
        Assert.NotEqual(identity, identity with { SourceRange = new ByteRange(2, 2) });
        Assert.NotEqual(identity, identity with { SubjectId = "another-rule" });
    }

    /// <summary>Existing informational version advisories join the list without changing the summary owner.</summary>
    [Fact]
    public void ObservationAdvisoryIsPublishedWithTheUnchangedWarningSummary()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.AbMerge, observeAbVersion: true);

        AuthoringInputSlotStatus result = Inspect(capability, [0x10, 0x20, 0x30, 0x40]);
        CompiledInputArtifactInspectionFinding[] expected =
        [
            new(new CompiledInputArtifactFindingIdentity(SourceSlot, SourceSpace,
                InputArtifactInspectionIssueCodes.AbVersionMetadataUnknown, null, SourceSlot),
                CompositionIssueSeverity.Warning, CompiledInputArtifactInspectionNextAction.ReviewUnknownVersion, null),
        ];

        Assert.Equal(expected, result.InspectionFindings);
        Assert.Empty(result.Inspection!.Findings);
        Assert.False(result.BlocksBuild);
        Assert.Equal(AuthoringSlotLifecycle.Warning, result.InspectionLifecycle);
        Assert.Equal(InputArtifactInspectionIssueCodes.AbVersionMetadataUnknown, result.InspectionIssueCode);
        Assert.Equal(CompiledInputArtifactInspectionSeverity.Valid, result.Inspection.Severity);
        Assert.Equal(CompiledInputArtifactInspectionNextAction.ReviewUnknownVersion, result.InspectionNextAction);
    }

    /// <summary>Informational findings remain informational and cannot alter the original valid summary.</summary>
    [Fact]
    public void InformationalFindingDoesNotChangeBuildAdmission()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge,
            validationRequirement: Uniform("content", CompiledValidationSeverity.Info, "CONTENT", new ByteRange(0, 2)));

        AuthoringInputSlotStatus result = Inspect(capability, [0xAA, 0xAA, 0x10, 0x20]);
        CompiledInputArtifactInspectionFinding[] expected =
            [UniformFinding("CONTENT", "content", 0, 0xAA, CompositionIssueSeverity.Info)];

        Assert.Equal(expected.Select(Facts), result.InspectionFindings.Select(Facts));
        Assert.False(result.BlocksBuild);
        Assert.Equal(AuthoringSlotLifecycle.Verified, result.InspectionLifecycle);
        Assert.Equal(InputArtifactInspectionIssueCodes.Ready, result.InspectionIssueCode);
        Assert.Equal(CompiledInputArtifactInspectionSeverity.Valid, result.Inspection!.Severity);
        Assert.Null(result.Inspection.DiagnosticEvidence);
        Assert.Null(result.Inspection.AdmissionIssue);
    }

    private static AuthoringInputSlotStatus Inspect(ResolvedCapability capability, byte[] bytes)
    {
        return AuthoringInputSlotInspectionService.Inspect(capability, new AuthoringRevision(7),
            new InputSelectionMemberReadiness(SourceSlot, true, ResolvedChildReadiness.Ready, true,
                "Ready.", null), SourceSpace, bytes);
    }

    private static CompiledUniformInputRangeValidation Uniform(
        string rule, CompiledValidationSeverity severity, string code, ByteRange range)
    {
        return CompiledValidationRequirements.RejectUniformInputRanges(rule, severity, code, SourceSpace, [range]);
    }

    private static CompiledInputArtifactInspectionFinding UniformFinding(
        string code, string rule, long start, byte repeatedByte, string severity = CompositionIssueSeverity.Warning)
    {
        ByteRange range = new(start, 2);
        return new CompiledInputArtifactInspectionFinding(
            new CompiledInputArtifactFindingIdentity(SourceSlot, SourceSpace, code, range, rule), severity,
            CompiledInputArtifactInspectionNextAction.None, new InputDiagnosticEvidence(SourceSpace, null, null, range, repeatedByte),
            new CompositionIssue(code,
                $"Input range {range} in '{SourceSpace}' is a repeated-byte placeholder and cannot be used.", rule, severity));
    }

    private static (CompiledInputArtifactFindingIdentity Identity, string Severity,
        CompiledInputArtifactInspectionNextAction NextAction, InputDiagnosticEvidence? Evidence,
        (string Code, string Message, string? Subject, string Severity)? Issue) Facts(
        CompiledInputArtifactInspectionFinding finding)
    {
        return (finding.Identity, finding.Severity, finding.NextAction, finding.DiagnosticEvidence,
            finding.Issue is { } issue ? (issue.Code, issue.Message, issue.OperationId, issue.Severity) : null);
    }
}
