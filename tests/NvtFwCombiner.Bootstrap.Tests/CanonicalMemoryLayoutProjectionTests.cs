using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Characterizes the canonical NT51929 Memory Layout pilot routes.</summary>
public sealed class CanonicalMemoryLayoutProjectionTests
{
    /// <summary>Projects the real Standard Merge capability without copying its physical map.</summary>
    [Fact]
    public void Nt51929StandardMergeProjectsThePublishedCanonicalCapability()
    {
        PilotFixture fixture = CreatePilot(ExperienceIds.StandardMerge);

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            fixture.Session,
            fixture.Capability.CompiledComposition);

        Assert.Equal(0x40000, snapshot.Capacity);
        Assert.Equal(
            ["copy-tp", "copy-dp"],
            fixture.Capability.CompiledComposition.Plan.OrderedOperations
                .Select(static operation => operation.OperationId));
        Assert.DoesNotContain(
            snapshot.AfterSegments,
            static segment => segment.Disposition == MemoryWorkflowDisposition.Kept);
        Assert.Equal(2, snapshot.PendingItems.Count);
        AssertCanonicalProjection(fixture, snapshot);
    }

    /// <summary>Projects the real V2 DP Replace capability and its nested DPCMI map authority.</summary>
    [Fact]
    public void Nt51929DpReplaceProjectsThePublishedCanonicalCapability()
    {
        PilotFixture fixture = CreatePilot(ExperienceIds.DpReplace);

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            fixture.Session,
            fixture.Capability.CompiledComposition);

        Assert.Equal(0x40000, snapshot.Capacity);
        Assert.Contains(
            snapshot.CanonicalRegions,
            static region => region.RegionId == "initial-code-cmd1-page0-anchor");
        Assert.Contains(
            fixture.Capability.CompiledComposition.Plan.OrderedOperations,
            static operation => operation.OperationId == "replace-dp-code");
        Assert.DoesNotContain(
            snapshot.AfterSegments,
            static segment => segment.Disposition == MemoryWorkflowDisposition.Kept);
        Assert.Equal(2, snapshot.PendingItems.Count);
        AssertCanonicalProjection(fixture, snapshot);
    }

    /// <summary>DP Replace keeps retained base ranges distinct from compiled replacement ranges.</summary>
    [Fact]
    public void DpReplaceDistinguishesBaseFirmwareFromReplacementInputs()
    {
        MemoryLayoutSnapshot layout =
            CanonicalMemoryLayoutTestSupport.PrepareDpReplace("NT51951", 0x80000);

        Assert.Contains(layout.BeforeSegments, static segment =>
            segment.Disposition == MemoryWorkflowDisposition.Kept);
        Assert.Contains(layout.AfterSegments, static segment =>
            segment.Disposition == MemoryWorkflowDisposition.WillReplace &&
            segment.SourceSpaceId is CompositionAddressSpaceIds.DpReplacement or
                CompositionAddressSpaceIds.InitialCodeReplacement);
        Assert.DoesNotContain(layout.AfterSegments, static segment =>
            segment.Disposition == MemoryWorkflowDisposition.Kept &&
            segment.SourceSpaceId is CompositionAddressSpaceIds.DpReplacement or
                CompositionAddressSpaceIds.InitialCodeReplacement);
    }

    /// <summary>Projects the real AB Merge overlay chain without a Bootstrap display replica.</summary>
    [Fact]
    public void Nt51929AbMergeProjectsThePublishedCanonicalCapability()
    {
        PilotFixture fixture = CreatePilot(ExperienceIds.AbMerge);

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            fixture.Session,
            fixture.Capability.CompiledComposition);

        Assert.Equal(0x80000, snapshot.Capacity);
        IReadOnlyList<string> requiredInputs = fixture.Capability.CompiledComposition
            .Plan.RequiredInputAddressSpaceIds;
        Assert.Contains(CompositionAddressSpaceIds.DpAbInput, requiredInputs);
        Assert.Contains(CompositionAddressSpaceIds.TpAInput, requiredInputs);
        Assert.Contains(CompositionAddressSpaceIds.TpBInput, requiredInputs);
        Assert.Contains(snapshot.AfterSegments, static segment =>
            segment.ContributingOperations.Any(static operation =>
                StringComparer.Ordinal.Equals(operation.OperationId, "copy-tpb")));
        AssertCanonicalProjection(fixture, snapshot);
    }

    private static void AssertCanonicalProjection(
        PilotFixture fixture,
        MemoryLayoutSnapshot snapshot)
    {
        FirmwareImageMap map = fixture.Capability.CompiledComposition.V2Details
            .Provenance.ResolvedMap.ImageMap;
        Assert.Same(map, fixture.Map);
        Assert.Equal(map.Regions.Count, snapshot.CanonicalRegions.Count);
        Assert.All(
            snapshot.CanonicalRegions,
            region => Assert.Contains(map.Regions, candidate => ReferenceEquals(candidate, region)));
        Assert.Equal(0, snapshot.AfterSegments[0].Range.Start);
        Assert.Equal(snapshot.Capacity, snapshot.AfterSegments[^1].Range.EndExclusive);
    }

    private static PilotFixture CreatePilot(string workflowId)
    {
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource(
                NvtFwCombiner.TestSupport.RetainedDpReplaceRegressionPolicy.Load));
        CapabilityCatalogReloadResult reload =
            catalog.Reload(TestContext.Current.CancellationToken);
        Assert.True(
            reload.Succeeded,
            string.Join(
                Environment.NewLine,
                reload.Issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
        CanonicalCapabilityCatalogSnapshot snapshot = reload.Snapshot!;
        ResolvedCapability capability = snapshot.Capabilities.Single(candidate =>
            candidate.Identity.IcId == "NT51929" &&
            candidate.Identity.WorkflowId == workflowId);
        var authoringCatalog =
            AuthoringCapabilityCatalogSnapshot.FromCanonical(snapshot, workflowId);
        var session = new AuthoringSessionState(workflowId);
        AuthoringSessionTransitionResult activation = session.Activate(authoringCatalog);
        Assert.True(
            activation.Succeeded,
            activation.Issue is null
                ? string.Empty
                : $"{activation.Issue.Code}: {activation.Issue.Message}");
        AuthoringSessionTransitionResult selection = session.Select(
            capability.Identity.IcId,
            capability.Identity.IcCountVariant);
        Assert.True(
            selection.Succeeded,
            selection.Issue is null
                ? string.Empty
                : $"{selection.Issue.Code}: {selection.Issue.Message}");
        ActiveSessionSnapshot authoring = selection.Snapshot!;
        FirmwareImageMap map = capability.CompiledComposition.V2Details
            .Provenance.ResolvedMap.ImageMap;
        return new PilotFixture(capability, authoring, map);
    }

    private sealed record PilotFixture(
        ResolvedCapability Capability,
        ActiveSessionSnapshot Session,
        FirmwareImageMap Map);
}
