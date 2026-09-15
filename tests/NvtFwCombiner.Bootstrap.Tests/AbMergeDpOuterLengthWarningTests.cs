using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Profile-owned size advice never changes DP source coverage or output bytes.</summary>
public sealed class AbMergeDpOuterLengthWarningTests
{
    private const int Capacity = 0x100000;
    private const string WarningCode = "DESAY_DP_SIZE_WARNING";

    /// <summary>Both real Desay routes warn once for an ignored tail and retain identical complete output.</summary>
    [Theory]
    [InlineData("NT51950", "single")]
    [InlineData("NT51950", "cascade")]
    [InlineData("NT51951", null)]
    public async Task DesayOversizedDpWarnsWithoutChangingBuildAsync(string ic, string? topology)
    {
        using TempWorkspace workspace = TempWorkspace.Create("desay-dp-size");
        CompositionHostServices host = await CreateHostAsync(workspace);
        byte[] exact = [.. Enumerable.Repeat((byte)0x31, Capacity)];
        byte[] oversized = [.. exact, 0xAA, 0x55];
        CompositionRunResult baseline = await BuildAsync(host, workspace, ic, topology, exact, "exact", 0x97, expectedWarning: false);
        CompositionRunResult warned = await BuildAsync(host, workspace, ic, topology, oversized, "oversized", 0x97, expectedWarning: true);
        Assert.Equal(baseline.OutputBytes.ToArray(), warned.OutputBytes.ToArray());
        Assert.Equal(baseline.Report.Output.Sha256, warned.Report.Output.Sha256);
        Assert.Equal(exact, File.ReadAllBytes(workspace.PathFor("exact-dp.bin")));
        Assert.Equal(oversized, File.ReadAllBytes(workspace.PathFor("oversized-dp.bin")));
        InputArtifactSummary input = Assert.Single(warned.Report.Inputs, input => input.AddressSpaceId == "dp-ab-input");
        Assert.Equal(Capacity + 2, input.Size);
        Assert.Equal(new ByteRange(Capacity, 2), input.ExecutionSnapshot!.IgnoredTrailingRange);
    }

    /// <summary>Short Desay coverage and the existing Common exact-size contract remain blocking.</summary>
    [Theory]
    [InlineData("NT51950", "single", 0x97, Capacity - 1, CompositionIssueCodes.InputSourceViewIncomplete)]
    [InlineData("NT51951", null, 0x97, Capacity - 1, CompositionIssueCodes.InputSourceViewIncomplete)]
    [InlineData("NT51950", "cascade", 0x84, Capacity + 2, CompositionIssueCodes.InputAddressSpaceLengthMismatch)]
    [InlineData("NT51951", null, 0x84, Capacity + 2, CompositionIssueCodes.InputAddressSpaceLengthMismatch)]
    public async Task RequiredCoverageAndCommonExactSizeRemainBlockingAsync(string ic, string? topology, byte format, int length, string expectedCode)
    {
        using TempWorkspace workspace = TempWorkspace.Create("desay-dp-short");
        CompositionHostServices host = await CreateHostAsync(workspace);
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(session, ic, topology,
            [new("dp-ab-input", workspace.PathFor("dp.bin"), new byte[length]),
             new("tp-a-input", workspace.PathFor("a.bin"), CreateTp(format, topology == "cascade" ? (byte)2 : (byte)1)),
             new("tp-b-input", workspace.PathFor("b.bin"), CreateTp(format, topology == "cascade" ? (byte)2 : (byte)1))],
            AbMergeDpMode.Normal, TestContext.Current.CancellationToken);
        Assert.False(prepared.Succeeded);
        AuthoringInputSlotStatus dp = Assert.Single(session.CurrentSnapshot!.InputSlotStatuses, status => status.SlotId == "dp-ab-input");
        Assert.Equal(expectedCode, dp.Inspection!.IssueCode);
        Assert.True(dp.Inspection.BlocksBuild);
        Assert.Null(dp.AcceptedBytes);
        Assert.Equal(Capacity, dp.Inspection.RequiredEndExclusive);
    }

    /// <summary>Common gets no new size policy, and Dummy never invents a missing DP advisory.</summary>
    [Theory]
    [InlineData("NT51950", "cascade", false)]
    [InlineData("NT51951", null, false)]
    [InlineData("NT51950", "single", true)]
    [InlineData("NT51951", null, true)]
    public async Task CommonAndDummyDoNotGainDesayDpWarningAsync(string ic, string? topology, bool dummy)
    {
        using TempWorkspace workspace = TempWorkspace.Create("desay-dp-unaffected");
        CompositionHostServices host = await CreateHostAsync(workspace);
        CompositionRunResult result = await BuildAsync(host, workspace, ic, topology,
            dummy ? null : new byte[Capacity], "unaffected", dummy ? (byte)0x97 : (byte)0x84, expectedWarning: false);
        Assert.DoesNotContain(result.Report.Issues, issue => issue.Code == WarningCode);
        Assert.Equal(dummy ? "desay" : "common", result.Report.AbMergeFormat!.FormatId);
    }

