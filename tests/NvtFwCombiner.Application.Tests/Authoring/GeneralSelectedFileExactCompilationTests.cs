using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>Verifies General terminal health remains exact-compilation-bound.</summary>
public sealed class GeneralSelectedFileExactCompilationTests
{
    /// <summary>Typed failures publish Error only against the current exact compilation.</summary>
    [Fact]
    public void ExactInspectionFailurePublishesTypedError()
    {
        (AuthoringSessionState session, AuthoringCapabilityRoute route) = CreateExactSession();
        AuthoringSlotInspectionStartResult started = session.BeginSlotFileInspection(
            "mapping-1",
            @"C:\firmware\source.bin");

        AuthoringSessionTransitionResult rejected = session.TryRejectSlotFileInspection(
            started.Lease!,
            new GeneralSelectedFileInspectionIssue(
                GeneralSelectedFileInspectionIssueCodes.InspectionFailed,
                "Unreadable.",
                "mapping-1"));

        Assert.True(rejected.Succeeded);
        AuthoringSlotState slot = Assert.Single(rejected.Snapshot!.Slots);
        Assert.Equal(AuthoringSlotLifecycle.Error, slot.Lifecycle);
        Assert.Equal(GeneralSelectedFileInspectionIssueCodes.InspectionFailed, slot.BlockingIssue!.IssueId);
        Assert.Equal(route.CompilationFingerprint, Assert.Single(rejected.Snapshot.DerivedPublications).CompilationFingerprint);
    }

    /// <summary>An accepted hash cannot cross the exact candidate length compiled before Checking.</summary>
    [Fact]
    public void ExactInspectionRejectsObservedLengthChange()
    {
        (AuthoringSessionState session, _) = CreateExactSession();
        AuthoringSlotInspectionStartResult started = session.BeginSlotFileInspection(
            "mapping-1",
            @"C:\firmware\source.bin");

        AuthoringSessionTransitionResult result = session.TryAcceptSlotFileInspection(
            started.Lease!,
            new GeneralSelectedFileInspection(
                "mapping-1",
                started.Snapshot!.AuthoringRevision,
                @"C:\firmware\source.bin",
                FileStamp.FromBytes([1, 2])));

        Assert.False(result.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.StaleInspection, result.Issue!.Code);
        Assert.Equal(AuthoringSlotLifecycle.Checking, Assert.Single(session.CurrentSnapshot!.Slots).Lifecycle);
    }

    /// <summary>Changing a selected path evicts content cached for the old path.</summary>
    [Fact]
    public void SelectedPathChangeEvictsPrebindingContentCache()
    {
        (AuthoringSessionState session, _) = CreateExactSession();
        const string original = @"C:\firmware\source.bin";
        CacheInspection(session, "mapping-1", original);

        _ = session.SetSlotFile("mapping-1", @"C:\firmware\replacement.bin", fileStamp: null);

        Assert.False(session.TryGetCachedGeneralSelectedFileInspection(
            "mapping-1",
            original,
            out _));
    }

    /// <summary>Removing a route definition evicts cache that cannot belong to the active route.</summary>
    [Fact]
    public void RouteDefinitionChangeEvictsRemovedContentCache()
    {
        (AuthoringSessionState session, AuthoringCapabilityRoute route) = CreateExactSession();
        const string selectedPath = @"C:\firmware\source.bin";
        CacheInspection(session, "mapping-1", selectedPath);
        var replacementRoute = new AuthoringCapabilityRoute(
            route.Identity,
            route.CapabilityFingerprint,
            executionAdmitted: false,
            [new AuthoringSlotDefinitionReference("mapping-2", expectedLength: 4)],
            route.CompilationFingerprint);

        Assert.True(session.Activate(new AuthoringCapabilityCatalogSnapshot(
            session.WorkflowId,
            new ResolutionToken("general-token"),
            [replacementRoute])).Succeeded);
        Assert.True(session.Activate(new AuthoringCapabilityCatalogSnapshot(
            session.WorkflowId,
            new ResolutionToken("general-token"),
            [route])).Succeeded);

        Assert.False(session.TryGetCachedGeneralSelectedFileInspection(
            "mapping-1",
            selectedPath,
            out _));
    }

    private static void CacheInspection(
        AuthoringSessionState session,
        string definitionId,
        string selectedPath)
    {
        Assert.True(session.SetSlotFile(definitionId, selectedPath, fileStamp: null).Succeeded);
        AuthoringPublicationLease lease = session.CapturePublicationLease(
            AuthoringDerivedResultKind.Inspection);
        Assert.True(session.TryCacheGeneralSelectedFileInspection(
            lease,
            new GeneralSelectedFileInspection(
                definitionId,
                lease.AuthoringRevision,
                selectedPath,
                FileStamp.FromBytes([1, 2, 3, 4]))).Succeeded);
    }

    private static (AuthoringSessionState Session, AuthoringCapabilityRoute Route) CreateExactSession()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        var route = new AuthoringCapabilityRoute(
            new CapabilityRouteIdentity("NT51926", ExperienceIds.GeneralMerge, "not-applicable", "generic"),
            new string('a', 64),
            executionAdmitted: false,
            [new AuthoringSlotDefinitionReference("mapping-1", expectedLength: 4)],
            new string('1', 64));
        Assert.True(session.Activate(new AuthoringCapabilityCatalogSnapshot(
            session.WorkflowId,
            new ResolutionToken("general-token"),
            [route])).Succeeded);
        return (session, route);
    }
}
