using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>Verifies explicit selected-file inspection/rebind session transitions.</summary>
public sealed class GeneralSelectedFileSessionLifecycleTests
{
    /// <summary>
    /// Rebind invalidates derived state, rejects stale inspection, advances the
    /// authoring revision, and preserves the editable mapping draft.
    /// </summary>
    [Fact]
    public void ExplicitRebindPreservesDraftAndAcceptsOnlyCurrentContentStamp()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        Activate(session);
        GeneralMappingDraftState draft = Draft(length: 4);
        Assert.True(session.SetDraft(draft).Succeeded);

        AuthoringSlotInspectionStartResult firstStart =
            session.BeginSlotFileInspection("source", @"C:\firmware\source.bin");
        Assert.True(firstStart.Succeeded);
        var firstStamp = FileStamp.FromBytes([1, 2, 3, 4]);
        Assert.True(session.TryAcceptSlotFileInspection(
            firstStart.Lease!,
            new GeneralSelectedFileInspection(
                "source",
                firstStart.Snapshot!.AuthoringRevision,
                @"C:\firmware\source.bin",
                firstStamp,
                "source.bin",
                DateTimeOffset.UnixEpoch)).Succeeded);
        AuthoringPublicationLease previewLease =
            session.CapturePublicationLease(AuthoringDerivedResultKind.Preview);
        Assert.True(session.TryPublish(
            previewLease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Preview,
                "preview-1")).Succeeded);
        AuthoringRevision acceptedRevision =
            session.CurrentSnapshot!.AuthoringRevision;

        AuthoringSlotInspectionStartResult reload =
            session.BeginSlotFileInspection("source", @"C:\firmware\source.bin");

        Assert.True(reload.Succeeded);
        Assert.Equal(acceptedRevision.Next(), reload.Snapshot!.AuthoringRevision);
        Assert.Empty(reload.Snapshot.DerivedPublications);
        Assert.Same(draft, reload.Snapshot.DraftState);
        Assert.Equal(
            AuthoringSlotLifecycle.Checking,
            Assert.Single(reload.Snapshot.Slots).Lifecycle);
        Assert.Null(Assert.Single(reload.Snapshot.Slots).FileStamp);

        AuthoringSessionTransitionResult stale =
            session.TryAcceptSlotFileInspection(
                firstStart.Lease!,
                new GeneralSelectedFileInspection(
                    "source",
                    firstStart.Snapshot.AuthoringRevision,
                    @"C:\firmware\source.bin",
                    firstStamp));
        Assert.False(stale.Succeeded);
        Assert.Equal(
            AuthoringSessionIssueCodes.StaleInspection,
            stale.Issue!.Code);

        var mutatedStamp = FileStamp.FromBytes([1, 2, 9, 4]);
        AuthoringSessionTransitionResult accepted =
            session.TryAcceptSlotFileInspection(
                reload.Lease!,
                new GeneralSelectedFileInspection(
                    "source",
                    reload.Snapshot.AuthoringRevision,
                    @"C:\firmware\source.bin",
                    mutatedStamp));

        Assert.True(accepted.Succeeded);
        Assert.Equal(mutatedStamp, Assert.Single(accepted.Snapshot!.Slots).FileStamp);
        Assert.Same(draft, accepted.Snapshot.DraftState);
        Assert.Equal(4, Assert.Single(
            Assert.IsType<GeneralMappingDraftState>(accepted.Snapshot.DraftState).Rows)
            .SourceRange.Length);
    }

    private static void Activate(AuthoringSessionState session)
    {
        var route = new AuthoringCapabilityRoute(
            new CapabilityRouteIdentity(
                "NT51926",
                ExperienceIds.GeneralMerge,
                "selector-free",
                "general-map"),
            "general-fingerprint",
            executionAdmitted: true,
            [new AuthoringSlotDefinitionReference("source")]);
        AuthoringSessionTransitionResult result = session.Activate(
            new AuthoringCapabilityCatalogSnapshot(
                ExperienceIds.GeneralMerge,
                new ResolutionToken("general-token"),
                [route]));
        Assert.True(result.Succeeded, result.Issue?.Message);
    }

    private static GeneralMappingDraftState Draft(long length)
    {
        return new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(
                "mapping-1",
                ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(@"C:\firmware\source.bin"),
                new ByteRange(0, length),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(0x100, length),
                OverlapPolicy.Reject,
                alignment: 1,
                "Copy selected General file."),
        ]);
    }
}
