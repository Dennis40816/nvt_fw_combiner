using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>Tests caller-owned, mode-isolated Replace authoring sessions.</summary>
public sealed class ReplaceAuthoringSessionIsolationTests
{
    /// <summary>Six independently owned workflow sessions retain their exact workflow identities.</summary>
    [Fact]
    public void CallerOwnedWorkflowSessionsRemainDistinct()
    {
        AuthoringSessionState[] sessions =
        [
            new(ExperienceIds.StandardMerge),
            new(ExperienceIds.AbMerge),
            new(ExperienceIds.GeneralMerge),
            new(ExperienceIds.DpReplace),
            new(ExperienceIds.CtrlRamReplace),
            new(ExperienceIds.GeneralReplace),
        ];

        Assert.Equal(
            [
                ExperienceIds.StandardMerge,
                ExperienceIds.AbMerge,
                ExperienceIds.GeneralMerge,
                ExperienceIds.DpReplace,
                ExperienceIds.CtrlRamReplace,
                ExperienceIds.GeneralReplace,
            ],
            sessions.Select(static session => session.WorkflowId));
        Assert.Equal(sessions.Length, sessions.Distinct().Count());
    }

    /// <summary>Each Replace mode restores only its own slots, mapping draft, readiness, and results.</summary>
    [Fact]
    public void ReplaceModesRestoreOnlyTheirOwnSlotsDraftReadinessAndResults()
    {
        var dp = new AuthoringSessionState(ExperienceIds.DpReplace);
        var ctrlRam = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        var general = new AuthoringSessionState(ExperienceIds.GeneralReplace);

        _ = Activate(
            dp,
            Catalog(
                ExperienceIds.DpReplace,
                "dp-token",
                Route(
                    "NT51929",
                    ExperienceIds.DpReplace,
                    "selector-free",
                    "dp-map",
                    "dp-fingerprint",
                    "reference",
                    "dp")));
        _ = Activate(
            ctrlRam,
            Catalog(
                ExperienceIds.CtrlRamReplace,
                "ctrlram-token",
                Route(
                    "NT51929",
                    ExperienceIds.CtrlRamReplace,
                    "single",
                    "ctrlram-map",
                    "ctrlram-fingerprint",
                    "reference",
                    "ctrlram-master")));
        _ = Activate(
            general,
            Catalog(
                ExperienceIds.GeneralReplace,
                "general-token",
                Route(
                    "NT51926",
                    ExperienceIds.GeneralReplace,
                    "single",
                    "general-map",
                    "general-fingerprint",
                    "reference",
                    "mapping-source")));

        _ = SetFile(
            dp,
            "dp",
            @"C:\firmware\dp.bin",
            Stamp(0x40000, 1));
        _ = SetFile(
            ctrlRam,
            "ctrlram-master",
            @"C:\firmware\ctrlram.bin",
            Stamp(0x1000, 1));
        _ = SetFile(
            general,
            "mapping-source",
            @"C:\firmware\mapping.bin",
            Stamp(0x2000, 1));
        _ = SetDraft(general, new TestDraftState("mapping-row-1"));
        Publish(
            ctrlRam,
            AuthoringDerivedResultKind.Validation,
            "ctrlram-readiness");
        Publish(
            general,
            AuthoringDerivedResultKind.Preview,
            "general-preview");

        Assert.Equal(@"C:\firmware\dp.bin", SelectedPath(dp, "dp"));
        Assert.Empty(dp.CurrentSnapshot!.DerivedPublications);
        Assert.Equal(
            @"C:\firmware\ctrlram.bin",
            SelectedPath(ctrlRam, "ctrlram-master"));
        Assert.Equal(
            "ctrlram-readiness",
            Assert.Single(ctrlRam.CurrentSnapshot!.DerivedPublications)
                .ResultReference);
        Assert.Equal(
            @"C:\firmware\mapping.bin",
            SelectedPath(general, "mapping-source"));
        Assert.Equal(
            "mapping-row-1",
            Assert.IsType<TestDraftState>(
                general.CurrentSnapshot!.DraftState).Value);
        Assert.Equal(
            "general-preview",
            Assert.Single(general.CurrentSnapshot.DerivedPublications)
                .ResultReference);
    }

