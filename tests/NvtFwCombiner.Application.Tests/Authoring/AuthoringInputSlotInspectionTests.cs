using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>Tests the headless per-slot readiness and inspection publication contract.</summary>
[Collection(nameof(AuthoringInputSlotInspectionSerialGroup))]
public sealed partial class AuthoringInputSlotInspectionTests
{
    /// <summary>Prerequisite readiness remains independent from artifact inspection health.</summary>
    [Fact]
    public void PendingPrerequisiteProjectsDisabledSelectionWithoutInspectionHealth()
    {
        ResolvedCapabilityRoute route = CreateRoute(ExperienceIds.DpReplace);
        var readiness = new InputSelectionMemberReadiness(
            SourceSlot,
            IsSelected: false,
            ResolvedChildReadiness.PendingInput,
            CanSelect: false,
            "Load reference-base first.",
            new InputSelectionNextAction(
                InputSelectionNextActionKind.LoadArtifactFirst,
                "reference-base"));

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.ProjectReadiness(
            route,
            new AuthoringRevision(4),
            readiness,
            new CompiledInputSpaceBinding(
                SourceSpace,
                SourceSlot,
                CompiledInputInstancePolicy.Singleton));

        Assert.Equal(ResolvedChildReadiness.PendingInput, result.Readiness);
        Assert.False(result.CanSelect);
        Assert.Null(result.InspectionLifecycle);
        Assert.Null(result.FileStamp);
        Assert.Equal("reference-base", result.ReadinessNextAction!.SubjectId);
        Assert.Equal(route.CapabilityFingerprint, result.CapabilityFingerprint);
        Assert.Null(result.CompilationFingerprint);
    }

    /// <summary>Compiled readiness carries the exact composition identity without inventing health.</summary>
    [Fact]
    public void CompiledReadinessProjectsCompilationIdentityWithoutInspectionHealth()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.ProjectReadiness(
            capability,
            new AuthoringRevision(5),
            ReadySelection(),
            SourceSpace);

