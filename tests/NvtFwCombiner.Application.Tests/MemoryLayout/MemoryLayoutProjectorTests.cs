using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

/// <summary>Exercises the pure canonical memory-layout projection contract.</summary>
[Collection(nameof(MemoryLayoutProjectorSerialGroup))]
public sealed partial class MemoryLayoutProjectorTests
{
    /// <summary>Projects Merge blank initialization, selected writes, and pending input.</summary>
    [Fact]
    public void StandardMergeProjectsCanonicalGeometryAndSelectedWrites()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge);
        ActiveSessionSnapshot session = CreateSession(
            fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("tp-input", AuthoringSlotLifecycle.Empty));

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            fixture.Composition);

        Assert.Equal(
            fixture.Capability.CapabilityFingerprint,
            snapshot.CapabilityFingerprint);
        Assert.Equal(
            fixture.Composition.CompilationFingerprint,
            snapshot.CompilationFingerprint);
        Assert.NotEqual(
            snapshot.CapabilityFingerprint,
            snapshot.CompilationFingerprint);
        Assert.Equal("flash", snapshot.AddressSpaceId);
        Assert.Equal(Capacity, snapshot.Capacity);
        Assert.Equal(
            [new ByteRange(0, 8), new ByteRange(8, 4), new ByteRange(12, 4)],
            snapshot.BeforeSegments.Select(static segment => segment.Range));
        Assert.Equal(
            [
                MemoryWorkflowDisposition.Blank,
                MemoryWorkflowDisposition.Resolved,
                MemoryWorkflowDisposition.Blank,
            ],
            snapshot.BeforeSegments.Select(static segment => segment.Disposition));
        Assert.Equal(
            [
                MemoryWorkflowDisposition.WillWrite,
                MemoryWorkflowDisposition.Blank,
                MemoryWorkflowDisposition.Resolved,
                MemoryWorkflowDisposition.WillWrite,
            ],
            snapshot.AfterSegments.Select(static segment => segment.Disposition));
        Assert.Equal(
            [
                MemoryContentRole.Dp,
                MemoryContentRole.Dp,
                MemoryContentRole.Reserved,
                MemoryContentRole.Tp,
            ],
            snapshot.AfterSegments.Select(static segment => segment.ContentRole));

        MemoryLayoutSegment dp = snapshot.AfterSegments[0];
        Assert.Same(fixture.DpRegion, dp.CanonicalRegion);
        Assert.Equal("dp-input", dp.SourceSlotId);
        Assert.Equal(["copy-dp"], dp.ContributingOperations.Select(static operation => operation.OperationId));
        Assert.Equal(MemorySelectionState.Selected, dp.Selection);
        Assert.Equal(MemoryObservedChange.NotObserved, dp.ObservedChange);
        Assert.Equal(MemoryFocusState.NotFocused, dp.Focus);
        Assert.Equal(MemoryBankIdentity.NotApplicable, dp.Bank);
        Assert.Equal(MemoryEndpointIdentity.NotApplicable, dp.Endpoint);

        MemoryLayoutSegment tp = Assert.Single(
            snapshot.AfterSegments,
            static segment => segment.ContributingOperations.Any(
                static operation => operation.OperationId == "copy-tp"));
        Assert.Equal("tp-input", tp.SourceSlotId);
        Assert.Equal(MemorySelectionState.NotSelected, tp.Selection);

        MemoryLayoutPendingItem pending = Assert.Single(snapshot.PendingItems);
        Assert.Equal("tp-input", pending.SlotId);
        Assert.Equal(MemoryLayoutReadiness.PendingInput, pending.Readiness);
        Assert.Equal(MemoryLayoutPrerequisite.SelectInput, pending.Prerequisite);
        Assert.Equal(MemoryLayoutNextAction.SelectInput, pending.NextAction);
        Assert.Null(pending.KnownInputLength);
        Assert.DoesNotContain(
            typeof(MemoryLayoutPendingItem).GetProperties(),
            static property => property.PropertyType == typeof(ByteRange));
        Assert.DoesNotContain(
            snapshot.AfterSegments,
            static segment => segment.Disposition == MemoryWorkflowDisposition.Kept);
    }

    /// <summary>Projects Replace reference preservation outside admitted selected writes.</summary>
    [Fact]
    public void DpReplaceKeepsReferenceBytesOutsideSelectedReplacement()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Replace);
        ActiveSessionSnapshot session = CreateSession(
            fixture,
            Slot("reference-base", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("dp-replacement", AuthoringSlotLifecycle.Warning, Capacity));

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            fixture.Composition);

        Assert.All(
            snapshot.BeforeSegments,
            static segment =>
            {
                Assert.Equal(MemoryWorkflowDisposition.Kept, segment.Disposition);
                Assert.Equal("reference-base", segment.SourceSpaceId);
                Assert.Equal("reference-base", segment.SourceSlotId);
            });
        Assert.Equal(
            [
                MemoryWorkflowDisposition.WillReplace,
                MemoryWorkflowDisposition.Kept,
                MemoryWorkflowDisposition.Kept,
                MemoryWorkflowDisposition.Kept,
            ],
            snapshot.AfterSegments.Select(static segment => segment.Disposition));
        Assert.Equal(
            [
                MemoryDiagnosticSeverity.Warning,
                MemoryDiagnosticSeverity.None,
                MemoryDiagnosticSeverity.None,
                MemoryDiagnosticSeverity.None,
            ],
            snapshot.AfterSegments.Select(static segment => segment.DiagnosticSeverity));
        Assert.Empty(snapshot.PendingItems);
        Assert.Same(fixture.DpRegion, snapshot.AfterSegments[0].CanonicalRegion);
        Assert.Same(fixture.DpRegion, snapshot.AfterSegments[1].CanonicalRegion);
        Assert.Same(fixture.TpRegion, snapshot.AfterSegments[3].CanonicalRegion);
        Assert.Equal(
            ["replace-dp"],
            snapshot.AfterSegments[0].ContributingOperations.Select(static operation => operation.OperationId));
        Assert.Empty(snapshot.AfterSegments[1].ContributingOperations);
        Assert.Empty(snapshot.AfterSegments[3].ContributingOperations);
        Assert.All(
            snapshot.AfterSegments.Where(static segment =>
                segment.Disposition == MemoryWorkflowDisposition.Kept),
            static segment =>
            {
                Assert.Equal("reference-base", segment.SourceSpaceId);
                Assert.Equal("reference-base", segment.SourceSlotId);
            });
    }

    /// <summary>Keeps unresolved inputs non-geometric until a compiled overlay is supplied.</summary>
    [Fact]
    public void MissingCompiledOverlayKeepsSkeletonAndPublishesNonGeometricPendingState()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge);
        ActiveSessionSnapshot session = CreateSession(
            fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Checking, length: 12),
            Slot("tp-input", AuthoringSlotLifecycle.Empty));

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            compiledOverlay: null);

        Assert.Same(snapshot.BeforeSegments, snapshot.AfterSegments);
        Assert.Equal(2, snapshot.PendingItems.Count);
        MemoryLayoutPendingItem checking = Assert.Single(
            snapshot.PendingItems,
            static item => item.SlotId == "dp-input");
        Assert.Equal(12, checking.KnownInputLength);
        Assert.Equal(MemoryLayoutPrerequisite.CompleteInspection, checking.Prerequisite);
        Assert.Equal(MemoryLayoutNextAction.WaitForInspection, checking.NextAction);
        Assert.All(
            snapshot.AfterSegments,
            static segment => Assert.Empty(segment.ContributingOperations));

        ActiveSessionSnapshot selectedSession = CreateSession(
            fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Selected, length: 12),
            Slot("tp-input", AuthoringSlotLifecycle.Empty));
        MemoryLayoutPendingItem selected = Assert.Single(
            MemoryLayoutProjector.Project(
                    fixture.Capability,
                    selectedSession,
                    compiledOverlay: null)
                .PendingItems,
            static item => item.SlotId == "dp-input");
        Assert.Equal(MemoryLayoutNextAction.RunInspection, selected.NextAction);
        Assert.Equal(MemoryLayoutReadiness.PendingInput, selected.Readiness);
        Assert.Null(selected.BlockedIssue);
    }

    /// <summary>Rejects stale authoring identity and separately compiled look-alike artifacts.</summary>
    [Fact]
    public void ProjectionRejectsStaleSessionAndRecompiledOverlay()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge);
        ActiveSessionSnapshot stale = CreateSession(
            fixture,
            capabilityFingerprint:
                "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
            Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("tp-input", AuthoringSlotLifecycle.Verified, Capacity));

        _ = Assert.Throws<ArgumentException>(() =>
            MemoryLayoutProjector.Project(fixture.Capability, stale, fixture.Composition));

        ProjectionFixture other = CreateFixture(CompositionKind.Merge);
        ActiveSessionSnapshot current = CreateSession(
            fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("tp-input", AuthoringSlotLifecycle.Verified, Capacity));
        _ = Assert.Throws<ArgumentException>(() =>
            MemoryLayoutProjector.Project(
                fixture.Capability,
                current,
                other.Composition));
    }

    /// <summary>TP Overview labels cannot redefine canonical CtrlRAM geometry or grouping identity.</summary>
    [Fact]
    public void CtrlRamDisplayGroupingBindsByExactCanonicalRange()
    {
        ProjectionFixture fixture = CreateFixture(
            CompositionKind.Replace,
            ctrlRamMap: true);
        ActiveSessionSnapshot session = CreateSession(
            fixture,
            Slot("reference-base", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("dp-replacement", AuthoringSlotLifecycle.Verified, Capacity));
        var display = new CtrlRamRegion(
            "tp-overview-display-id",
            "Slave Right CtrlRAM",
            fixture.TpRegion.Range.Start,
            fixture.TpRegion.Range.Length,
            IsMultiChipOnly: true,
            ReplaceRegionGroup.SlaveRight,
            CtrlRamRegionRole.Vn);

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            fixture.Composition,
            [display]);

        MemoryLayoutSegment segment = Assert.Single(
            snapshot.AfterSegments,
            candidate => ReferenceEquals(candidate.CanonicalRegion, fixture.TpRegion));
        Assert.Equal("tp-code", segment.RegionId);
        Assert.Equal(ReplaceRegionGroup.SlaveRight, segment.RegionGroup);
        Assert.Equal(CtrlRamRegionRole.Vn, segment.CtrlRamRegionRole);

        _ = Assert.Throws<MemoryLayoutDisplayProjectionException>(() =>
        {
            _ = MemoryLayoutProjector.Project(
                fixture.Capability,
                session,
                fixture.Composition,
                [display, display with { RegionId = "duplicate-display-id" }]);
        });
        _ = Assert.Throws<MemoryLayoutDisplayProjectionException>(() =>
        {
            _ = MemoryLayoutProjector.Project(
                fixture.Capability,
                session,
                fixture.Composition,
                [display with { Start = display.Start + 1 }]);
        });
    }
}