    /// <summary>All enclosing-bundle route pins remain exact, without changing publication/evidence decisions.</summary>
    [Fact]
    public void AbBundleRoutePinsMatchCanonicalSource()
    {
        CanonicalCapabilityPolicyRoute[] routes = [.. BuiltInCanonicalCapabilityPolicy.Load().Routes.Where(route =>
            route.Identity.WorkflowId == ExperienceIds.AbMerge && route.Identity.IcId is "NT51950" or "NT51951")];
        Assert.NotEmpty(routes);
        (CanonicalCapabilityPolicyRoute Policy, CanonicalDynamicRoute Actual)[] pairs =
            [.. routes.Select(route => (route, CanonicalDynamicRouteInventory.Resolve(route.Identity)))];
        foreach ((CanonicalCapabilityPolicyRoute policy, CanonicalDynamicRoute actual) in pairs)
        {
            TestContext.Current.TestOutputHelper!.WriteLine($"PIN {policy.Identity.RouteId} {actual.CapabilityFingerprint}");
        }
        Assert.All(pairs, pair => Assert.Equal(pair.Policy.CapabilityFingerprint, pair.Actual.CapabilityFingerprint));
    }

    private static async Task<CompositionHostServices> CreateHostAsync(TempWorkspace workspace)
    {
        var environment = new ExternalProcessorEnvironmentLoader(RepositoryPaths.FromRepositoryRoot("external-tools"));
        Assert.True((await ((IExternalProcessorEnvironmentLoader)environment)
            .LoadToCompletionAsync(null, TestContext.Current.CancellationToken)).Succeeded);
        CompositionHostServices host = CompositionHostServices.Create(environment, loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        return host;
    }

    private static async Task<CompositionRunResult> BuildAsync(CompositionHostServices host, TempWorkspace workspace,
        string ic, string? topology, byte[]? dpBytes, string prefix, byte format, bool expectedWarning)
    {
        byte count = topology == "cascade" ? (byte)2 : (byte)1;
        var inputs = new List<CompiledAuthoringSelectedInput>
        {
            new("tp-a-input", workspace.Write($"{prefix}-a.bin", CreateTp(format, count)), CreateTp(format, count)),
            new("tp-b-input", workspace.Write($"{prefix}-b.bin", CreateTp(format, count)), CreateTp(format, count)),
        };
        if (dpBytes is not null) { inputs.Add(new("dp-ab-input", workspace.Write($"{prefix}-dp.bin", dpBytes), dpBytes)); }
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(
            new AuthoringSessionState(ExperienceIds.AbMerge), ic, topology, inputs,
            dpBytes is null ? AbMergeDpMode.Dummy : AbMergeDpMode.Normal, TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues.Select(issue => issue.Code)));
        if (dpBytes is not null)
        {
            CompiledInputArtifactInspectionResult inspection = Assert.Single(prepared.Snapshot!.InputSlotStatuses,
                status => status.SlotId == "dp-ab-input").Inspection!;
            Assert.Equal(expectedWarning, inspection.Severity == CompiledInputArtifactInspectionSeverity.Warning);
            Assert.False(inspection.BlocksBuild);
            Assert.Equal(dpBytes.Length, inspection.ActualLength);
            if (format == 0x97)
            {
                long compiledCapacity = prepared.Snapshot!.ExactCapability!.CompiledComposition.Plan.OutputInitialization.Capacity;
                Assert.Equal(Capacity, compiledCapacity);
                Assert.Equal([compiledCapacity], inspection.ExpectedOuterLengths);
            }
            if (expectedWarning) { Assert.Equal(WarningCode, inspection.IssueCode); }
        }
        CapabilityActionReadinessSnapshot readiness = (await host.AbMergeAuthoring.GetActionReadinessAsync(
            prepared.Snapshot!, TestContext.Current.CancellationToken))!;
        Assert.True(readiness.Build.IsAvailable);
        Assert.True(readiness.Preview.IsAvailable);
        var request = new AcceptedCompositionExecutionRequest(prepared.Snapshot!,
            inputs.ToDictionary(input => input.SlotId, input => input.SelectedPathHint), build: true,
            outputPath: workspace.PathFor($"{prefix}-output.bin"), actionReadiness: readiness);
        CompositionRunResult result = await host.CompositionExecution.ExecuteAsync(request,
            new CompositionRunProgressFeed(), TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        Assert.True(result.Report.Output.Committed);
        Assert.Equal(expectedWarning ? 1 : 0, result.Report.Issues.Count(issue => issue.Code == WarningCode));
        if (expectedWarning)
        {
            CompositionIssue warning = Assert.Single(result.Report.Issues, issue => issue.Code == WarningCode);
            Assert.Equal("dp-ab-input", warning.OperationId);
            Assert.Contains("1048578 bytes", warning.Message, StringComparison.Ordinal);
            Assert.Contains("expected 0x100000", warning.Message, StringComparison.Ordinal);
        }
        using JsonDocument json = JsonDocument.Parse(CompositionRunReportJson.Serialize(result));
        JsonElement[] warnings = [.. json.RootElement.GetProperty("Issues").EnumerateArray().Where(issue => issue.GetProperty("Code").GetString() == WarningCode)];
        Assert.Equal(expectedWarning ? 1 : 0, warnings.Length);
        if (expectedWarning) { Assert.Equal(CompositionIssueSeverity.Warning, warnings[0].GetProperty("Severity").GetString()); }
        return result;
    }

    private static byte[] CreateTp(byte format, byte count)
    {
        byte[] bytes = new byte[0x37000];
        bytes[0x22200] = 0x31;
        bytes[0x22201] = 0xCE;
        bytes[0x2220C] = format;
        bytes[0x36000] = 0x42;
        bytes[0x36001] = 0xBD;
        bytes[0x36017] = count;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(bytes, 0x36FFC);
        return bytes;
    }
}
