using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Composition;

/// <summary>Contracts for non-executing POSTBUILD-unavailable Preview.</summary>
public sealed class GeneralReplaceDiagnosticPreviewTests
{
    private static readonly ResolutionToken Token = new("diagnostic:1");

    /// <summary>Missing Parent stage remains distinct and projects complete Kept/Changed coverage.</summary>
    [Fact]
    public void MissingParentStageProjectsPlanOnlyCoverage()
    {
        GeneralAuthoringAdmissionResult admission = CreateAdmission();
        CapabilityActionReadinessSnapshot readiness = CreateReadiness(
            executionAdmitted: false,
            new CapabilityActionBlocker(
                CapabilityActionReadinessIssueCodes
                    .PostbuildStageAuthorityMissing,
                CapabilityReadinessDimension.Execution,
                "parent-profile",
                "The exact Parent omits POSTBUILD.",
                CapabilityReadinessNextAction.ReviewCompilation),
            []);

        GeneralReplaceDiagnosticPreviewSummary result =
            GeneralReplaceDiagnosticPreviewProjector.Project(
                referenceCapacity: 20,
                admission,
                readiness,
                requiredStageId: null);

        Assert.True(readiness.Preview.IsAvailable);
        Assert.False(readiness.Build.IsAvailable);
        Assert.Same(readiness.Build.PrimaryBlocker, result.Blocker);
        Assert.Equal("diagnostic-plan-only", result.Mode);
        Assert.False(result.OutputProduced);
        Assert.False(result.ClaimsFinalIntegrity);
        Assert.Null(result.RequiredStageId);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes
                .PostbuildStageAuthorityMissing,
            result.Blocker.Code);
        Assert.Equal(
            [
                new PlanOnlyCoverageSegment(
                    new ByteRange(0, 10),
                    PlanOnlyCoverageDisposition.Kept,
                    null),
                new PlanOnlyCoverageSegment(
                    new ByteRange(10, 2),
                    PlanOnlyCoverageDisposition.Changed,
                    "write"),
                new PlanOnlyCoverageSegment(
                    new ByteRange(12, 8),
                    PlanOnlyCoverageDisposition.Kept,
                    null),
            ],
            result.Coverage);
    }

    /// <summary>A declared stage with a missing tool uses the runtime blocker and retains its stage id.</summary>
    [Fact]
    public void MissingRuntimeToolRetainsCompiledStageAndSharedBlocker()
    {
        GeneralAuthoringAdmissionResult admission = CreateAdmission();
        var dependency = RuntimeDependencyEntry.Blocked(
            "general-postbuild",
            "legacy-combiner-1.13.0",
            "external-tool.executable.missing",
            "The required tool is unavailable.");
        CapabilityActionReadinessSnapshot readiness = CreateReadiness(
            executionAdmitted: true,
            executionBlocker: null,
            [dependency]);

        GeneralReplaceDiagnosticPreviewSummary result =
            GeneralReplaceDiagnosticPreviewProjector.Project(
                20,
                admission,
                readiness,
                requiredStageId: "general-postbuild");

        Assert.Equal("general-postbuild", result.RequiredStageId);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.RuntimeDependencyBlocked,
            result.Blocker.Code);
        Assert.Same(readiness.Build.PrimaryBlocker, result.Blocker);
    }

    private static GeneralAuthoringAdmissionResult CreateAdmission()
    {
        var row = new GeneralMappingDraftRow(
            "write",
            ExplicitMappingOperationKind.ReplaceRange,
            GeneralMappingSource.HexOverwrite("AABB"),
            new ByteRange(0, 2),
            CompositionAddressSpaceIds.OutputImage,
            new ByteRange(10, 2),
            OverlapPolicy.Reject,
            alignment: 1,
            "Diagnostic mapping.");
        return GeneralAuthoringAdmission.Evaluate(
            new GeneralMappingDraftState([row]),
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.OutputImage] = 20,
            },
            [],
            GeneralAuthoringTechnicalLimits.Default,
            new GeneralTrustedParentResourcePolicy(
                "parent-profile",
                GeneralAuthoringTechnicalLimits.Default),
            savedRule: null);
    }

    private static CapabilityActionReadinessSnapshot CreateReadiness(
        bool executionAdmitted,
        CapabilityActionBlocker? executionBlocker,
        IReadOnlyList<RuntimeDependencyEntry> dependencies)
    {
        var admission = new CapabilityAdmissionSnapshot(
            "general-replace:diagnostic",
            new string('a', 64),
            new string('b', 64),
            Token,
            new AuthoringRevision(1),
            CapabilityAuthoringAvailability.Available,
            executionAdmitted,
            CapabilityEvidenceStatus.Missing,
            CapabilityPublicationStatus.Internal,
            executionBlocker);
        var runtime = new RuntimeDependencyReadinessSnapshot(
            admission.RouteId,
            admission.CapabilityFingerprint,
            admission.CompilationFingerprint,
            admission.ResolutionToken,
            admission.AuthoringRevision,
            generation: 1,
            DateTimeOffset.UnixEpoch,
            dependencies);
        return CapabilityActionReadinessResolver.Resolve(
            admission,
            [
                new CapabilityChildReadiness(
                    "reference",
                    ResolvedChildReadiness.Ready),
            ],
            runtime,
            currentRuntimeDependencyGeneration: 1);
    }
}
