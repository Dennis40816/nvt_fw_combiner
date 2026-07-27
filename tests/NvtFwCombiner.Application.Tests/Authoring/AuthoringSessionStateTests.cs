using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>Tests isolated, host-independent authoring-session transitions.</summary>
public sealed class AuthoringSessionStateTests
{
    /// <summary>Entering DP Replace publishes its own catalog without inheriting AB-only state.</summary>
    [Fact]
    public void DpReplaceFirstActivationDoesNotInheritAbMergeSelection()
    {
        var abSession = new AuthoringSessionState("ab-merge");
        ActiveSessionSnapshot abSnapshot = Activate(
            abSession,
            Catalog(
                "ab-merge",
                "ab-token",
                Route(
                    "NT51951",
                    "ab-merge",
                    "cascade",
                    "nt51951-ab-cascade",
                    "ab-fingerprint",
                    "dp-ab",
                    "tp-a",
                    "tp-b")));
        abSnapshot = Select(abSession, "NT51951", "cascade");
        abSnapshot = SetFile(
            abSession,
            "dp-ab",
            @"C:\firmware\dp-ab.bin",
            Stamp(0x100000, 1));

        var dpSession = new AuthoringSessionState("dp-replace");
        ActiveSessionSnapshot dpSnapshot = Activate(
            dpSession,
            Catalog(
                "dp-replace",
                "dp-token",
                Route(
                    "NT51917",
                    "dp-replace",
                    "selector-free",
                    "nt51917-dp-replace",
                    "dp-17-fingerprint",
                    "reference",
                    "dp"),
                Route(
                    "NT51929",
                    "dp-replace",
                    "selector-free",
                    "nt51929-dp-replace",
                    "dp-29-fingerprint",
                    "reference",
                    "dp")));

        Assert.Equal(["NT51917", "NT51929"], dpSnapshot.IcChoices);
        Assert.Equal("NT51917", dpSnapshot.SelectedIc);
        Assert.Equal(["selector-free"], dpSnapshot.IcCountChoices);
        Assert.Equal("selector-free", dpSnapshot.SelectedIcCount);
        Assert.Equal(["dp", "reference"], dpSnapshot.Slots.Select(static slot => slot.DefinitionId));
        Assert.All(dpSnapshot.Slots, static slot => Assert.Null(slot.SelectedPath));
        Assert.Equal(@"C:\firmware\dp-ab.bin", Assert.Single(
            abSnapshot.Slots,
            static slot => slot.DefinitionId == "dp-ab").SelectedPath);
    }

    /// <summary>Selection changes retain only inputs with the same resolved slot definition and fingerprint.</summary>
    [Fact]
    public void SelectionChangePreservesOnlyCompatibleInputsAndInvalidatesDerivedState()
    {
        const string sharedFingerprint = "same-capability-fingerprint";
        var session = new AuthoringSessionState("dp-replace");
        _ = Activate(
            session,
            Catalog(
                "dp-replace",
                "dp-token",
                Route(
                    "NT51950",
                    "dp-replace",
                    "single",
                    "nt51950-single",
                    sharedFingerprint,
                    "reference",
                    "dp",
                    "ldc"),
                Route(
                    "NT51950",
                    "dp-replace",
                    "cascade",
                    "nt51950-cascade",
                    sharedFingerprint,
                    "reference",
                    "dp")));
        _ = Select(session, "NT51950", "single");
        _ = SetFile(session, "dp", @"C:\firmware\dp.bin", Stamp(0x40000, 1));
        ActiveSessionSnapshot before = SetFile(
            session,
            "ldc",
            @"C:\firmware\ldc.bin",
            Stamp(0x22000, 1));
        AuthoringPublicationLease lease = session.CapturePublicationLease(
            AuthoringDerivedResultKind.Validation);
        AuthoringPublicationResult published = session.TryPublish(
            lease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Validation,
                "validation-1"));
        Assert.True(published.Succeeded);
        _ = Assert.Single(session.CurrentSnapshot!.DerivedPublications);

        ActiveSessionSnapshot after = Select(session, "NT51950", "cascade");