    /// <summary>An incompatible CtrlRAM selection removes old targets and processor-derived state.</summary>
    [Fact]
    public void CtrlRamSelectionInvalidatesTargetsAndProcessorProjection()
    {
        var session = new AuthoringSessionState(
            ExperienceIds.CtrlRamReplace);
        _ = Activate(
            session,
            Catalog(
                ExperienceIds.CtrlRamReplace,
                "ctrlram-token",
                Route(
                    "NT51929",
                    ExperienceIds.CtrlRamReplace,
                    "single",
                    "single-map",
                    "single-fingerprint",
                    "reference",
                    "ctrlram-master"),
                Route(
                    "NT51929",
                    ExperienceIds.CtrlRamReplace,
                    "cascade",
                    "cascade-map",
                    "cascade-fingerprint",
                    "reference",
                    "diff-dlm")));
        _ = Select(session, "NT51929", "single");
        _ = SetFile(
            session,
            "reference",
            @"C:\firmware\reference.bin",
            Stamp(0x80000, 1));
        ActiveSessionSnapshot before = SetFile(
            session,
            "ctrlram-master",
            @"C:\firmware\ctrlram.bin",
            Stamp(0x1000, 1));
        Publish(
            session,
            AuthoringDerivedResultKind.Validation,
            "legacy-combiner-readiness");

        ActiveSessionSnapshot after = Select(
            session,
            "NT51929",
            "cascade");

        Assert.Equal(before.AuthoringRevision.Next(), after.AuthoringRevision);
        Assert.DoesNotContain(
            after.Slots,
            static slot => slot.DefinitionId == "ctrlram-master");
        Assert.Contains(
            after.Slots,
            static slot => slot.DefinitionId == "diff-dlm");
        Assert.All(after.Slots, static slot => Assert.Null(slot.SelectedPath));
        Assert.Empty(after.DerivedPublications);
    }

    /// <summary>General Replace drafts survive only compatible routes and reject stale results.</summary>
    [Fact]
    public void GeneralReplaceDraftUsesCapabilityAndPublicationIdentity()
    {
        const string sharedFingerprint = "shared-general-fingerprint";
        var session = new AuthoringSessionState(
            ExperienceIds.GeneralReplace);
        _ = Activate(
            session,
            Catalog(
                ExperienceIds.GeneralReplace,
                "general-token",
                Route(
                    "NT51926",
                    ExperienceIds.GeneralReplace,
                    "single",
                    "single-map",
                    sharedFingerprint,
                    "reference",
                    "mapping-source"),
                Route(
                    "NT51926",
                    ExperienceIds.GeneralReplace,
                    "cascade",
                    "cascade-map",
                    sharedFingerprint,
                    "reference",
                    "mapping-source"),
                Route(
                    "NT51929",
                    ExperienceIds.GeneralReplace,
                    "selector-free",
                    "other-map",
                    "other-general-fingerprint",
                    "reference",
                    "mapping-source")));
        _ = Select(session, "NT51926", "single");
        _ = SetDraft(session, new TestDraftState("mapping-row-1"));
        AuthoringPublicationLease staleLease = session.CapturePublicationLease(
            AuthoringDerivedResultKind.Preview);

        ActiveSessionSnapshot compatible = Select(
            session,
            "NT51926",
            "cascade");
        AuthoringPublicationResult stale = session.TryPublish(
            staleLease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Preview,
                "late-preview"));
        ActiveSessionSnapshot incompatible = Select(
            session,
            "NT51929",
            "selector-free");

