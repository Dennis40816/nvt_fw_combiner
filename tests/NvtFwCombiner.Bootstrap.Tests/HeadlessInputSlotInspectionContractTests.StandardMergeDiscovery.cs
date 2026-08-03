using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class HeadlessInputSlotInspectionContractTests
{
    /// <summary>Every reviewed NT51950 capacity may resolve atomically from one discovery lease.</summary>
    [Theory]
    [InlineData(0x40000, "nt51950-standard-merge-256k")]
    [InlineData(0x80000, "nt51950-standard-merge-512k")]
    [InlineData(0x100000, "nt51950-standard-merge-1024k")]
    public void Nt51950DiscoveryLeaseResolvesOnlyReviewedExactCapacity(
        int dpLength,
        string expectedMapVariant)
    {
        ReloadCatalog();
        const string path = "dp.bin";
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        WorkbenchStandardMergeAuthoringSnapshot discovery =
            WorkbenchCompositionService.GetStandardMergeAuthoringSnapshot(
                "NT51950",
                [CompositionAddressSpaceIds.DpInput],
                new Dictionary<string, FileStamp>(StringComparer.Ordinal),
                new AuthoringRevision(1));
        AuthoringSessionTransitionResult activated = session.Activate(discovery.Catalog);
        Assert.True(activated.Succeeded, activated.Issue?.Message);
        ReviewedDiscoveryTransition proof = Assert.IsType<ReviewedDiscoveryTransition>(
            Assert.Single(discovery.Catalog.Routes).DiscoveryTransition);
        Assert.Equal(3, proof.AllowedExactMembers.Count);
        AuthoringSlotInspectionStartResult started = session.BeginSlotFileInspection(
            CompositionAddressSpaceIds.DpInput,
            path);
        Assert.True(started.Succeeded, started.Issue?.Message);

        WorkbenchFirmwareInspection inspection = Assert.Single(
            WorkbenchCompositionService.InspectFirmwareBatch(
                "NT51950",
                [new WorkbenchFirmwareInspectionInput(
                    "dp",
                    path,
                    AuthoringRevision: started.Snapshot!.AuthoringRevision.Value,
                    StandardMergeAddressSpaceId: CompositionAddressSpaceIds.DpInput)],
                _ => new byte[dpLength])).Inspection;
        AuthoringCapabilityCatalogSnapshot exactCatalog =
            Assert.IsType<AuthoringCapabilityCatalogSnapshot>(inspection.InputSlotCatalog);
        AuthoringInputSlotStatus status =
            Assert.IsType<AuthoringInputSlotStatus>(inspection.InputSlotStatus);
        AuthoringRevision completionRevision = started.Snapshot.AuthoringRevision;

        AuthoringSessionTransitionResult completed = session.TryCompleteSlotFileInspectionBatch(
            exactCatalog,
            [started.Lease!],
            new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal)
            {
                [status.SlotId] = status,
            });

        Assert.True(completed.Succeeded, completed.Issue?.Message);
        Assert.Equal(completionRevision, completed.Snapshot!.AuthoringRevision);
        Assert.Equal(expectedMapVariant, completed.Snapshot.SelectedMapVariant);
        Assert.NotNull(completed.Snapshot.CompilationFingerprint);
        Assert.True(status.IsTerminal);
    }

    /// <summary>A same-publication catalog still cannot invent a route or alter a reviewed capability.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Nt51950DiscoveryLeaseRejectsUnreviewedExactIdentity(bool alterCapability)
    {
        ReloadCatalog();
        const string path = "dp.bin";
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        WorkbenchStandardMergeAuthoringSnapshot discovery =
            WorkbenchCompositionService.GetStandardMergeAuthoringSnapshot(
                "NT51950",
                [CompositionAddressSpaceIds.DpInput],
                new Dictionary<string, FileStamp>(StringComparer.Ordinal),
                new AuthoringRevision(1));
        Assert.True(session.Activate(discovery.Catalog).Succeeded);
        AuthoringSlotInspectionStartResult started = session.BeginSlotFileInspection(
            CompositionAddressSpaceIds.DpInput,
            path);
        ReviewedDiscoveryTransition proof = Assert.IsType<ReviewedDiscoveryTransition>(
            Assert.Single(discovery.Catalog.Routes).DiscoveryTransition);
        WorkbenchFirmwareInspection reviewed = InspectStandardMergeInput(
            "NT51950",
            path,
            started.Snapshot!.AuthoringRevision,
            0x40000);
        AuthoringCapabilityRoute reviewedRoute = Assert.Single(reviewed.InputSlotCatalog!.Routes);
        CapabilityRouteIdentity identity = alterCapability
            ? reviewedRoute.Identity
            : new CapabilityRouteIdentity(
                "NT51950",
                ExperienceIds.StandardMerge,
                "selector-free",
                "forged-map");
        string capabilityFingerprint = alterCapability
            ? new string('b', 64)
            : reviewedRoute.CapabilityFingerprint;
        var forgedRoute = new AuthoringCapabilityRoute(
            identity,
            capabilityFingerprint,
            executionAdmitted: true,
            [new AuthoringSlotDefinitionReference(CompositionAddressSpaceIds.DpInput)],
            reviewedRoute.CompilationFingerprint,
            proof);
        var forgedCatalog = new AuthoringCapabilityCatalogSnapshot(
            ExperienceIds.StandardMerge,
            discovery.Catalog.ResolutionToken,
            [forgedRoute]);
        AuthoringInputSlotStatus forgedStatus = StatusForRoute(
            forgedCatalog,
            forgedRoute,
            started.Snapshot.AuthoringRevision,
            path);
        ActiveSessionSnapshot beforeCompletion = session.CurrentSnapshot!;

        AuthoringSessionTransitionResult completed = session.TryCompleteSlotFileInspectionBatch(
            forgedCatalog,
            [started.Lease!],
            new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal)
            {
                [forgedStatus.SlotId] = forgedStatus,
            });

        AssertStaleWithoutPublication(session, beforeCompletion, completed);
    }

    /// <summary>An exact lease cannot cross to another member of the reviewed discovery set.</summary>
    [Fact]
    public void Nt51950ExactLeaseRejectsAnotherReviewedCapacity()
    {
        ReloadCatalog();
        const string firstPath = "dp-256.bin";
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        WorkbenchStandardMergeAuthoringSnapshot discovery =
            WorkbenchCompositionService.GetStandardMergeAuthoringSnapshot(
                "NT51950",
                [CompositionAddressSpaceIds.DpInput],
                new Dictionary<string, FileStamp>(StringComparer.Ordinal),
                new AuthoringRevision(1));
        Assert.True(session.Activate(discovery.Catalog).Succeeded);
        AuthoringSlotInspectionStartResult discoveryStart = session.BeginSlotFileInspection(
            CompositionAddressSpaceIds.DpInput,
            firstPath);
        WorkbenchFirmwareInspection first = InspectStandardMergeInput(
            "NT51950",
            firstPath,
            discoveryStart.Snapshot!.AuthoringRevision,
            0x40000);
        AuthoringSessionTransitionResult firstCompletion = session.TryCompleteSlotFileInspectionBatch(
            first.InputSlotCatalog!,
            [discoveryStart.Lease!],
            new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal)
            {
                [first.InputSlotStatus!.SlotId] = first.InputSlotStatus,
            });
        Assert.True(firstCompletion.Succeeded, firstCompletion.Issue?.Message);

        const string secondPath = "dp-512.bin";
        AuthoringSlotInspectionStartResult exactStart = session.BeginSlotFileInspection(
            CompositionAddressSpaceIds.DpInput,
            secondPath);
        WorkbenchFirmwareInspection second = InspectStandardMergeInput(
            "NT51950",
            secondPath,
            exactStart.Snapshot!.AuthoringRevision,
            0x80000);
        ActiveSessionSnapshot beforeCompletion = session.CurrentSnapshot!;

        AuthoringSessionTransitionResult secondCompletion = session.TryCompleteSlotFileInspectionBatch(
            second.InputSlotCatalog!,
            [exactStart.Lease!],
            new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal)
            {
                [second.InputSlotStatus!.SlotId] = second.InputSlotStatus,
            });

        AssertStaleWithoutPublication(session, beforeCompletion, secondCompletion);
    }

    /// <summary>A canonical catalog reload invalidates an older discovery transition.</summary>
    [Fact]
    public void Nt51950DiscoveryLeaseRejectsCatalogReload()
    {
        ReloadCatalog();
        const string path = "dp.bin";
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        WorkbenchStandardMergeAuthoringSnapshot discovery =
            WorkbenchCompositionService.GetStandardMergeAuthoringSnapshot(
                "NT51950",
                [CompositionAddressSpaceIds.DpInput],
                new Dictionary<string, FileStamp>(StringComparer.Ordinal),
                new AuthoringRevision(1));
        Assert.True(session.Activate(discovery.Catalog).Succeeded);
        AuthoringSlotInspectionStartResult started = session.BeginSlotFileInspection(
            CompositionAddressSpaceIds.DpInput,
            path);
        ReloadCatalog();
        WorkbenchFirmwareInspection reloaded = InspectStandardMergeInput(
            "NT51950",
            path,
            started.Snapshot!.AuthoringRevision,
            0x40000);
        Assert.NotEqual(
            discovery.Catalog.ResolutionToken,
            reloaded.InputSlotCatalog!.ResolutionToken);
        ActiveSessionSnapshot beforeCompletion = session.CurrentSnapshot!;

        AuthoringSessionTransitionResult completed = session.TryCompleteSlotFileInspectionBatch(
            reloaded.InputSlotCatalog,
            [started.Lease!],
            new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal)
            {
                [reloaded.InputSlotStatus!.SlotId] = reloaded.InputSlotStatus,
            });

        AssertStaleWithoutPublication(session, beforeCompletion, completed);
    }
}
