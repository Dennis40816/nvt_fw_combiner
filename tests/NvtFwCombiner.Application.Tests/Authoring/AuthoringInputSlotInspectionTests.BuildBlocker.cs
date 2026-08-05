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
}