        Assert.Equal(
            "mapping-row-1",
            Assert.IsType<TestDraftState>(compatible.DraftState).Value);
        Assert.False(stale.Succeeded);
        Assert.Equal(
            AuthoringSessionIssueCodes.StalePublication,
            stale.Issue!.Code);
        Assert.Null(incompatible.DraftState);
        Assert.Empty(incompatible.DerivedPublications);
    }

    /// <summary>CLI Replace sessions use identical rules without sharing desktop state.</summary>
    [Fact]
    public void CliEphemeralReplaceSessionDoesNotShareDesktopState()
    {
        AuthoringCapabilityCatalogSnapshot catalog = Catalog(
            ExperienceIds.CtrlRamReplace,
            "ctrlram-token",
            Route(
                "NT51929",
                ExperienceIds.CtrlRamReplace,
                "single",
                "ctrlram-map",
                "ctrlram-fingerprint",
                "reference",
                "ctrlram-master"));
        var desktop = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        var cli = new AuthoringSessionState(
            ExperienceIds.CtrlRamReplace);
        _ = Activate(desktop, catalog);
        _ = Activate(cli, catalog);
        FileStamp firstStamp = Stamp(0x1000, 1);
        _ = SetFile(
            desktop,
            "ctrlram-master",
            @"C:\firmware\ctrlram.bin",
            firstStamp);
        _ = SetFile(
            cli,
            "ctrlram-master",
            @"C:\firmware\ctrlram.bin",
            firstStamp);

        AuthoringPublicationLease desktopLease =
            desktop.CapturePublicationLease(
                AuthoringDerivedResultKind.Validation);
        AuthoringPublicationResult crossSession = cli.TryPublish(
            desktopLease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Validation,
                "wrong-session-readiness"));
        _ = SetFile(
            desktop,
            "ctrlram-master",
            @"C:\firmware\ctrlram-v2.bin",
            Stamp(0x1000, 2));

        Assert.NotSame(desktop, cli);
        Assert.False(crossSession.Succeeded);
        Assert.Equal(
            AuthoringSessionIssueCodes.StalePublication,
            crossSession.Issue!.Code);
        Assert.Equal(
            @"C:\firmware\ctrlram.bin",
            SelectedPath(cli, "ctrlram-master"));
        Assert.Empty(cli.CurrentSnapshot!.DerivedPublications);
    }

    private static string? SelectedPath(
        AuthoringSessionState session,
        string slotDefinitionId)
    {
        return Assert.Single(
            session.CurrentSnapshot!.Slots,
            slot => slot.DefinitionId == slotDefinitionId).SelectedPath;
    }

    private static void Publish(
        AuthoringSessionState session,
        AuthoringDerivedResultKind kind,
        string resultReference)
    {
        AuthoringPublicationLease lease = session.CapturePublicationLease(kind);
        AuthoringPublicationResult result = session.TryPublish(
            lease,
            new AuthoringDerivedPublication(kind, resultReference));
        Assert.True(result.Succeeded, result.Issue?.Message);
    }

    private static ActiveSessionSnapshot SetDraft(
        AuthoringSessionState session,
        AuthoringDraftState draft)
    {
        AuthoringSessionTransitionResult result = session.SetDraft(draft);
        Assert.True(result.Succeeded, result.Issue?.Message);
        return result.Snapshot!;
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
            new CapabilityRouteIdentity(
                icId,
                workflowId,
                icCount,
                mapVariant),
            capabilityFingerprint,
            executionAdmitted: true,
            slotDefinitionIds.Select(static definitionId =>
                new AuthoringSlotDefinitionReference(definitionId)));
    }

    private static FileStamp Stamp(long length, int revision)
    {
        return new FileStamp(length, $"{revision:x64}");
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
        AuthoringSessionTransitionResult result = session.Select(
            icId,
            icCount);
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

    private sealed record TestDraftState(string Value)
        : AuthoringDraftState(AuthoringDraftKind.GeneralMapping)
    {
        internal override AuthoringDraftState CreateImmutableSnapshot()
        {
            return this;
        }
    }
}