        Assert.Equal(
            capability.CompiledComposition.CompilationFingerprint,
            result.CompilationFingerprint);
        Assert.Null(result.InspectionLifecycle);
        Assert.Null(result.FileStamp);
        Assert.Null(result.Inspection);
    }

    /// <summary>Inspection admission requires one ready selection bound by the compiler.</summary>
    [Fact]
    public void InspectionRejectsUnreadySelectionAndMismatchedBinding()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);
        InputSelectionMemberReadiness unselected = ReadySelection() with
        {
            IsSelected = false,
        };
        InputSelectionMemberReadiness wrongSlot = ReadySelection() with
        {
            SlotId = "different-slot",
        };

        _ = Assert.Throws<ArgumentException>(() =>
            AuthoringInputSlotInspectionService.Inspect(
                capability,
                new AuthoringRevision(6),
                unselected,
                SourceSpace,
                sourceBytes: ReadOnlyMemory<byte>.Empty));
        _ = Assert.Throws<ArgumentException>(() =>
            AuthoringInputSlotInspectionService.ProjectReadiness(
                capability,
                new AuthoringRevision(6),
                wrongSlot,
                SourceSpace));
    }

    /// <summary>Every targeted workflow reaches terminal typed health without Presentation.</summary>
    [Theory]
    [InlineData(ExperienceIds.StandardMerge, 4, AuthoringSlotLifecycle.Verified)]
    [InlineData(ExperienceIds.AbMerge, 4, AuthoringSlotLifecycle.Verified)]
    [InlineData(ExperienceIds.DpReplace, 4, AuthoringSlotLifecycle.Error)]
    [InlineData(ExperienceIds.CtrlRamReplace, 6, AuthoringSlotLifecycle.Warning)]
    public void FourWorkflowsPublishTerminalCompiledInspection(
        string workflowId,
        int sourceLength,
        AuthoringSlotLifecycle expectedLifecycle)
    {
        ResolvedCapability capability = CreateCapability(workflowId);
        byte[] source = [.. Enumerable.Range(0, sourceLength).Select(static value => (byte)value)];

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(6),
            ReadySelection(),
            SourceSpace,
            source);

        Assert.Equal(workflowId, result.WorkflowId);
        Assert.Equal(expectedLifecycle, result.InspectionLifecycle);
        Assert.Equal(FileStamp.FromBytes(source), result.FileStamp);
        Assert.Equal(SourceSlot, result.SlotId);
        Assert.Equal(capability.ResolutionToken, result.ResolutionToken);
        Assert.Equal(CapabilityFingerprint, result.CapabilityFingerprint);
        Assert.Equal(capability.CompiledComposition.CompilationFingerprint, result.CompilationFingerprint);
        Assert.NotNull(result.Inspection);
    }

    /// <summary>The retained TP maximum alias reaches terminal shared source-view health without throwing.</summary>
    [Theory]
    [InlineData(3, AuthoringSlotLifecycle.Error, CompositionIssueCodes.InputSourceViewIncomplete)]
    [InlineData(4, AuthoringSlotLifecycle.Error, "firmware-config.chip-count-unreadable")]
    [InlineData(8, AuthoringSlotLifecycle.Error, "firmware-config.chip-count-unreadable")]
    [InlineData(262145, AuthoringSlotLifecycle.Error, CompositionIssueCodes.InputAddressSpaceLengthMismatch)]
    public void TpMaximumCompatibilityPublishesTerminalCompiledInspection(
        int sourceLength,
        AuthoringSlotLifecycle expectedLifecycle,
        string expectedIssueCode)
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge, tpMaximum: true);

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(6),
            ReadySelection(),
            SourceSpace,
            new byte[sourceLength]);

        Assert.Equal(expectedLifecycle, result.InspectionLifecycle);
        Assert.Equal(expectedIssueCode, result.Inspection!.IssueCode);
        if (expectedIssueCode == "firmware-config.chip-count-unreadable")
        {
            Assert.Equal(new ByteRange(0, 4), result.Inspection.AcceptedSnapshotRange);
            Assert.Equal(FileStamp.FromBytes(new byte[4]).Sha256, result.Inspection.AcceptedSnapshotSha256);
            Assert.True(result.AcceptedBytes.GetValueOrDefault().IsEmpty);
        }
    }

    /// <summary>Profile-declared input-load plausibility is part of terminal slot health.</summary>
    [Fact]
    public void UniformAcceptedSourcePublishesWarning()
    {
        CompiledValidationRequirement validation =
            CompiledValidationRequirements.RejectUniformInputRanges(
                "source-content-plausibility",
                CompiledValidationSeverity.Warning,
                "SOURCE_UNIFORM_CONTENT_WARNING",
                SourceSpace,
                [new ByteRange(0, 2)]);
        ResolvedCapability capability = CreateCapability(
            ExperienceIds.StandardMerge,
            validationRequirement: validation);

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(7),
            ReadySelection(),
            SourceSpace,
            new byte[] { 0xAA, 0xAA, 0x10, 0x20 });

        Assert.Equal(AuthoringSlotLifecycle.Warning, result.InspectionLifecycle);
        Assert.False(result.Inspection!.BlocksBuild);
        Assert.Equal("SOURCE_UNIFORM_CONTENT_WARNING", result.Inspection.IssueCode);
    }

    /// <summary>Accepted compiled-role version metadata publishes Application-owned non-blocking Warning.</summary>
    [Fact]
    public void AcceptedUnknownVersionPublishesCanonicalWarningAndAction()
    {
        ResolvedCapability capability = CreateCapability(
            ExperienceIds.AbMerge,
            observeAbVersion: true);

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(7),
            ReadySelection(),
            SourceSpace,
            new byte[] { 0x10, 0x20, 0x30, 0x40 });

        Assert.Equal(AuthoringSlotLifecycle.Warning, result.InspectionLifecycle);
        Assert.False(result.BlocksBuild);
        Assert.Equal(InputArtifactInspectionIssueCodes.AbVersionMetadataUnknown, result.InspectionIssueCode);
        Assert.Equal(
            CompiledInputArtifactInspectionNextAction.ReviewUnknownVersion,
            result.InspectionNextAction);
        Assert.Equal(
            InputArtifactInspectionIssueCodes.AbVersionMetadataUnknown,
            Assert.Single(result.InspectionAdvisories).IssueCode);
        Assert.False(Assert.Single(result.Observation.Versions).IsKnown);
        Assert.Equal(
            CompiledInputArtifactInspectionSeverity.Valid,
            result.Inspection!.Severity);
    }

    /// <summary>An unreadable selected path is terminal Error without fabricating file identity.</summary>
    [Fact]
    public void UnreadableSelectedSourcePublishesBlockingError()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.DpReplace);

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(7),
            ReadySelection(),
            SourceSpace,
            sourceBytes: null);

        Assert.True(result.IsTerminal);
        Assert.Equal(AuthoringSlotLifecycle.Error, result.InspectionLifecycle);
        Assert.True(result.BlocksBuild);
        Assert.Equal(InputArtifactInspectionIssueCodes.SourceUnreadable, result.InspectionIssueCode);
        Assert.Equal(
            CompiledInputArtifactInspectionNextAction.SelectReadableInput,
            result.InspectionNextAction);
        Assert.Null(result.FileStamp);
        Assert.Null(result.Inspection);
    }

    /// <summary>Accepted execution bytes are one immutable copy owned by the inspection publication.</summary>
    [Fact]
    public void AcceptedInspectionRetainsImmutableExecutionBytes()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);
        byte[] source = [0x10, 0x20, 0x30, 0x40];

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(7),
            ReadySelection(),
            SourceSpace,
            source);
        source[0] = 0xff;

        Assert.Equal(0x10, result.AcceptedBytes!.Value.Span[0]);
        Assert.NotSame(source, result.AcceptedByteArray);
    }

    /// <summary>A short concrete CtrlRAM source is terminal Error rather than endless Checking.</summary>
    [Fact]
    public void ShortCtrlRamSourcePublishesBlockingError()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.CtrlRamReplace);

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(7),
            ReadySelection(),
            SourceSpace,
            new byte[3]);

        Assert.Equal(AuthoringSlotLifecycle.Error, result.InspectionLifecycle);
        Assert.True(result.Inspection!.BlocksBuild);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, result.Inspection.IssueCode);
    }

    private static InputSelectionMemberReadiness ReadySelection()
    {
        return new InputSelectionMemberReadiness(
            SourceSlot,
            IsSelected: true,
            ResolvedChildReadiness.Ready,
            CanSelect: true,
            Reason: null,
            NextAction: null);
    }

}
