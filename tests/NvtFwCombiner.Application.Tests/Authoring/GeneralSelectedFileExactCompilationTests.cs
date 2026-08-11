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
