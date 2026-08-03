using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>Verifies explicit selected-file inspection/rebind session transitions.</summary>
public sealed class GeneralSelectedFileSessionLifecycleTests
{
    /// <summary>One completion retains every terminal severity and publishes the batch once.</summary>
    [Fact]
    public void BatchInspectionRetainsTerminalHealthAtOneRevision()
    {
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        AuthoringCapabilityCatalogSnapshot catalog = ActivateExact(
            session,
            "dp-input",
            "tp-input",
            "ldc-input");

        AuthoringSlotInspectionBatchStartResult started = session.BeginSlotFileInspections(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dp-input"] = @"C:\firmware\dp.bin",
                ["tp-input"] = @"C:\firmware\tp.bin",
                ["ldc-input"] = @"C:\firmware\ldc.bin",
            });

        Assert.True(started.Succeeded, started.Issue?.Message);
        Assert.Equal(3, started.Leases.Count);
        Assert.All(started.Leases, lease =>
            Assert.Equal(started.Snapshot!.AuthoringRevision, lease.AuthoringRevision));
        AuthoringSessionTransitionResult completed = session.TryCompleteSlotFileInspectionBatch(
            catalog,
            started.Leases,
            new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal)
            {
                ["dp-input"] = Status(
                    catalog,
                    started.Snapshot!.AuthoringRevision,
                    "dp-input",
                    @"C:\firmware\dp.bin",
                    AuthoringSlotLifecycle.Verified,
                    1),
                ["tp-input"] = Status(
                    catalog,
                    started.Snapshot.AuthoringRevision,
                    "tp-input",
                    @"C:\firmware\tp.bin",
                    AuthoringSlotLifecycle.Warning,
                    2),
                ["ldc-input"] = Status(
                    catalog,
                    started.Snapshot.AuthoringRevision,
                    "ldc-input",
                    @"C:\firmware\ldc.bin",
                    AuthoringSlotLifecycle.Error,
                    3),
            });

        Assert.True(completed.Succeeded, completed.Issue?.Message);
        var lifecycles = completed.Snapshot!.Slots.ToDictionary(
            static slot => slot.DefinitionId,
            static slot => slot.Lifecycle,
            StringComparer.Ordinal);
        Assert.Equal(AuthoringSlotLifecycle.Verified, lifecycles["dp-input"]);
        Assert.Equal(AuthoringSlotLifecycle.Warning, lifecycles["tp-input"]);
        Assert.Equal(AuthoringSlotLifecycle.Error, lifecycles["ldc-input"]);
        Assert.Equal(3, completed.Snapshot.InputSlotStatuses.Count);
        _ = Assert.Single(completed.Snapshot.DerivedPublications);
    }

    /// <summary>One stale member rejects the whole completion without partial slot publication.</summary>
    [Fact]
    public void BatchInspectionRejectsOneStaleLeaseWithoutPartialAcceptance()
    {
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        AuthoringCapabilityCatalogSnapshot catalog = ActivateExact(
            session,
            "dp-input",
            "tp-input");
        AuthoringSlotInspectionBatchStartResult stale = session.BeginSlotFileInspections(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dp-input"] = @"C:\firmware\dp.bin",
                ["tp-input"] = @"C:\firmware\tp.bin",
            });
        AuthoringSlotInspectionStartResult current = session.BeginSlotFileInspection(
            "dp-input",
            @"C:\firmware\replacement-dp.bin");
        ActiveSessionSnapshot beforeCompletion = session.CurrentSnapshot!;

        AuthoringSessionTransitionResult result = session.TryCompleteSlotFileInspectionBatch(
            catalog,
            stale.Leases,
            stale.Leases.ToDictionary(
                static lease => lease.DefinitionId,
                lease => Status(
                    catalog,
                    lease.AuthoringRevision,
                    lease.DefinitionId,
                    lease.SelectedPath,
                    AuthoringSlotLifecycle.Verified,
                    1),
                StringComparer.Ordinal));

        Assert.False(result.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.StaleInspection, result.Issue!.Code);
        Assert.Same(beforeCompletion, session.CurrentSnapshot);
        Assert.Equal(current.Snapshot!.AuthoringRevision, session.CurrentSnapshot!.AuthoringRevision);
        Assert.All(session.CurrentSnapshot.Slots, static slot => Assert.Null(slot.FileStamp));
        Assert.Empty(session.CurrentSnapshot.InputSlotStatuses);
        Assert.Empty(session.CurrentSnapshot.DerivedPublications);
    }

    /// <summary>A self-consistent foreign catalog cannot replace any current publication identity.</summary>
    [Theory]
    [InlineData("resolution-token")]
    [InlineData("route")]
    [InlineData("capability")]
    [InlineData("compilation")]
    public void BatchInspectionRejectsForeignCatalogIdentityWithoutPartialAcceptance(
        string changedIdentity)
    {
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        _ = ActivateExact(session, "dp-input", "tp-input");
        AuthoringSlotInspectionBatchStartResult started = session.BeginSlotFileInspections(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dp-input"] = @"C:\firmware\dp.bin",
                ["tp-input"] = @"C:\firmware\tp.bin",
            });
        AuthoringCapabilityCatalogSnapshot foreign = ExactCatalog(
            session.WorkflowId,
            ["dp-input", "tp-input"],
            changedIdentity == "resolution-token" ? "foreign-token" : "exact-token",
            changedIdentity == "route" ? "foreign-map" : "exact-map",
            changedIdentity == "capability" ? new string('b', 64) : new string('a', 64),
            changedIdentity == "compilation" ? new string('2', 64) : new string('1', 64));
        ActiveSessionSnapshot beforeCompletion = session.CurrentSnapshot!;

        AuthoringSessionTransitionResult result = session.TryCompleteSlotFileInspectionBatch(
            foreign,
            started.Leases,
            started.Leases.ToDictionary(
                static lease => lease.DefinitionId,
                lease => Status(
                    foreign,
                    lease.AuthoringRevision,
                    lease.DefinitionId,
                    lease.SelectedPath,
                    AuthoringSlotLifecycle.Verified,
                    1),
                StringComparer.Ordinal));

        AssertStaleBatchWithoutPartialAcceptance(session, beforeCompletion, result);
    }

    /// <summary>Dictionary keys cannot disguise statuses for different slot definitions.</summary>
    [Fact]
    public void BatchInspectionRejectsSwappedStatusSlotIdsWithoutPartialAcceptance()
    {
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        AuthoringCapabilityCatalogSnapshot catalog = ActivateExact(
            session,
            "dp-input",
            "tp-input");
        AuthoringSlotInspectionBatchStartResult started = session.BeginSlotFileInspections(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dp-input"] = @"C:\firmware\dp.bin",
                ["tp-input"] = @"C:\firmware\tp.bin",
            });
        ActiveSessionSnapshot beforeCompletion = session.CurrentSnapshot!;

        AuthoringSessionTransitionResult result = session.TryCompleteSlotFileInspectionBatch(
            catalog,
            started.Leases,
            new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal)
            {
                ["dp-input"] = Status(
                    catalog,
                    started.Snapshot!.AuthoringRevision,
                    "tp-input",
                    @"C:\firmware\dp.bin",
                    AuthoringSlotLifecycle.Verified,
                    1),
                ["tp-input"] = Status(
                    catalog,
                    started.Snapshot.AuthoringRevision,
                    "dp-input",
                    @"C:\firmware\tp.bin",
                    AuthoringSlotLifecycle.Verified,
                    2),
            });

        AssertStaleBatchWithoutPartialAcceptance(session, beforeCompletion, result);
    }

    /// <summary>A completion must contain every Checking member started at the current revision.</summary>
    [Fact]
    public void BatchInspectionRejectsPartialCheckingSetWithoutPublishingCompletion()
    {
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        AuthoringCapabilityCatalogSnapshot catalog = ActivateExact(
            session,
            "dp-input",
            "tp-input");
        AuthoringSlotInspectionBatchStartResult started = session.BeginSlotFileInspections(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dp-input"] = @"C:\firmware\dp.bin",
                ["tp-input"] = @"C:\firmware\tp.bin",
            });
        AuthoringSlotInspectionLease dpLease = started.Leases.Single(static lease =>
            lease.DefinitionId == "dp-input");
        ActiveSessionSnapshot beforeCompletion = session.CurrentSnapshot!;

        AuthoringSessionTransitionResult result = session.TryCompleteSlotFileInspectionBatch(
            catalog,
            [dpLease],
            new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal)
            {
                [dpLease.DefinitionId] = Status(
                    catalog,
                    dpLease.AuthoringRevision,
                    dpLease.DefinitionId,
                    dpLease.SelectedPath,
                    AuthoringSlotLifecycle.Verified,
                    1),
            });

        AssertStaleBatchWithoutPartialAcceptance(session, beforeCompletion, result);
    }

    /// <summary>Caller enumeration order cannot change one atomic batch result.</summary>
    [Fact]
    public void BatchInspectionCompletionIsOrderIndependent()
    {
        ActiveSessionSnapshot forward = CompleteExactBatch(reverse: false);
        ActiveSessionSnapshot reverse = CompleteExactBatch(reverse: true);

        Assert.Equal(
            forward.Slots.Select(static slot => (slot.DefinitionId, slot.Lifecycle, slot.FileStamp)),
            reverse.Slots.Select(static slot => (slot.DefinitionId, slot.Lifecycle, slot.FileStamp)));
        Assert.Equal(
            forward.InputSlotStatuses.Select(static status =>
                (status.SlotId, status.InspectionLifecycle, status.FileStamp)),
            reverse.InputSlotStatuses.Select(static status =>
                (status.SlotId, status.InspectionLifecycle, status.FileStamp)));
        Assert.Equal(forward.DerivedPublications, reverse.DerivedPublications);
    }

    /// <summary>An exact compilation change rebuilds slots without changing reviewed capability identity.</summary>
    [Fact]
    public void CompilationChangeInvalidatesTerminalStateWithinOneCapability()
    {
        var identity = new CapabilityRouteIdentity(
            "NT51928",
            ExperienceIds.StandardMerge,
            "selector-free",
            "dual-capacity");
        string capabilityFingerprint = new('a', 64);
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        var firstRoute = new AuthoringCapabilityRoute(
            identity,
            capabilityFingerprint,
            executionAdmitted: true,
            [new AuthoringSlotDefinitionReference("dp-input")],
            new string('1', 64));
        Assert.True(session.Activate(new AuthoringCapabilityCatalogSnapshot(
            session.WorkflowId,
            new ResolutionToken("catalog-token"),
            [firstRoute])).Succeeded);
        AuthoringSlotInspectionStartResult started = session.BeginSlotFileInspection(
            "dp-input",
            @"C:\firmware\dp.bin");
        Assert.True(session.TryAcceptSlotFileInspection(
            started.Lease!,
            new GeneralSelectedFileInspection(
                "dp-input",
                started.Snapshot!.AuthoringRevision,
                @"C:\firmware\dp.bin",
                FileStamp.FromBytes([1, 2, 3, 4]))).Succeeded);
        AuthoringRevision acceptedRevision = session.CurrentSnapshot!.AuthoringRevision;

        var secondRoute = new AuthoringCapabilityRoute(
            identity,
            capabilityFingerprint,
            executionAdmitted: true,
            [
                new AuthoringSlotDefinitionReference("dp-input"),
                new AuthoringSlotDefinitionReference("ldc-input"),
            ],
            new string('2', 64));
        AuthoringSessionTransitionResult changed = session.Activate(
            new AuthoringCapabilityCatalogSnapshot(
                session.WorkflowId,
                new ResolutionToken("catalog-token"),
                [secondRoute]));

        Assert.True(changed.Succeeded, changed.Issue?.Message);
        Assert.Equal(acceptedRevision.Next(), changed.Snapshot!.AuthoringRevision);
        Assert.Equal(secondRoute.CompilationFingerprint, changed.Snapshot.CompilationFingerprint);
        Assert.Equal(AuthoringSlotLifecycle.Selected, changed.Snapshot.Slots.Single(
            static slot => slot.DefinitionId == "dp-input").Lifecycle);
        Assert.Equal(AuthoringSlotLifecycle.Empty, changed.Snapshot.Slots.Single(
            static slot => slot.DefinitionId == "ldc-input").Lifecycle);
        Assert.Empty(changed.Snapshot.DerivedPublications);
    }

    /// <summary>
    /// Rebind invalidates derived state, rejects stale inspection, advances the
    /// authoring revision, and preserves the editable mapping draft.
    /// </summary>
    [Fact]
    public void ExplicitRebindPreservesDraftAndAcceptsOnlyCurrentContentStamp()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        Activate(session);
        GeneralMergeDraftState draft = Draft(length: 4);
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
            Assert.IsType<GeneralMergeDraftState>(
                reload.Snapshot.DraftState).Mappings.Rows);
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
            Assert.IsType<GeneralMergeDraftState>(
                accepted.Snapshot.DraftState).Mappings.Rows);
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
            Assert.IsType<GeneralMergeDraftState>(
                session.CurrentSnapshot!.DraftState).Mappings.Rows);
        Assert.Equal("different-mapping", unchanged.MappingId);
        Assert.Null(unchanged.Source.AcceptedFileStamp);
    }

    /// <summary>Non-General typed drafts retain their existing slot-only lifecycle.</summary>
    [Fact]
    public void InspectionPreservesOtherTypedDraftContracts()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralReplace);
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

    private static ActiveSessionSnapshot CompleteExactBatch(bool reverse)
    {
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        AuthoringCapabilityCatalogSnapshot catalog = ActivateExact(
            session,
            "dp-input",
            "tp-input");
        KeyValuePair<string, string>[] selections =
        [
            new("dp-input", @"C:\firmware\dp.bin"),
            new("tp-input", @"C:\firmware\tp.bin"),
        ];
        if (reverse)
        {
            Array.Reverse(selections);
        }

        AuthoringSlotInspectionBatchStartResult started = session.BeginSlotFileInspections(
            selections.ToDictionary(
                static selection => selection.Key,
                static selection => selection.Value,
                StringComparer.Ordinal));
        KeyValuePair<string, AuthoringInputSlotStatus>[] statuses =
        [
            new(
                "dp-input",
                Status(
                    catalog,
                    started.Snapshot!.AuthoringRevision,
                    "dp-input",
                    @"C:\firmware\dp.bin",
                    AuthoringSlotLifecycle.Verified,
                    1)),
            new(
                "tp-input",
                Status(
                    catalog,
                    started.Snapshot!.AuthoringRevision,
                    "tp-input",
                    @"C:\firmware\tp.bin",
                    AuthoringSlotLifecycle.Warning,
                    2)),
        ];
        if (reverse)
        {
            Array.Reverse(statuses);
        }

        AuthoringSessionTransitionResult result = session.TryCompleteSlotFileInspectionBatch(
            catalog,
            started.Leases,
            statuses.ToDictionary(
                static status => status.Key,
                static status => status.Value,
                StringComparer.Ordinal));
        Assert.True(result.Succeeded, result.Issue?.Message);
        return result.Snapshot!;
    }

    private static AuthoringInputSlotStatus Status(
        AuthoringCapabilityCatalogSnapshot catalog,
        AuthoringRevision revision,
        string slotId,
        string selectedPath,
        AuthoringSlotLifecycle lifecycle,
        byte content)
    {
        AuthoringCapabilityRoute route = Assert.Single(catalog.Routes);
        return new AuthoringInputSlotStatus(
            route.Identity,
            catalog.ResolutionToken,
            revision,
            route.CapabilityFingerprint,
            route.CompilationFingerprint,
            new InputSelectionMemberReadiness(
                slotId,
                IsSelected: true,
                ResolvedChildReadiness.Ready,
                CanSelect: true,
                Reason: null,
                NextAction: null),
            slotId,
            lifecycle,
            FileStamp.FromBytes([content]),
            inspection: null,
            selectedPath);
    }

    private static void AssertStaleBatchWithoutPartialAcceptance(
        AuthoringSessionState session,
        ActiveSessionSnapshot beforeCompletion,
        AuthoringSessionTransitionResult result)
    {
        Assert.False(result.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.StaleInspection, result.Issue!.Code);
        Assert.Same(beforeCompletion, session.CurrentSnapshot);
        Assert.All(session.CurrentSnapshot!.Slots, static slot =>
            Assert.Equal(AuthoringSlotLifecycle.Checking, slot.Lifecycle));
        Assert.Empty(session.CurrentSnapshot.InputSlotStatuses);
        Assert.Empty(session.CurrentSnapshot.DerivedPublications);
    }

    private static AuthoringCapabilityCatalogSnapshot ActivateExact(
        AuthoringSessionState session,
        params string[] slotDefinitionIds)
    {
        AuthoringCapabilityCatalogSnapshot catalog = ExactCatalog(
            session.WorkflowId,
            slotDefinitionIds,
            "exact-token",
            "exact-map",
            new string('a', 64),
            new string('1', 64));
        AuthoringSessionTransitionResult activated = session.Activate(catalog);
        Assert.True(activated.Succeeded, activated.Issue?.Message);
        return catalog;
    }

    private static AuthoringCapabilityCatalogSnapshot ExactCatalog(
        string workflowId,
        IEnumerable<string> slotDefinitionIds,
        string resolutionToken,
        string mapVariant,
        string capabilityFingerprint,
        string compilationFingerprint)
    {
        var route = new AuthoringCapabilityRoute(
            new CapabilityRouteIdentity(
                "NT51926",
                workflowId,
                "selector-free",
                mapVariant),
            capabilityFingerprint,
            executionAdmitted: true,
            slotDefinitionIds.Select(static definitionId =>
                new AuthoringSlotDefinitionReference(definitionId)),
            compilationFingerprint);
        return new AuthoringCapabilityCatalogSnapshot(
            workflowId,
            new ResolutionToken(resolutionToken),
            [route]);
    }

    private static void Activate(
        AuthoringSessionState session,
        params string[] slotDefinitionIds)
    {
        string[] definitions = slotDefinitionIds.Length == 0
            ? ["mapping-1"]
            : slotDefinitionIds;
        var route = new AuthoringCapabilityRoute(
            new CapabilityRouteIdentity(
                "NT51926",
                session.WorkflowId,
                "selector-free",
                "general-map"),
            "general-fingerprint",
            executionAdmitted: true,
            definitions.Select(static definitionId =>
                new AuthoringSlotDefinitionReference(definitionId)));
        AuthoringSessionTransitionResult result = session.Activate(
            new AuthoringCapabilityCatalogSnapshot(
                session.WorkflowId,
                new ResolutionToken("general-token"),
                [route]));
        Assert.True(result.Succeeded, result.Issue?.Message);
    }

    private static GeneralMergeDraftState Draft(
        long length,
        string mappingId = "mapping-1")
    {
        return new GeneralMergeDraftState(
            new GeneralMergeOutputInitializer(0x200, 0),
            new GeneralMappingDraftState(
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
            ]));
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
