using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

public sealed partial class AuthoringInputSlotInspectionTests
{
    /// <summary>Compiled extension admission is ordinal-case-insensitive for an accepted BIN.</summary>
    [Theory]
    [InlineData("selected.bin")]
    [InlineData("selected.BIN")]
    public void AcceptedExtensionReachesContentInspection(string selectedPath)
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);
        byte[] source = [0x10, 0x20, 0x30, 0x40];

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(10),
            ReadySelection(),
            SourceSpace,
            source,
            selectedPath);

        Assert.Equal(AuthoringSlotLifecycle.Verified, result.InspectionLifecycle);
        Assert.False(result.BlocksBuild);
        Assert.Equal(InputArtifactInspectionIssueCodes.Ready, result.InspectionIssueCode);
        Assert.Equal(FileStamp.FromBytes(source), result.FileStamp);
        Assert.Equal(source, result.AcceptedBytes!.Value.ToArray());
    }

    /// <summary>An unaccepted extension is terminal typed Error before content gains authority.</summary>
    [Theory]
    [InlineData("selected.txt")]
    [InlineData("selected")]
    [InlineData("selected.bin.txt")]
    public void UnacceptedExtensionPublishesRetainedBlockingSelectionError(string selectedPath)
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(10),
            ReadySelection(),
            SourceSpace,
            new byte[] { 0x10, 0x20, 0x30, 0x40 },
            selectedPathHint: selectedPath);

        Assert.True(result.IsTerminal);
        Assert.Equal(AuthoringSlotLifecycle.Error, result.InspectionLifecycle);
        Assert.True(result.BlocksBuild);
        Assert.Equal(InputArtifactInspectionIssueCodes.ExtensionNotAccepted, result.InspectionIssueCode);
        Assert.Equal(
            CompiledInputArtifactInspectionNextAction.SelectCompatibleInput,
            result.InspectionNextAction);
        Assert.Equal(selectedPath, result.SelectedPathHint);
        Assert.Null(result.FileStamp);
        Assert.Null(result.Inspection);
        Assert.Null(result.AcceptedBytes);
        Assert.Empty(result.Observation.Versions);
        Assert.Empty(result.InspectionAdvisories);
    }

    /// <summary>The canonical batch owner publishes a path-bound extension Error without content identity.</summary>
    [Fact]
    public void ExtensionErrorPublishesThroughCanonicalSessionBatch()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);
        var route = new AuthoringCapabilityRoute(
            capability.Identity,
            capability.CapabilityFingerprint,
            executionAdmitted: true,
            [new AuthoringSlotDefinitionReference(SourceSlot)],
            capability.CompiledComposition.CompilationFingerprint);
        var catalog = new AuthoringCapabilityCatalogSnapshot(
            ExperienceIds.StandardMerge,
            capability.ResolutionToken,
            [route]);
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        Assert.True(session.Activate(catalog).Succeeded);
        AuthoringSlotInspectionStartResult started = session.BeginSlotFileInspection(
            SourceSlot,
            "selected.txt");
        Assert.True(started.Succeeded, started.Issue?.Message);
        AuthoringInputSlotStatus status = AuthoringInputSlotInspectionService.Inspect(
            capability,
            started.Snapshot!.AuthoringRevision,
            ReadySelection(),
            SourceSpace,
            new byte[] { 0x10, 0x20, 0x30, 0x40 },
            selectedPathHint: "selected.txt");

        AuthoringSessionTransitionResult completed = session.TryCompleteSlotFileInspectionBatch(
            catalog,
            [started.Lease!],
            new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal)
            {
                [SourceSlot] = status,
            });

        Assert.True(completed.Succeeded, completed.Issue?.Message);
        AuthoringSlotState slot = Assert.Single(completed.Snapshot!.Slots);
        Assert.Equal("selected.txt", slot.SelectedPath);
        Assert.Equal(AuthoringSlotLifecycle.Error, slot.Lifecycle);
        Assert.Null(slot.FileStamp);
        Assert.Equal(
            InputArtifactInspectionIssueCodes.ExtensionNotAccepted,
            slot.BlockingIssue!.IssueId);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.InputBlocked,
            ActiveSessionBuildBlockerResolver.Resolve(
                completed.Snapshot,
                ExperienceIds.StandardMerge)!.Code);
    }
}
