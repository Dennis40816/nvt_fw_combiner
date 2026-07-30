using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>Tests caller-owned, mode-isolated Merge authoring sessions.</summary>
public sealed class MergeAuthoringSessionSetTests
{
    /// <summary>Each Merge mode has one stable session and never inherits another mode's state.</summary>
    [Fact]
    public void MergeModesRestoreOnlyTheirOwnSelectionsSlotsAndDraft()
    {
        var sessions = new MergeAuthoringSessionSet();
        AuthoringSessionState standard = sessions.ForWorkflow(
            ExperienceIds.StandardMerge);
        AuthoringSessionState ab = sessions.ForWorkflow(
            ExperienceIds.AbMerge);
        AuthoringSessionState general = sessions.ForWorkflow(
            ExperienceIds.GeneralMerge);

        _ = Activate(
            standard,
            Catalog(
                ExperienceIds.StandardMerge,
                "standard-token",
                Route(
                    "NT51929",
                    ExperienceIds.StandardMerge,
                    "selector-free",
                    "standard-map",
                    "standard-fingerprint",
                    "dp",
                    "tp")));
        _ = Activate(
            ab,
            Catalog(
                ExperienceIds.AbMerge,
                "ab-token",
                Route(
                    "NT51950",
                    ExperienceIds.AbMerge,
                    "single",
                    "ab-map",
                    "ab-fingerprint",
                    "dp-ab",
                    "tp-a",
                    "tp-b")));
        _ = Activate(
            general,
            Catalog(
                ExperienceIds.GeneralMerge,
                "general-token",
                Route(
                    "NT51926",
                    ExperienceIds.GeneralMerge,
                    "selector-free",
                    "general-map",
                    "general-fingerprint",
                    "mapping-source")));

        _ = SetFile(
            standard,
            "dp",
            @"C:\firmware\standard-dp.bin",
            Stamp(0x6000, 1));
        _ = SetFile(
            ab,
            "dp-ab",
            @"C:\firmware\ab-dp.bin",
            Stamp(0x80000, 1));
        _ = SetFile(
            general,
            "mapping-source",
            @"C:\firmware\mapping.bin",
            Stamp(0x2000, 1));
        _ = SetDraft(
            general,
            new TestDraftState("row-1"));

        Assert.Same(standard, sessions.ForWorkflow(ExperienceIds.StandardMerge));
        Assert.Same(ab, sessions.ForWorkflow(ExperienceIds.AbMerge));
        Assert.Same(general, sessions.ForWorkflow(ExperienceIds.GeneralMerge));
        Assert.Equal(
            @"C:\firmware\standard-dp.bin",
            SelectedPath(standard, "dp"));
        Assert.Null(standard.CurrentSnapshot!.DraftState);
        Assert.Equal(
            @"C:\firmware\ab-dp.bin",
            SelectedPath(ab, "dp-ab"));
        Assert.Null(ab.CurrentSnapshot!.DraftState);
        Assert.Equal(
            @"C:\firmware\mapping.bin",
            SelectedPath(general, "mapping-source"));
        Assert.Equal(
            "row-1",
            Assert.IsType<TestDraftState>(
                general.CurrentSnapshot!.DraftState).Value);
    }

    /// <summary>Draft changes invalidate derived state and survive only compatible route changes.</summary>
    [Fact]
    public void DraftStateUsesTheSameRevisionAndCompatibilityRulesAsSlots()
    {
        const string sharedFingerprint = "shared-general-fingerprint";
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        _ = Activate(
            session,
            Catalog(
                ExperienceIds.GeneralMerge,
                "general-token",
                Route(
                    "NT51926",
                    ExperienceIds.GeneralMerge,
                    "single",
                    "general-single",
                    sharedFingerprint,
                    "mapping-source"),
                Route(
                    "NT51926",
                    ExperienceIds.GeneralMerge,
                    "cascade",
                    "general-cascade",
                    sharedFingerprint,
                    "mapping-source"),
                Route(
                    "NT51929",
                    ExperienceIds.GeneralMerge,
                    "selector-free",
                    "general-other",
                    "other-general-fingerprint",
                    "mapping-source")));
        ActiveSessionSnapshot beforeDraft = SetFile(
            session,
            "mapping-source",
            @"C:\firmware\mapping.bin",
            Stamp(0x2000, 1));
        AuthoringPublicationLease lease = session.CapturePublicationLease(
            AuthoringDerivedResultKind.Validation);
        Assert.True(session.TryPublish(
            lease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Validation,
                "validation-before-draft")).Succeeded);

