using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Regression coverage for a real canonical run with an explicitly injected read failure.</summary>
public sealed class CanonicalInputReadFailureRegressionTests
{
    private const string ExportPathEnvironmentVariable =
        "NVT_FW_COMBINER_ACTUAL_FAILED_RUN_REPORT_PATH";

    /// <summary>
    /// Executes the published NT51929 Standard Merge contract over certified manifest fixtures,
    /// then injects a TP artifact-reader I/O failure. This is not evidence of a user-selected
    /// path changing after immutable authoring acceptance. An explicit environment path may
    /// export the returned report for UI fidelity inspection but is not required for regression coverage.
    /// </summary>
    [Fact]
    public async Task CanonicalNt51929RunReturnsInjectedArtifactReadFailureWithoutOutputAsync()
    {
        System.Text.Json.JsonElement goldenCase = V2StandardMergeGoldenTestSupport.ReadGoldenCase("51929");
        Dictionary<string, byte[]> inputs = V2StandardMergeGoldenTestSupport.ReadInputs(
            goldenCase.GetProperty("inputs"));
        byte[] dp = inputs[CompositionAddressSpaceIds.DpInput];
        byte[] tp = inputs[CompositionAddressSpaceIds.TpInput];

        var host = new IsolatedBootstrapTestHost();
        CapabilityCatalogReloadResult reload = host.Catalog.Reload(TestContext.Current.CancellationToken);
        Assert.True(reload.Succeeded);
        CapabilityResolutionResult resolution = host.Canonical.Catalog.ResolveUniqueRoute(
            "NT51929",
            ExperienceIds.StandardMerge,
            "selector-free");
        Assert.True(resolution.Succeeded);
        Assert.True(host.Canonical.Compiler.TryCompileStandardMerge(
            "NT51929",
            dp.LongLength,
            out CompiledComposition? compiled,
            out IReadOnlyList<CompositionIssue> compileIssues));
        Assert.Empty(compileIssues);
        ResolvedCapability publishedCapability = Assert.IsType<ResolvedCapability>(resolution.Capability);
        Assert.Same(publishedCapability.CompiledComposition, compiled);

        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        CompiledAuthoringSessionPreparation prepared = host.Services.StandardMergeAuthoring.PrepareSession(
            session,
            "NT51929",
            [
                new CompiledAuthoringSelectedInput(CompositionAddressSpaceIds.DpInput, "dp.bin", dp),
                new CompiledAuthoringSelectedInput(CompositionAddressSpaceIds.TpInput, "tp.bin", tp),
            ]);
        Assert.True(
            prepared.Succeeded,
            string.Join(Environment.NewLine, prepared.Issues.Select(static issue => issue.Message)));
        ActiveSessionSnapshot acceptedSession = Assert.IsType<ActiveSessionSnapshot>(prepared.Snapshot);
        ResolvedCapability acceptedCapability = Assert.IsType<ResolvedCapability>(acceptedSession.ExactCapability);
        Assert.Same(compiled, acceptedCapability.CompiledComposition);

        string profileId = acceptedCapability.CompiledComposition.V2Details.ProfileId;
        string dpArtifactId = $"{profileId}:{CompositionAddressSpaceIds.DpInput}";
        string tpArtifactId = $"{profileId}:{CompositionAddressSpaceIds.TpInput}";
        var reader = new InjectedArtifactReadFailureReader(
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [dpArtifactId] = dp,
                [tpArtifactId] = tp,
            },
            tpArtifactId);
        var service = new CompositionRunService(
            reader,
            new FakeClock([
                new DateTimeOffset(2026, 9, 6, 2, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 6, 2, 0, 1, TimeSpan.Zero),
            ]));
        var request = new CompositionRunRequest(
            "ui-issue-card-injected-io",
            acceptedCapability.CompiledComposition,
            [
                new InputArtifactBinding(
                    CompositionAddressSpaceIds.DpInput,
                    "dp-control",
                    dpArtifactId,
                    "dp.bin",
                    CompiledInputArtifactClass.DpFirmware),
                new InputArtifactBinding(
                    CompositionAddressSpaceIds.TpInput,
                    "tp-injected-io",
                    tpArtifactId,
                    "tp.bin",
                    CompiledInputArtifactClass.TpFirmware),
            ],
            acceptedCapability.CompiledComposition.V2Details.OutputNamingRequirement.FileNameTemplate,
            outputNamingInspection: AcceptedOutputNamingInspection.Accept(acceptedSession),
            outputNamingAdmission: OutputNamingAdmissionIdentity.Capture(
                acceptedCapability,
                acceptedSession.AuthoringRevision.Value),
            resolvedCapability: acceptedCapability);

        CompositionRunResult result = await service.PreviewAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.True(result.OutputBytes.IsEmpty);
        Assert.False(result.Report.Output.Committed);
        CompositionIssue issue = Assert.Single(result.Report.Issues);
        Assert.Equal("input.artifact.read-failed", issue.Code);
        Assert.Equal(CompositionIssueSeverity.Error, issue.Severity);
        Assert.Contains("tp-injected-io", issue.Message, StringComparison.Ordinal);
        Assert.Contains("IOException", issue.Message, StringComparison.Ordinal);
        Assert.Equal([dpArtifactId, tpArtifactId], reader.ReadArtifactIds);

        string reportJson = CompositionRunReportJson.Serialize(result);
        Assert.Contains("ui-issue-card-injected-io", reportJson, StringComparison.Ordinal);
        Assert.DoesNotContain(@"D:\\", reportJson, StringComparison.OrdinalIgnoreCase);
        string? outputPath = Environment.GetEnvironmentVariable(ExportPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            string? testAreaRoot = Environment.GetEnvironmentVariable("NFC_TEST_AREA_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(testAreaRoot));
            string expectedOutputPath = Path.Combine(
                testAreaRoot ?? throw new InvalidOperationException(),
                "evidence",
                "v114-issue-card-fidelity",
                "actual-failed-run-report.json");
            Assert.True(StringComparer.OrdinalIgnoreCase.Equals(
                expectedOutputPath,
                Path.GetFullPath(outputPath)));
            await File.WriteAllTextAsync(
                expectedOutputPath,
                reportJson,
                TestContext.Current.CancellationToken);
            Assert.Equal(reportJson, await File.ReadAllTextAsync(
                expectedOutputPath,
                TestContext.Current.CancellationToken));
        }
    }

    private sealed class InjectedArtifactReadFailureReader(
        IReadOnlyDictionary<string, byte[]> artifacts,
        string failingArtifactId) : IArtifactReader
    {
        private readonly IReadOnlyDictionary<string, byte[]> _artifacts = artifacts;
        private readonly string _failingArtifactId = failingArtifactId;

        internal List<string> ReadArtifactIds { get; } = [];

        public ValueTask<ReadOnlyMemory<byte>> ReadAsync(
            string artifactId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadArtifactIds.Add(artifactId);
            return StringComparer.Ordinal.Equals(artifactId, _failingArtifactId)
                ? ValueTask.FromException<ReadOnlyMemory<byte>>(new IOException(
                    "Injected UI-evidence artifact-reader I/O failure."))
                : ValueTask.FromResult<ReadOnlyMemory<byte>>(_artifacts[artifactId]);
        }
    }
}
