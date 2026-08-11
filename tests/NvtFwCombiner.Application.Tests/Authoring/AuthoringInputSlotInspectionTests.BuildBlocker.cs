using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

public sealed partial class AuthoringInputSlotInspectionTests
{
    /// <summary>The shell blocker projection delegates compiled sessions to canonical action readiness.</summary>
    [Fact]
    public void CompiledSessionBuildBlockerUsesCanonicalReadiness()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        Assert.True(session.Activate(
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(capability)).Succeeded);

        CapabilityActionBlocker? blocker = ActiveSessionBuildBlockerResolver.Resolve(
            session.CurrentSnapshot,
            ExperienceIds.StandardMerge);

        Assert.NotNull(blocker);
        Assert.Equal(CapabilityActionReadinessIssueCodes.InputPending, blocker.Code);
        Assert.Equal(SourceSlot, blocker.SubjectId);
        Assert.Equal(CapabilityReadinessNextAction.LoadRequiredInput, blocker.NextAction);
    }

    /// <summary>A dependency-bearing compilation never fabricates a ready runtime observation.</summary>
    [Fact]
    public void MissingRuntimeSnapshotFailsClosedOnlyWhenTheCompiledPlanDeclaresDependencies()
    {
        ResolvedCapability withDependency = CreateCapability(
            ExperienceIds.StandardMerge,
            includeExternalProcessor: true);
        ResolvedCapability withoutDependency = CreateCapability(ExperienceIds.StandardMerge);
        var revision = new AuthoringRevision(3);
        CapabilityChildReadiness[] readyInputs =
        [
            new CapabilityChildReadiness(SourceSlot, ResolvedChildReadiness.Ready),
        ];

        CapabilityActionBlocker? missingRuntime =
            CapabilityActionReadinessResolver.ResolvePrimaryBuildBlockerBeforeRuntimeRefresh(
                CapabilityAdmissionSnapshot.FromResolvedCapability(withDependency, revision),
                readyInputs,
                RuntimeDependencyReadinessRequest.FromResolvedCapability(
                    withDependency,
                    revision));
        CapabilityActionBlocker? noRuntimeRequired =
            CapabilityActionReadinessResolver.ResolvePrimaryBuildBlockerBeforeRuntimeRefresh(
                CapabilityAdmissionSnapshot.FromResolvedCapability(withoutDependency, revision),
                readyInputs,
                RuntimeDependencyReadinessRequest.FromResolvedCapability(
                    withoutDependency,
                    revision));

        Assert.Equal(
            CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale,
            Assert.IsType<CapabilityActionBlocker>(missingRuntime).Code);
        Assert.Equal(
            CapabilityReadinessNextAction.RefreshRuntimeDependencies,
            missingRuntime.NextAction);
        Assert.Null(noRuntimeRequired);
    }

    /// <summary>A current canonical runtime observation is consumed instead of being recomputed.</summary>
    [Fact]
    public void CurrentCanonicalReadinessSuppliesTheRuntimeBlocker()
    {
        ResolvedCapability capability = CreateCapability(
            ExperienceIds.StandardMerge,
            includeExternalProcessor: true);
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        Assert.True(session.Activate(
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(capability)).Succeeded);
        ActiveSessionSnapshot snapshot = session.CurrentSnapshot!;
        var admission =
            CapabilityAdmissionSnapshot.FromResolvedCapability(
                capability,
                snapshot.AuthoringRevision);
        var runtime = new RuntimeDependencyReadinessSnapshot(
            admission.RouteId,
            admission.CapabilityFingerprint,
            admission.CompilationFingerprint,
            admission.ResolutionToken,
            admission.AuthoringRevision,
            7,
            DateTimeOffset.UnixEpoch,
            [RuntimeDependencyEntry.Blocked(
                "crc-worker",
                "nvt-crc-worker",
                "runtime.missing",
                "The required CRC worker is unavailable.")]);
        CapabilityActionReadinessSnapshot readiness =
            CapabilityActionReadinessResolver.Resolve(
                admission,
                [],
                runtime,
                currentRuntimeDependencyGeneration: 7);

        CapabilityActionBlocker? blocker = ActiveSessionBuildBlockerResolver.Resolve(
            snapshot,
            ExperienceIds.StandardMerge,
            readiness);

        Assert.Equal(
            CapabilityActionReadinessIssueCodes.RuntimeDependencyBlocked,
            Assert.IsType<CapabilityActionBlocker>(blocker).Code);
        Assert.Equal("crc-worker:nvt-crc-worker", blocker.SubjectId);
    }

    /// <summary>Pre-compilation sessions fail closed using the most specific current input state.</summary>
    [Fact]
    public void PreCompilationBuildBlockerRanksStatusAndSlotState()
    {
        const string workflowId = ExperienceIds.DpReplace;
        ResolvedCapabilityRoute route = CreateRoute(workflowId);
        var revision = new AuthoringRevision(4);
        InputSelectionMemberReadiness blocked = Readiness(
            ResolvedChildReadiness.Blocked,
            reason: "The selected input is invalid.");
        InputSelectionMemberReadiness pending = Readiness(
            ResolvedChildReadiness.PendingInput,
            reason: "Load the prerequisite input.");
        AuthoringInputSlotStatus blockedStatus = PreCompilationStatus(
            route,
            revision,
            blocked);
        AuthoringInputSlotStatus pendingStatus = PreCompilationStatus(
            route,
            revision,
            pending);
        AuthoringInputSlotStatus blockingInspection = new(
            route.Identity,
            route.ResolutionToken,
            revision,
            route.CapabilityFingerprint,
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
            Readiness(ResolvedChildReadiness.Ready),
            SourceSpace,
            AuthoringSlotLifecycle.Error,
            fileStamp: null,
            inspection: null);

        AssertBlocker(
            ActiveSessionBuildBlockerResolver.Resolve(null, workflowId),
            CapabilityActionReadinessIssueCodes.InputPending,
            workflowId,
            "Select the required inputs before continuing.");
        AssertBlocker(
            ActiveSessionBuildBlockerResolver.Resolve(
                PreCompilationSnapshot(route, revision, Slot(AuthoringSlotLifecycle.Selected),
                    [blockingInspection]),
                workflowId),
            CapabilityActionReadinessIssueCodes.InputBlocked,
            SourceSlot,
            "Correct the selected input before continuing.");
        AssertBlocker(
            ActiveSessionBuildBlockerResolver.Resolve(
                PreCompilationSnapshot(route, revision, Slot(AuthoringSlotLifecycle.Selected),
                    [blockedStatus]),
                workflowId),
            CapabilityActionReadinessIssueCodes.InputBlocked,
            SourceSlot,
            "The selected input is invalid.");
        AssertBlocker(
            ActiveSessionBuildBlockerResolver.Resolve(
                PreCompilationSnapshot(route, revision, Slot(AuthoringSlotLifecycle.Selected),
                    [pendingStatus]),
                workflowId),
            CapabilityActionReadinessIssueCodes.InputPending,
            SourceSlot,
            "Load the prerequisite input.");
        AssertBlocker(
            ActiveSessionBuildBlockerResolver.Resolve(
                PreCompilationSnapshot(route, revision, Slot(AuthoringSlotLifecycle.Error)),
                workflowId),
            CapabilityActionReadinessIssueCodes.InputBlocked,
            SourceSlot,
            "Correct the selected input before continuing.");
        AssertBlocker(
            ActiveSessionBuildBlockerResolver.Resolve(
                PreCompilationSnapshot(route, revision, Slot(AuthoringSlotLifecycle.Empty)),
                workflowId),
            CapabilityActionReadinessIssueCodes.InputPending,
            SourceSlot,
            "Load the required input before continuing.");
        AssertBlocker(
            ActiveSessionBuildBlockerResolver.Resolve(
                PreCompilationSnapshot(route, revision, Slot(AuthoringSlotLifecycle.Checking)),
                workflowId),
            CapabilityActionReadinessIssueCodes.InputPending,
            SourceSlot,
            "Wait for input verification to finish before continuing.");
        AssertBlocker(
            ActiveSessionBuildBlockerResolver.Resolve(
                PreCompilationSnapshot(route, revision, Slot(AuthoringSlotLifecycle.Verified)),
                workflowId),
            CapabilityActionReadinessIssueCodes.InputPending,
            workflowId,
            "Load the required input before continuing.");
    }

    /// <summary>Compiled sessions project each canonical slot state into shared action readiness.</summary>
    [Theory]
    [InlineData(ResolvedChildReadiness.Blocked, AuthoringSlotLifecycle.Selected,
        CapabilityActionReadinessIssueCodes.InputBlocked)]
    [InlineData(ResolvedChildReadiness.PendingInput, AuthoringSlotLifecycle.Selected,
        CapabilityActionReadinessIssueCodes.InputPending)]
    [InlineData(ResolvedChildReadiness.Ready, AuthoringSlotLifecycle.Error,
        CapabilityActionReadinessIssueCodes.InputBlocked)]
    [InlineData(ResolvedChildReadiness.Ready, AuthoringSlotLifecycle.Empty,
        CapabilityActionReadinessIssueCodes.InputPending)]
    [InlineData(ResolvedChildReadiness.Ready, AuthoringSlotLifecycle.Selected,
        CapabilityActionReadinessIssueCodes.InputPending)]
    [InlineData(ResolvedChildReadiness.Ready, AuthoringSlotLifecycle.Checking,
        CapabilityActionReadinessIssueCodes.InputPending)]
    [InlineData(ResolvedChildReadiness.Ready, AuthoringSlotLifecycle.Verified, null)]
    public void CompiledBuildBlockerProjectsCanonicalSlotState(
        ResolvedChildReadiness readiness,
        AuthoringSlotLifecycle lifecycle,
        string? expectedCode)
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);
        var revision = new AuthoringRevision(5);
        InputSelectionMemberReadiness selection = Readiness(readiness);
        AuthoringInputSlotStatus status =
            AuthoringInputSlotInspectionService.ProjectReadiness(
                capability,
                revision,
                selection,
                SourceSpace);
        ActiveSessionSnapshot snapshot = CompiledSnapshot(
            capability,
            revision,
            Slot(lifecycle),
            [status]);

        CapabilityActionBlocker? blocker = ActiveSessionBuildBlockerResolver.Resolve(
            snapshot,
            ExperienceIds.StandardMerge);

        Assert.Equal(expectedCode, blocker?.Code);
    }

    private static InputSelectionMemberReadiness Readiness(
        ResolvedChildReadiness readiness,
        string? reason = null)
    {
        return new InputSelectionMemberReadiness(
            SourceSlot,
            IsSelected: readiness == ResolvedChildReadiness.Ready,
            readiness,
            CanSelect: true,
            reason,
            NextAction: null,
            IssueCode: readiness == ResolvedChildReadiness.Blocked
                ? "input.invalid"
                : null);
    }

    private static AuthoringSlotState Slot(AuthoringSlotLifecycle lifecycle)
    {
        string? selectedPath = lifecycle == AuthoringSlotLifecycle.Empty
            ? null
            : "selected.bin";
        FileStamp? stamp = lifecycle is
            AuthoringSlotLifecycle.Verified or AuthoringSlotLifecycle.Warning
                ? FileStamp.FromBytes([0x01])
                : null;
        AuthoringSlotIssueReference? issue = lifecycle == AuthoringSlotLifecycle.Error
            ? new AuthoringSlotIssueReference(
                AuthoringDerivedResultKind.Inspection,
                "inspection",
                "input.invalid")
            : null;
        return new AuthoringSlotState(
            SourceSlot,
            selectedPath,
            stamp,
            lifecycle,
            issue);
    }

    private static AuthoringInputSlotStatus PreCompilationStatus(
        ResolvedCapabilityRoute route,
        AuthoringRevision revision,
        InputSelectionMemberReadiness readiness)
    {
        return new AuthoringInputSlotStatus(
            route.Identity,
            route.ResolutionToken,
            revision,
            route.CapabilityFingerprint,
            compilationFingerprint: null,
            readiness,
            SourceSpace,
            inspectionLifecycle: null,
            fileStamp: null,
            inspection: null);
    }

    private static ActiveSessionSnapshot PreCompilationSnapshot(
        ResolvedCapabilityRoute route,
        AuthoringRevision revision,
        AuthoringSlotState slot,
        IReadOnlyList<AuthoringInputSlotStatus>? statuses = null)
    {
        return Snapshot(
            route.Identity,
            route.ResolutionToken,
            revision,
            route.CapabilityFingerprint,
            compilationFingerprint: null,
            exactCapability: null,
            slot,
            statuses);
    }

    private static ActiveSessionSnapshot CompiledSnapshot(
        ResolvedCapability capability,
        AuthoringRevision revision,
        AuthoringSlotState slot,
        IReadOnlyList<AuthoringInputSlotStatus> statuses)
    {
        return Snapshot(
            capability.Identity,
            capability.ResolutionToken,
            revision,
            capability.CapabilityFingerprint,
            capability.CompiledComposition.CompilationFingerprint,
            capability,
            slot,
            statuses);
    }

    private static ActiveSessionSnapshot Snapshot(
        CapabilityRouteIdentity identity,
        ResolutionToken resolutionToken,
        AuthoringRevision revision,
        string capabilityFingerprint,
        string? compilationFingerprint,
        ResolvedCapability? exactCapability,
        AuthoringSlotState slot,
        IReadOnlyList<AuthoringInputSlotStatus>? statuses)
    {
        return new ActiveSessionSnapshot(
            identity.WorkflowId,
            resolutionToken,
            revision,
            identity.RouteId,
            capabilityFingerprint,
            executionAdmitted: true,
            identity.IcId,
            identity.IcCountVariant,
            identity.MapVariant,
            [identity.IcId],
            [identity.IcCountVariant],
            [slot],
            draftState: null,
            draftCapabilityFingerprint: null,
            derivedPublications: [],
            compilationFingerprint,
            exactCapability,
            statuses);
    }

    private static void AssertBlocker(
        CapabilityActionBlocker? blocker,
        string expectedCode,
        string expectedSubject,
        string expectedMessage)
    {
        CapabilityActionBlocker actual = Assert.IsType<CapabilityActionBlocker>(blocker);
        Assert.Equal(expectedCode, actual.Code);
        Assert.Equal(expectedSubject, actual.SubjectId);
        Assert.Equal(expectedMessage, actual.Message);
    }
}