        Assert.Equal(before.AuthoringRevision.Next(), after.AuthoringRevision);
        Assert.Equal(@"C:\firmware\dp.bin", Assert.Single(
            after.Slots,
            static slot => slot.DefinitionId == "dp").SelectedPath);
        Assert.DoesNotContain(after.Slots, static slot => slot.DefinitionId == "ldc");
        Assert.Empty(after.DerivedPublications);
    }

    /// <summary>A different capability fingerprint makes same-named slots incompatible.</summary>
    [Fact]
    public void CapabilityChangeClearsSameNamedSlotInputs()
    {
        var session = new AuthoringSessionState("dp-replace");
        _ = Activate(
            session,
            Catalog(
                "dp-replace",
                "dp-token",
                Route(
                    "NT51917",
                    "dp-replace",
                    "selector-free",
                    "nt51917-dp-replace",
                    "dp-17-fingerprint",
                    "reference",
                    "dp"),
                Route(
                    "NT51929",
                    "dp-replace",
                    "selector-free",
                    "nt51929-dp-replace",
                    "dp-29-fingerprint",
                    "reference",
                    "dp")));
        _ = SetFile(session, "dp", @"C:\firmware\dp.bin", Stamp(0x40000, 1));
        AuthoringPublicationLease lease = session.CapturePublicationLease(
            AuthoringDerivedResultKind.Validation);
        Assert.True(session.TryPublish(
            lease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Validation,
                "validation-before-ic-change")).Succeeded);

        ActiveSessionSnapshot after = Select(session, "NT51929", "selector-free");

        Assert.All(after.Slots, static slot => Assert.Null(slot.SelectedPath));
        Assert.Empty(after.DerivedPublications);
    }

    /// <summary>A file change retains other selected inputs and invalidates derived state.</summary>
    [Fact]
    public void FileChangePreservesOtherSelectedInputsAndInvalidatesDerivedState()
    {
        var session = new AuthoringSessionState("dp-replace");
        _ = Activate(
            session,
            Catalog(
                "dp-replace",
                "dp-token",
                Route(
                    "NT51929",
                    "dp-replace",
                    "selector-free",
                    "nt51929-dp-replace",
                    "dp-29-fingerprint",
                    "reference",
                    "dp")));
        _ = SetFile(
            session,
            "reference",
            @"C:\firmware\reference.bin",
            Stamp(0x80000, 1));
        ActiveSessionSnapshot before = SetFile(
            session,
            "dp",
            @"C:\firmware\dp-v1.bin",
            Stamp(0x40000, 1));
        AuthoringPublicationLease lease = session.CapturePublicationLease(
            AuthoringDerivedResultKind.Inspection);
        Assert.True(session.TryPublish(
            lease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Inspection,
                "inspection-v1")).Succeeded);

        ActiveSessionSnapshot after = SetFile(
            session,
            "dp",
            @"C:\firmware\dp-v2.bin",
            Stamp(0x40000, 2));

        Assert.Equal(before.AuthoringRevision.Next(), after.AuthoringRevision);
        Assert.Equal(@"C:\firmware\reference.bin", Assert.Single(
            after.Slots,
            static slot => slot.DefinitionId == "reference").SelectedPath);
        Assert.Equal(@"C:\firmware\dp-v2.bin", Assert.Single(
            after.Slots,
            static slot => slot.DefinitionId == "dp").SelectedPath);
        Assert.Empty(after.DerivedPublications);
    }

    /// <summary>A mode instance cannot activate a catalog owned by another workflow.</summary>
    [Fact]
    public void ActivationRejectsCatalogForDifferentWorkflow()
    {
        var session = new AuthoringSessionState("dp-replace");
        AuthoringCapabilityCatalogSnapshot abCatalog = Catalog(
            "ab-merge",
            "ab-token",
            Route(
                "NT51929",
                "ab-merge",
                "selector-free",
                "nt51929-ab",
                "ab-fingerprint",
                "dp-ab",
                "tp-a",
                "tp-b"));

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => session.Activate(abCatalog));

        Assert.Equal("catalog", exception.ParamName);
        Assert.Null(session.CurrentSnapshot);
    }

    /// <summary>Every asynchronous result kind rejects publication after the selected file changes.</summary>
    [Theory]
    [InlineData(AuthoringDerivedResultKind.Inspection)]
    [InlineData(AuthoringDerivedResultKind.Validation)]
    [InlineData(AuthoringDerivedResultKind.Preview)]
    [InlineData(AuthoringDerivedResultKind.Build)]
    public void LateResultCannotPublishIntoNewerFileRevision(
        AuthoringDerivedResultKind resultKind)
    {
        var session = new AuthoringSessionState("dp-replace");
        _ = Activate(
            session,
            Catalog(
                "dp-replace",
                "dp-token",
                Route(
                    "NT51929",
                    "dp-replace",
                    "selector-free",
                    "nt51929-dp-replace",
                    "dp-29-fingerprint",
                    "reference",
                    "dp")));
        _ = SetFile(session, "dp", @"C:\firmware\dp.bin", Stamp(0x40000, 1));
        AuthoringPublicationLease staleLease = session.CapturePublicationLease(resultKind);
        _ = SetFile(session, "dp", @"C:\firmware\dp.bin", Stamp(0x40000, 2));

        AuthoringPublicationResult result = session.TryPublish(
            staleLease,
            new AuthoringDerivedPublication(resultKind, $"late-{resultKind}"));

        Assert.False(result.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.StalePublication, result.Issue!.Code);
        Assert.Empty(session.CurrentSnapshot!.DerivedPublications);
    }

    /// <summary>A new canonical publication rejects old-token results while retaining compatible paths.</summary>
    [Fact]
    public void ResolutionTokenChangeRejectsOldLeaseAndRetainsCompatibleInput()
    {
        AuthoringCapabilityRoute route = Route(
            "NT51929",
            "dp-replace",
            "selector-free",
            "nt51929-dp-replace",
            "dp-29-fingerprint",
            "reference",
            "dp");
        var session = new AuthoringSessionState("dp-replace");
        _ = Activate(session, Catalog("dp-replace", "token-1", route));
        _ = SetFile(session, "dp", @"C:\firmware\dp.bin", Stamp(0x40000, 1));
        AuthoringPublicationLease staleLease = session.CapturePublicationLease(
            AuthoringDerivedResultKind.Preview);

        ActiveSessionSnapshot refreshed = Activate(
            session,
            Catalog("dp-replace", "token-2", route));
        AuthoringPublicationResult result = session.TryPublish(
            staleLease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Preview,
                "late-preview"));

        Assert.Equal(@"C:\firmware\dp.bin", Assert.Single(
            refreshed.Slots,
            static slot => slot.DefinitionId == "dp").SelectedPath);
        Assert.False(result.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.StalePublication, result.Issue!.Code);
        Assert.Empty(session.CurrentSnapshot!.DerivedPublications);
    }

    /// <summary>A lease cannot publish into another otherwise identical session.</summary>
    [Fact]
    public void PublicationLeaseCannotCrossSessionInstances()
    {
        AuthoringCapabilityCatalogSnapshot catalog = Catalog(
            "dp-replace",
            "shared-token",
            Route(
                "NT51929",
                "dp-replace",
                "selector-free",
                "nt51929-dp-replace",
                "dp-29-fingerprint",
                "reference",
                "dp"));
        var first = new AuthoringSessionState("dp-replace");
        var second = new AuthoringSessionState("dp-replace");
        _ = Activate(first, catalog);
        _ = Activate(second, catalog);
        FileStamp stamp = Stamp(0x40000, 1);
        _ = SetFile(first, "dp", @"C:\firmware\dp.bin", stamp);
        _ = SetFile(second, "dp", @"C:\firmware\dp.bin", stamp);
        AuthoringPublicationLease firstLease = first.CapturePublicationLease(
            AuthoringDerivedResultKind.Inspection);

        AuthoringPublicationResult result = second.TryPublish(
            firstLease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Inspection,
                "wrong-session"));

        Assert.False(result.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.StalePublication, result.Issue!.Code);
        Assert.Empty(second.CurrentSnapshot!.DerivedPublications);
    }

    /// <summary>Inactive operations fail with stable issues and cannot start derived work.</summary>
    [Fact]
    public void InactiveSessionRejectsSelectionFileUpdatesAndPublicationLeases()
    {
        var session = new AuthoringSessionState("dp-replace");

        AuthoringSessionTransitionResult selection = session.Select(
            "NT51929",
            "selector-free");
        AuthoringSessionTransitionResult file = session.SetSlotFile(
            "dp",
            null,
            null);

        Assert.False(selection.Succeeded);
        Assert.Equal(
            AuthoringSessionIssueCodes.CatalogUnavailable,
            selection.Issue!.Code);
        Assert.False(file.Succeeded);
        Assert.Equal(
            AuthoringSessionIssueCodes.CatalogUnavailable,
            file.Issue!.Code);
        _ = Assert.Throws<InvalidOperationException>(() =>
            session.CapturePublicationLease(
                AuthoringDerivedResultKind.Validation));
    }

    /// <summary>Empty, absent, and ambiguous catalog selections are distinct typed failures.</summary>
    [Fact]
    public void CatalogAndRouteFailuresRemainDistinct()
    {
        var emptySession = new AuthoringSessionState("dp-replace");
        AuthoringSessionTransitionResult empty = emptySession.Activate(
            Catalog("dp-replace", "empty-token"));
        Assert.False(empty.Succeeded);
        Assert.Equal(
            AuthoringSessionIssueCodes.CatalogUnavailable,
            empty.Issue!.Code);

        var session = new AuthoringSessionState("dp-replace");
        _ = Activate(
            session,
            Catalog(
                "dp-replace",
                "dp-token",
                Route(
                    "NT51929",
                    "dp-replace",
                    "selector-free",
                    "nt51929-map",
                    "dp-29-fingerprint",
                    "reference",
                    "dp")));
        AuthoringSessionTransitionResult absent = session.Select(
            "NT51950",
            "cascade");
        Assert.False(absent.Succeeded);
        Assert.Equal(
            AuthoringSessionIssueCodes.RouteUnavailable,
            absent.Issue!.Code);

        var ambiguousSession = new AuthoringSessionState("dp-replace");
        AuthoringSessionTransitionResult ambiguous = ambiguousSession.Activate(
            Catalog(
                "dp-replace",
                "ambiguous-token",
                Route(
                    "NT51950",
                    "dp-replace",
                    "cascade",
                    "nt51950-map-a",
                    "shared-fingerprint",
                    "reference",
                    "dp"),
                Route(
                    "NT51950",
                    "dp-replace",
                    "cascade",
                    "nt51950-map-b",
                    "shared-fingerprint",
                    "reference",
                    "dp")));
        Assert.False(ambiguous.Succeeded);
        Assert.Equal(
            AuthoringSessionIssueCodes.RouteAmbiguous,
            ambiguous.Issue!.Code);
    }

    /// <summary>Slot updates reject incoherent identities, no-op exact repeats, and clear atomically.</summary>
    [Fact]
    public void SlotUpdatesValidateIdentityAndSupportNoOpAndClear()
    {
        var session = new AuthoringSessionState("dp-replace");
        _ = Activate(
            session,
            Catalog(
                "dp-replace",
                "dp-token",
                Route(
                    "NT51929",
                    "dp-replace",
                    "selector-free",
                    "nt51929-map",
                    "dp-29-fingerprint",
                    "reference",
                    "dp")));

        ArgumentException incoherent = Assert.Throws<ArgumentException>(() =>
            session.SetSlotFile(
                "dp",
                @"C:\firmware\dp.bin",
                null));
        Assert.Equal("selectedPath", incoherent.ParamName);

        AuthoringSessionTransitionResult unavailable = session.SetSlotFile(
            "ldc",
            null,
            null);
        Assert.False(unavailable.Succeeded);
        Assert.Equal(
            AuthoringSessionIssueCodes.SlotUnavailable,
            unavailable.Issue!.Code);

        ActiveSessionSnapshot selected = SetFile(
            session,
            "dp",
            @"C:\firmware\dp.bin",
            Stamp(0x40000, 1));
        AuthoringSessionTransitionResult unchanged = session.SetSlotFile(
            "dp",
            @"C:\firmware\dp.bin",
            Stamp(0x40000, 1));
        Assert.Same(selected, unchanged.Snapshot);

        AuthoringSessionTransitionResult cleared = session.SetSlotFile(
            "dp",
            null,
            null);
        Assert.True(cleared.Succeeded);
        AuthoringSlotState clearedSlot = Assert.Single(
            cleared.Snapshot!.Slots,
            static slot => slot.DefinitionId == "dp");
        Assert.Null(clearedSlot.SelectedPath);
        Assert.Equal(AuthoringSlotLifecycle.Empty, clearedSlot.Lifecycle);
    }

    /// <summary>Publication kinds are lease-bound and a newer same-kind result replaces the old reference.</summary>
    [Fact]
    public void PublicationKindIsLeaseBoundAndSameKindPublicationIsReplaced()
    {
        var session = new AuthoringSessionState("dp-replace");
        _ = Activate(
            session,
            Catalog(
                "dp-replace",
                "dp-token",
                Route(
                    "NT51929",
                    "dp-replace",
                    "selector-free",
                    "nt51929-map",
                    "dp-29-fingerprint",
                    "reference",
                    "dp")));
        AuthoringPublicationLease lease = session.CapturePublicationLease(
            AuthoringDerivedResultKind.Validation);

        AuthoringPublicationResult wrongKind = session.TryPublish(
            lease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Preview,
                "preview"));
        Assert.False(wrongKind.Succeeded);
        Assert.Equal(
            AuthoringSessionIssueCodes.InvalidPublication,
            wrongKind.Issue!.Code);

        Assert.True(session.TryPublish(
            lease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Validation,
                "validation-v1")).Succeeded);
        Assert.True(session.TryPublish(
            lease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Validation,
                "validation-v2")).Succeeded);
        AuthoringDerivedPublication publication = Assert.Single(
            session.CurrentSnapshot!.DerivedPublications);
        Assert.Equal("validation-v2", publication.ResultReference);
    }

    /// <summary>Boundary value objects reject invalid revisions, stamps, lifecycles, and result kinds.</summary>
    [Fact]
    public void AuthoringValueObjectsRejectInvalidExternalIdentity()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AuthoringRevision(-1));
        _ = Assert.Throws<OverflowException>(() =>
            new AuthoringRevision(long.MaxValue).Next());
        _ = Assert.Throws<ArgumentException>(() =>
            new FileStamp(
                exists: false,
                length: 1,
                DateTimeOffset.UnixEpoch));
        _ = Assert.Throws<ArgumentException>(() =>
            new FileStamp(
                exists: true,
                length: 0,
                new DateTimeOffset(
                    2026,
                    1,
                    1,
                    0,
                    0,
                    0,
                    TimeSpan.FromHours(8))));
        _ = Assert.Throws<ArgumentException>(() =>
            new AuthoringSlotState(
                "dp",
                null,
                null,
                AuthoringSlotLifecycle.Selected));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AuthoringDerivedPublication(
                (AuthoringDerivedResultKind)999,
                "invalid"));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            SessionWithInvalidResultKind);

        static AuthoringPublicationLease SessionWithInvalidResultKind()
        {
            var session = new AuthoringSessionState("dp-replace");
            _ = Activate(
                session,
                Catalog(
                    "dp-replace",
                    "dp-token",
                    Route(
                        "NT51929",
                        "dp-replace",
                        "selector-free",
                        "nt51929-map",
                        "dp-29-fingerprint",
                        "reference",
                        "dp")));
            return session.CapturePublicationLease(
                (AuthoringDerivedResultKind)999);
        }
    }

    private static AuthoringCapabilityCatalogSnapshot Catalog(
        string workflowId,
        string token,
        params AuthoringCapabilityRoute[] routes)
    {
        return new AuthoringCapabilityCatalogSnapshot(
            workflowId,
            new ResolutionToken(token),
            routes);
    }

    private static AuthoringCapabilityRoute Route(
        string icId,
        string workflowId,
        string icCount,
        string mapVariant,
        string capabilityFingerprint,
        params string[] slotDefinitionIds)
    {
        return new AuthoringCapabilityRoute(
            new CapabilityRouteIdentity(icId, workflowId, icCount, mapVariant),
            capabilityFingerprint,
            executionAdmitted: true,
            slotDefinitionIds.Select(static definitionId =>
                new AuthoringSlotDefinitionReference(definitionId)));
    }

    private static FileStamp Stamp(long length, int revision)
    {
        return new FileStamp(
            exists: true,
            length,
            DateTimeOffset.UnixEpoch.AddSeconds(revision));
    }

    private static ActiveSessionSnapshot Activate(
        AuthoringSessionState session,
        AuthoringCapabilityCatalogSnapshot catalog)
    {
        AuthoringSessionTransitionResult result = session.Activate(catalog);
        Assert.True(result.Succeeded, result.Issue?.Message);
        return result.Snapshot!;
    }

    private static ActiveSessionSnapshot Select(
        AuthoringSessionState session,
        string icId,
        string icCount)
    {
        AuthoringSessionTransitionResult result = session.Select(icId, icCount);
        Assert.True(result.Succeeded, result.Issue?.Message);
        return result.Snapshot!;
    }

    private static ActiveSessionSnapshot SetFile(
        AuthoringSessionState session,
        string slotDefinitionId,
        string path,
        FileStamp stamp)
    {
        AuthoringSessionTransitionResult result = session.SetSlotFile(
            slotDefinitionId,
            path,
            stamp);
        Assert.True(result.Succeeded, result.Issue?.Message);
        return result.Snapshot!;
    }
}
