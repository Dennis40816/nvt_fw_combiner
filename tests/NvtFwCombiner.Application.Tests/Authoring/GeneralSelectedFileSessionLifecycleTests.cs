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
            session.BeginSlotFileInspection("mapping-1", @"C:\firmware\source.bin");
        Assert.True(firstStart.Succeeded);
        var firstStamp = FileStamp.FromBytes([1, 2, 3, 4]);
        Assert.True(session.TryAcceptSlotFileInspection(
            firstStart.Lease!,
            new GeneralSelectedFileInspection(
                "mapping-1",
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
            session.BeginSlotFileInspection("mapping-1", @"C:\firmware\replacement.bin");

        Assert.True(reload.Succeeded);
        Assert.Equal(acceptedRevision.Next(), reload.Snapshot!.AuthoringRevision);
        Assert.Empty(reload.Snapshot.DerivedPublications);
        GeneralMappingDraftRow pendingRow = Assert.Single(
            Assert.IsType<GeneralMappingDraftState>(reload.Snapshot.DraftState).Rows);
        Assert.Null(pendingRow.Source.AcceptedFileStamp);
        Assert.Equal(@"C:\firmware\replacement.bin", pendingRow.Source.Reference);
        Assert.Equal(4, pendingRow.SourceRange.Length);
        Assert.Equal(
            AuthoringSlotLifecycle.Checking,
            Assert.Single(reload.Snapshot.Slots).Lifecycle);
        Assert.Null(Assert.Single(reload.Snapshot.Slots).FileStamp);

        AuthoringSessionTransitionResult stale =
            session.TryAcceptSlotFileInspection(
                firstStart.Lease!,
                new GeneralSelectedFileInspection(
                    "mapping-1",
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
                    "mapping-1",
                    reload.Snapshot.AuthoringRevision,
                    @"C:\firmware\replacement.bin",
                    mutatedStamp));

        Assert.True(accepted.Succeeded);
        Assert.Equal(mutatedStamp, Assert.Single(accepted.Snapshot!.Slots).FileStamp);
        GeneralMappingDraftRow acceptedRow = Assert.Single(
            Assert.IsType<GeneralMappingDraftState>(accepted.Snapshot.DraftState).Rows);
        Assert.Equal(mutatedStamp, acceptedRow.Source.AcceptedFileStamp);
        Assert.Equal(@"C:\firmware\replacement.bin", acceptedRow.Source.Reference);
        Assert.Equal(4, acceptedRow.SourceRange.Length);
    }

    /// <summary>A slot result cannot publish into a General draft with a different definition.</summary>
    [Fact]
    public void InspectionRejectsGeneralDraftDefinitionMismatch()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        Activate(session);
        Assert.True(session.SetDraft(
            Draft(length: 4, mappingId: "different-mapping")).Succeeded);
        AuthoringSlotInspectionStartResult start =
            session.BeginSlotFileInspection(
                "mapping-1",
                @"C:\firmware\source.bin");

        AuthoringSessionTransitionResult result =
            session.TryAcceptSlotFileInspection(
                start.Lease!,
                new GeneralSelectedFileInspection(
                    "mapping-1",
                    start.Snapshot!.AuthoringRevision,
                    @"C:\firmware\source.bin",
                    FileStamp.FromBytes([1, 2, 3, 4])));

        Assert.False(result.Succeeded);
        Assert.Equal(
            AuthoringSessionIssueCodes.StaleInspection,
            result.Issue!.Code);
        GeneralMappingDraftRow unchanged = Assert.Single(
            Assert.IsType<GeneralMappingDraftState>(
                session.CurrentSnapshot!.DraftState).Rows);
        Assert.Equal("different-mapping", unchanged.MappingId);
        Assert.Null(unchanged.Source.AcceptedFileStamp);
    }

    /// <summary>Non-General typed drafts retain their existing slot-only lifecycle.</summary>
    [Fact]
    public void InspectionPreservesOtherTypedDraftContracts()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        Activate(session);
        var draft = new TestDraftState("draft");
        Assert.True(session.SetDraft(draft).Succeeded);
        AuthoringSlotInspectionStartResult start =
            session.BeginSlotFileInspection(
                "mapping-1",
                @"C:\firmware\source.bin");

        AuthoringSessionTransitionResult result =
            session.TryAcceptSlotFileInspection(
                start.Lease!,
                new GeneralSelectedFileInspection(
                    "mapping-1",
                    start.Snapshot!.AuthoringRevision,
                    @"C:\firmware\source.bin",
                    FileStamp.FromBytes([1, 2, 3, 4])));

        Assert.True(result.Succeeded);
        Assert.Same(draft, result.Snapshot!.DraftState);
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
            [new AuthoringSlotDefinitionReference("mapping-1")]);
        AuthoringSessionTransitionResult result = session.Activate(
            new AuthoringCapabilityCatalogSnapshot(
                ExperienceIds.GeneralMerge,
                new ResolutionToken("general-token"),
                [route]));
        Assert.True(result.Succeeded, result.Issue?.Message);
    }

    private static GeneralMappingDraftState Draft(
        long length,
        string mappingId = "mapping-1")
    {
        return new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(
                mappingId,
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

    private sealed record TestDraftState(string Value)
        : AuthoringDraftState(AuthoringDraftKind.GeneralMapping)
    {
        internal override AuthoringDraftState CreateImmutableSnapshot()
        {
            return this;
        }
    }
}
