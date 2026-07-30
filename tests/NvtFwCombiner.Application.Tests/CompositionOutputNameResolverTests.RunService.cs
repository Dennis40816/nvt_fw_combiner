using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionOutputNameResolverTests
{
    /// <summary>The run service commits the name derived from the same accepted input bytes it executes.</summary>
    [Fact]
    public async Task CurrentInspectionCommitsCanonicalAutomaticName()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);
        var accepted = new AcceptedOutputNamingInspection(
            OutputNamingRouteId,
            composition.CompilationFingerprint,
            fixture.Plan,
            fixture.Snapshot);
        var writer = new RecordingOutputWriter();
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["input-artifact"] = fixture.Bytes,
            }),
            new FakeClock([RunTime, RunTime.AddSeconds(1)]),
            writer);
        var request = new CompositionRunRequest(
            "normal-output-current",
            composition,
            [CreateInputBinding()],
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            outputNamingInspection: accepted,
            outputNamingAdmission: CreateAdmission(
                composition,
                fixture));

        CompositionRunResult result = await service.PreviewOrBuildAsync(
            request,
            build: true,
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.True(writer.WasCalled);
        Assert.Equal(
            "NT51929_FlashCode_D8205T8004_20260728.bin",
            writer.FileName);
        Assert.Equal(writer.FileName, result.Report.Output.FileName);
        Assert.NotNull(result.Report.OutputNaming);
    }

    /// <summary>A stale inspection blocks execution and output publication instead of naming different bytes.</summary>
    [Fact]
    public async Task StaleInspectionCannotExecuteOrCommit()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);
        var accepted = new AcceptedOutputNamingInspection(
            OutputNamingRouteId,
            composition.CompilationFingerprint,
            fixture.Plan,
            fixture.Snapshot);
        byte[] changedBytes = [.. fixture.Bytes];
        changedBytes[0] ^= 0x01;
        var writer = new RecordingOutputWriter();
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["input-artifact"] = changedBytes,
            }),
            new FakeClock([RunTime, RunTime.AddSeconds(1)]),
            writer);
        var request = new CompositionRunRequest(
            "normal-output-stale",
            composition,
            [CreateInputBinding()],
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            outputNamingInspection: accepted,
            outputNamingAdmission: CreateAdmission(
                composition,
                fixture));

        CompositionRunResult result = await service.PreviewOrBuildAsync(
            request,
            build: true,
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.False(writer.WasCalled);
        Assert.Null(result.Report.OutputNaming);
        CompositionIssue issue = Assert.Single(
            result.Report.Issues,
            static candidate => candidate.Code == "output-naming.inspection-stale");
        Assert.Equal(CompositionIssueSeverity.Error, issue.Severity);
        Assert.All(
            result.Report.Operations,
            static operation => Assert.Equal(OperationRunStatus.Skipped, operation.Status));
    }

    /// <summary>A naming inspection from a different compiled capability cannot enter a run request.</summary>
    [Fact]
    public void DifferentCapabilityFingerprintIsRejectedAtRequestBoundary()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);

        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "normal-output-wrong-capability",
            composition,
            [CreateInputBinding()],
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            outputNamingInspection: fixture.AcceptedInspection,
            outputNamingAdmission: new OutputNamingAdmissionIdentity(
                fixture.AcceptedInspection.RouteId,
                fixture.AcceptedInspection.CapabilityFingerprint,
                fixture.AcceptedInspection.ResolutionToken,
                fixture.AcceptedInspection.AuthoringRevision)));
    }

    /// <summary>A compiled normal renderer cannot execute without the accepted inspection boundary.</summary>
    [Fact]
    public void NormalRendererRequiresAcceptedInspection()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);

        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "normal-output-missing-inspection",
            composition,
            [CreateInputBinding()],
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template));
    }

    /// <summary>Same bytes and fingerprint cannot carry an inspection across publication or revision boundaries.</summary>
    [Theory]
    [InlineData("publication")]
    [InlineData("revision")]
    [InlineData("route")]
    public void StalePublicationAdmissionIsRejectedAtRequestBoundary(string change)
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);
        var accepted = new AcceptedOutputNamingInspection(
            OutputNamingRouteId,
            composition.CompilationFingerprint,
            fixture.Plan,
            fixture.Snapshot);
        var admission = new OutputNamingAdmissionIdentity(
            change == "route"
                ? "different-output-naming-route"
                : accepted.RouteId,
            composition.CompilationFingerprint,
            change == "publication"
                ? new ResolutionToken("different-output-naming-publication")
                : accepted.ResolutionToken,
            change == "revision"
                ? accepted.AuthoringRevision + 1
                : accepted.AuthoringRevision);

        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            $"normal-output-stale-{change}",
            composition,
            [CreateInputBinding()],
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            outputNamingInspection: accepted,
            outputNamingAdmission: admission));
    }

    /// <summary>A normal-name preview requires a freshly captured matching admission before build.</summary>
    [Fact]
    public void PreviewCannotBeApprovedUnderAChangedPublication()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);
        var accepted = new AcceptedOutputNamingInspection(
            OutputNamingRouteId,
            composition.CompilationFingerprint,
            fixture.Plan,
            fixture.Snapshot);
        var request = new CompositionRunRequest(
            "normal-output-preview-a",
            composition,
            [CreateInputBinding()],
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            outputNamingInspection: accepted,
            outputNamingAdmission: CreateAdmission(composition, fixture));
        var publicationB = new OutputNamingAdmissionIdentity(
            accepted.RouteId,
            composition.CompilationFingerprint,
            new ResolutionToken("output-naming-publication-b"),
            accepted.AuthoringRevision);

        _ = Assert.Throws<InvalidOperationException>(() =>
            request.WithApprovedPreviewToken("preview-a"));
        _ = Assert.Throws<ArgumentException>(() =>
            request.WithApprovedPreviewToken("preview-a", publicationB));
    }

    /// <summary>Report and preview token retain the exact output-naming publication identity.</summary>
    [Fact]
    public async Task PublicationIdentityParticipatesInReportAndPreviewToken()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);
        var acceptedA = new AcceptedOutputNamingInspection(
            OutputNamingRouteId,
            composition.CompilationFingerprint,
            fixture.Plan,
            fixture.Snapshot);
        OutputNamingAdmissionIdentity admissionA =
            CreateAdmission(composition, fixture);
        ResolvedMetadataPlan planB = fixture.Plan.Definition.Resolve(
            new ResolutionToken("output-naming-publication-b"));
        MetadataInspectionSnapshot snapshotB = FirmwareMetadataInspector.Inspect(
            new MetadataInspectionRequest(
                planB,
                fixture.Snapshot.AuthoringRevision,
                [fixture.Artifact]));
        var acceptedB = new AcceptedOutputNamingInspection(
            acceptedA.RouteId,
            composition.CompilationFingerprint,
            planB,
            snapshotB);
        var admissionB = new OutputNamingAdmissionIdentity(
            acceptedA.RouteId,
            composition.CompilationFingerprint,
            planB.ResolutionToken,
            snapshotB.AuthoringRevision);
        CompositionRunRequest requestA = Request(
            "normal-output-publication-a",
            acceptedA,
            admissionA);
        CompositionRunRequest requestB = Request(
            "normal-output-publication-b",
            acceptedB,
            admissionB);

        CompositionRunResult previewA = await Preview(requestA);
        CompositionRunResult previewB = await Preview(requestB);

        Assert.NotEqual(previewA.PreviewToken, previewB.PreviewToken);
        OutputNamingAdmissionSummary summaryA = Assert.IsType<OutputNamingAdmissionSummary>(
            previewA.Report.OutputNaming?.Admission);
        Assert.Equal(admissionA.RouteId, summaryA.RouteId);
        Assert.Equal(admissionA.CapabilityFingerprint, summaryA.CapabilityFingerprint);
        Assert.Equal(admissionA.ResolutionToken.Value, summaryA.ResolutionToken);
        Assert.Equal(admissionA.AuthoringRevision, summaryA.AuthoringRevision);

        CompositionRunRequest Request(
            string runId,
            AcceptedOutputNamingInspection accepted,
            OutputNamingAdmissionIdentity admission)
        {
            return new CompositionRunRequest(
                runId,
                composition,
                [CreateInputBinding()],
                CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
                outputNamingInspection: accepted,
                outputNamingAdmission: admission);
        }

        async Task<CompositionRunResult> Preview(CompositionRunRequest request)
        {
            CompositionRunService service = new(
                new FakeArtifactReader(new Dictionary<string, byte[]>
                {
                    ["input-artifact"] = fixture.Bytes,
                }),
                new FakeClock([RunTime, RunTime.AddSeconds(1)]),
                new RecordingOutputWriter());
            return await service.PreviewOrBuildAsync(
                request,
                build: false,
                CancellationToken.None);
        }
    }
}