        ActiveSessionSnapshot withDraft = SetDraft(
            session,
            new TestDraftState("row-1"));

        Assert.Equal(
            beforeDraft.AuthoringRevision.Next(),
            withDraft.AuthoringRevision);
        Assert.Empty(withDraft.DerivedPublications);
        AuthoringSessionTransitionResult unchanged = session.SetDraft(
            new TestDraftState("row-1"));
        Assert.Same(withDraft, unchanged.Snapshot);
        Assert.False(session.TryPublish(
            lease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Validation,
                "late-validation")).Succeeded);

        _ = Select(
            session,
            "NT51926",
            "single");
        ActiveSessionSnapshot compatible = Select(
            session,
            "NT51926",
            "cascade");
        _ = Assert.IsType<TestDraftState>(compatible.DraftState);

        ActiveSessionSnapshot incompatible = Select(
            session,
            "NT51929",
            "selector-free");
        Assert.Null(incompatible.DraftState);
    }

    /// <summary>Session publication stores a defensive immutable draft projection.</summary>
    [Fact]
    public void DraftStateIsDefensivelySnapshottedBeforePublication()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        _ = Activate(
            session,
            Catalog(
                ExperienceIds.GeneralMerge,
                "general-token",
                Route(
                    "NT51926",
                    ExperienceIds.GeneralMerge,
                    "selector-free",
                    "general-map",
                    "general-fingerprint",
                    "mapping-source")));
        var callerOwnedRows = new List<string> { "row-1" };

        ActiveSessionSnapshot snapshot = SetDraft(
            session,
            new MutableTestDraftState(callerOwnedRows));
        callerOwnedRows[0] = "mutated-after-publication";
        callerOwnedRows.Add("row-2");

        Assert.Equal(
            "row-1",
            Assert.IsType<TestDraftState>(snapshot.DraftState).Value);
        Assert.Equal(
            "row-1",
            Assert.IsType<TestDraftState>(
                session.CurrentSnapshot!.DraftState).Value);
    }

    /// <summary>Only General authoring workflows admit mapping-draft state.</summary>
    [Theory]
    [InlineData(ExperienceIds.StandardMerge)]
    [InlineData(ExperienceIds.AbMerge)]
    [InlineData(ExperienceIds.DpReplace)]
    public void WorkflowsWithoutDraftSemanticsRejectDraftState(
        string workflowId)
    {
        var session = new AuthoringSessionState(workflowId);
        ActiveSessionSnapshot before = Activate(
            session,
            Catalog(
                workflowId,
                $"{workflowId}-token",
                Route(
                    "NT51929",
                    workflowId,
                    "selector-free",
                    $"{workflowId}-map",
                    $"{workflowId}-fingerprint",
                    "input")));

        AuthoringSessionTransitionResult result = session.SetDraft(
            new TestDraftState("row-1"));

        Assert.False(result.Succeeded);
        Assert.Equal(
            AuthoringSessionIssueCodes.DraftUnavailable,
            result.Issue!.Code);
        Assert.Same(before, result.Snapshot);
        Assert.Null(before.DraftState);
        Assert.Equal(new AuthoringRevision(1), before.AuthoringRevision);
    }

    /// <summary>Capability changes advance revision when they invalidate a draft.</summary>
    [Fact]
    public void ActivationAdvancesRevisionWhenCapabilityChangeDropsDraft()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        _ = Activate(
            session,
            Catalog(
                ExperienceIds.GeneralMerge,
                "token-1",
                Route(
                    "NT51926",
                    ExperienceIds.GeneralMerge,
                    "selector-free",
                    "general-map",
                    "fingerprint-1",
                    "mapping-source")));
        ActiveSessionSnapshot withDraft = SetDraft(
            session,
            new TestDraftState("row-1"));

        ActiveSessionSnapshot changed = Activate(
            session,
            Catalog(
                ExperienceIds.GeneralMerge,
                "token-2",
                Route(
                    "NT51926",
                    ExperienceIds.GeneralMerge,
                    "selector-free",
                    "general-map",
                    "fingerprint-2",
                    "mapping-source")));

        Assert.Equal(
            withDraft.AuthoringRevision.Next(),
            changed.AuthoringRevision);
        Assert.Null(changed.DraftState);

        ActiveSessionSnapshot restored = SetDraft(
            session,
            new TestDraftState("row-2"));
        ActiveSessionSnapshot policyOnlyPublication = Activate(
            session,
            Catalog(
                ExperienceIds.GeneralMerge,
                "token-3",
                Route(
                    "NT51926",
                    ExperienceIds.GeneralMerge,
                    "selector-free",
                    "general-map",
                    "fingerprint-2",
                    "mapping-source")));

        Assert.Equal(
            restored.AuthoringRevision,
            policyOnlyPublication.AuthoringRevision);
        Assert.Equal(
            "row-2",
            Assert.IsType<TestDraftState>(
                policyOnlyPublication.DraftState).Value);
    }

    /// <summary>CLI sessions are ephemeral instances over the identical transition contract.</summary>
    [Fact]
    public void CliEphemeralSessionUsesTheSameRulesWithoutSharingState()
    {
        AuthoringCapabilityCatalogSnapshot catalog = Catalog(
            ExperienceIds.StandardMerge,
            "standard-token",
            Route(
                "NT51929",
                ExperienceIds.StandardMerge,
                "selector-free",
                "standard-map",
                "standard-fingerprint",
                "dp",
                "tp"));
        var desktopSessions = new MergeAuthoringSessionSet();
        AuthoringSessionState desktop = desktopSessions.ForWorkflow(
            ExperienceIds.StandardMerge);
        AuthoringSessionState cli = MergeAuthoringSessionSet.CreateEphemeral(
            ExperienceIds.StandardMerge);
        _ = Activate(desktop, catalog);
        _ = Activate(cli, catalog);
        FileStamp firstStamp = Stamp(0x6000, 1);
        _ = SetFile(
            desktop,
            "dp",
            @"C:\firmware\dp.bin",
            firstStamp);
        _ = SetFile(
            cli,
            "dp",
            @"C:\firmware\dp.bin",
            firstStamp);

        Assert.NotSame(desktop, cli);
        Assert.Equal(
            desktop.CurrentSnapshot!.SelectedRouteId,
            cli.CurrentSnapshot!.SelectedRouteId);
        Assert.Equal(
            desktop.CurrentSnapshot.AuthoringRevision,
            cli.CurrentSnapshot.AuthoringRevision);
        Assert.Equal(
            SelectedPath(desktop, "dp"),
            SelectedPath(cli, "dp"));

        _ = SetFile(
            desktop,
            "dp",
            @"C:\firmware\dp-v2.bin",
            Stamp(0x6000, 2));
        Assert.Equal(@"C:\firmware\dp.bin", SelectedPath(cli, "dp"));
    }

    /// <summary>The fixed Merge set rejects Replace workflows instead of growing an arbitrary store.</summary>
    [Fact]
    public void MergeSessionSetRejectsNonMergeWorkflows()
    {
        var sessions = new MergeAuthoringSessionSet();
        var inactive = new AuthoringSessionState(
            ExperienceIds.GeneralMerge);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            sessions.ForWorkflow(ExperienceIds.DpReplace));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MergeAuthoringSessionSet.CreateEphemeral(
                ExperienceIds.CtrlRamReplace));
        AuthoringSessionTransitionResult draft = inactive.SetDraft(
            new TestDraftState("row-1"));
        Assert.False(draft.Succeeded);
        Assert.Equal(
            AuthoringSessionIssueCodes.CatalogUnavailable,
            draft.Issue!.Code);
    }

    /// <summary>Session snapshots retain metadata identities, never complete firmware or UI document payloads.</summary>
    [Fact]
    public void SessionSnapshotContractHasNoBinReportOrHexPayloadOwner()
    {
        Type[] forbiddenTypes =
        [
            typeof(byte[]),
            typeof(Stream),
        ];
        string[] forbiddenNames =
        [
            "Bin",
            "Bytes",
            "Hex",
            "Payload",
            "ReportHistory",
        ];

        Assert.DoesNotContain(
            typeof(ActiveSessionSnapshot).GetProperties(),
            property =>
                forbiddenTypes.Any(type =>
                    type.IsAssignableFrom(property.PropertyType)) ||
                forbiddenNames.Any(name =>
                    property.Name.Contains(
                        name,
                        StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(
            typeof(AuthoringSlotState).GetProperties(),
            property => forbiddenTypes.Any(type =>
                type.IsAssignableFrom(property.PropertyType)));
    }

    private static string? SelectedPath(
        AuthoringSessionState session,
        string slotDefinitionId)
    {
        return Assert.Single(
            session.CurrentSnapshot!.Slots,
            slot => slot.DefinitionId == slotDefinitionId).SelectedPath;
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

    private sealed record MutableTestDraftState(IReadOnlyList<string> Rows)
        : AuthoringDraftState(AuthoringDraftKind.GeneralMapping)
    {
        internal override AuthoringDraftState CreateImmutableSnapshot()
        {
            return new TestDraftState(string.Join("|", Rows));
        }
    }
}
