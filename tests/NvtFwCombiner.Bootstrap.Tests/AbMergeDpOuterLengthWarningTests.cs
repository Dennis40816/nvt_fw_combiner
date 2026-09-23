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
    private const string WarningCode = "DP_NONSTANDARD_SIZE_WARNING";

    /// <summary>Both recognized formats retain the complete DP outside the two declared TP overlays.</summary>
    [Theory]
    [InlineData("NT51950", "single", 0x84, 0x80001, 0x40000, 1)]
    [InlineData("NT51950", "single", 0x97, 0x80001, 0x40000, 1)]
    [InlineData("NT51950", "single", 0x97, 0x100000, 0x40000, 1)]
    [InlineData("NT51950", "cascade", 0x84, 0x100001, 0x80000, 2)]
    [InlineData("NT51950", "cascade", 0x97, 0x100001, 0x80000, 2)]
    [InlineData("NT51950", "cascade", 0x84, 0x100001, 0x80000, 3)]
    [InlineData("NT51951", null, 0x84, 0x100001, 0x80000, 1)]
    [InlineData("NT51951", null, 0x97, 0x100001, 0x80000, 1)]
    public async Task NonstandardNormalDpBuildsFromCompleteCapturedSourceAsync(
        string ic, string? topology, byte format, int length, int bankLength, byte count)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-nonstandard-dp");
        CompositionHostServices host = await CreateHostAsync(workspace);
        byte[] dp = [.. Enumerable.Repeat((byte)0x31, length)];
        dp[^1] = 0xA7;
        CompositionRunResult result = await BuildAsync(host, workspace, ic, topology, dp,
            "nonstandard", format, expectedWarning: true, tpCount: count);
        Assert.Equal(length, result.OutputBytes.Length);
        Assert.Equal(dp.AsSpan(0, 0xA000).ToArray(), result.OutputBytes.Span[..0xA000].ToArray());
        Assert.Equal(dp.AsSpan(0x37000, bankLength - 0x37000 + 0xA000).ToArray(),
            result.OutputBytes.Span.Slice(0x37000, bankLength - 0x37000 + 0xA000).ToArray());
        Assert.Equal(dp.AsSpan(bankLength + 0x37000).ToArray(),
            result.OutputBytes.Span[(bankLength + 0x37000)..].ToArray());
        Assert.Equal((byte)0xA7, result.OutputBytes.Span[^1]);
    }

    /// <summary>Equal lengths cannot cause one accepted DP payload to be reused for another.</summary>
    [Fact]
    public async Task EqualLengthDifferentDpPayloadsKeepTheirOwnTailAsync()
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-captured-dp-identity");
        CompositionHostServices host = await CreateHostAsync(workspace);
        byte[] first = [.. Enumerable.Repeat((byte)0x31, 0x80001)];
        byte[] second = [.. first];
        first[^1] = 0xA7;
        second[^1] = 0x5C;
        CompositionRunResult firstResult = await BuildAsync(host, workspace, "NT51950", "single",
            first, "first", 0x84, expectedWarning: true);
        CompositionRunResult secondResult = await BuildAsync(host, workspace, "NT51950", "single",
            second, "second", 0x84, expectedWarning: true);
        Assert.Equal((byte)0xA7, firstResult.OutputBytes.Span[^1]);
        Assert.Equal((byte)0x5C, secondResult.OutputBytes.Span[^1]);
        Assert.Equal(firstResult.OutputBytes.Span[..^1].ToArray(),
            secondResult.OutputBytes.Span[..^1].ToArray());
    }

    /// <summary>Both recognized formats use exact Common DP capacity and produce a complete output.</summary>
    [Theory]
    [InlineData("NT51950", "single", 0x84, 0x80000)]
    [InlineData("NT51950", "single", 0x97, 0x80000)]
    [InlineData("NT51950", "cascade", 0x84, 0x100000)]
    [InlineData("NT51950", "cascade", 0x97, 0x100000)]
    [InlineData("NT51951", null, 0x84, 0x100000)]
    [InlineData("NT51951", null, 0x97, 0x100000)]
    public async Task BothFormatsBuildWithExactCommonDpCapacityAsync(string ic, string? topology, byte format, int capacity)
    {
        using TempWorkspace workspace = TempWorkspace.Create("common-dp-capacity");
        CompositionHostServices host = await CreateHostAsync(workspace);
        byte[] exact = [.. Enumerable.Repeat((byte)0x31, capacity)];
        CompositionRunResult result = await BuildAsync(host, workspace, ic, topology, exact,
            "exact", format, expectedWarning: false);
        Assert.Equal(capacity, result.OutputBytes.Length);
        Assert.Equal(exact, File.ReadAllBytes(workspace.PathFor("exact-dp.bin")));
        InputArtifactSummary input = Assert.Single(result.Report.Inputs, input => input.AddressSpaceId == "dp-ab-input");
        Assert.Equal(capacity, input.Size);
        Assert.Null(input.ExecutionSnapshot!.IgnoredTrailingRange);
    }

    /// <summary>Any DP shorter than the fixed two-bank transport remains a blocking input error.</summary>
    [Theory]
    [InlineData("NT51950", "single", 0x84, 0x80000, -1)]
    [InlineData("NT51950", "single", 0x97, 0x80000, -1)]
    [InlineData("NT51950", "cascade", 0x84, 0x100000, -1)]
    [InlineData("NT51950", "cascade", 0x97, 0x100000, -1)]
    [InlineData("NT51951", null, 0x84, 0x100000, -1)]
    [InlineData("NT51951", null, 0x97, 0x100000, -1)]
    public async Task ShortDpCapacityBlocksForBothFormatsAsync(string ic, string? topology, byte format, int capacity, int delta)
    {
        using TempWorkspace workspace = TempWorkspace.Create("common-dp-boundary");
        CompositionHostServices host = await CreateHostAsync(workspace);
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(session, ic, topology,
            [new("dp-ab-input", workspace.PathFor("dp.bin"), new byte[capacity + delta]),
             new("tp-a-input", workspace.PathFor("a.bin"), CreateTp(format, topology == "cascade" ? (byte)2 : (byte)1)),
             new("tp-b-input", workspace.PathFor("b.bin"), CreateTp(format, topology == "cascade" ? (byte)2 : (byte)1))],
            AbMergeDpMode.Normal, TestContext.Current.CancellationToken);
        Assert.False(prepared.Succeeded);
        Assert.Contains(prepared.Issues, issue => issue.Code == "profile.v2.plan.invalid-view" &&
            issue.Message.Contains("b-bank-output", StringComparison.Ordinal));
        Assert.Null(prepared.Snapshot?.ExactCapability);
        Assert.Empty(session.CurrentSnapshot?.InputSlotStatuses ?? []);
    }

    /// <summary>Exact-size DP does not warn, and Dummy never invents a missing DP advisory.</summary>
    [Theory]
    [InlineData("NT51950", "cascade", false)]
    [InlineData("NT51951", null, false)]
    [InlineData("NT51950", "single", true)]
    [InlineData("NT51951", null, true)]
    public async Task ExactDpAndDummyDoNotWarnAboutSizeAsync(string ic, string? topology, bool dummy)
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
    public void AffectedBundleRoutePinsMatchCanonicalSource()
    {
        CanonicalCapabilityPolicyRoute[] routes = [.. BuiltInCanonicalCapabilityPolicy.Load().Routes.Where(route =>
            (route.Identity.WorkflowId is ExperienceIds.AbMerge or ExperienceIds.StandardMerge or ExperienceIds.CtrlRamReplace) &&
            (route.Identity.IcId is "NT51950" or "NT51951"))];
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
        string ic, string? topology, byte[]? dpBytes, string prefix, byte format, bool expectedWarning,
        byte? tpCount = null)
    {
        byte count = tpCount ?? (topology == "cascade" ? (byte)2 : (byte)1);
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
            long templateCapacity = ic == "NT51950" && topology == "single" ? 0x80000 : Capacity;
            long compiledCapacity = prepared.Snapshot!.ExactCapability!.CompiledComposition.Plan.OutputInitialization.Capacity;
            Assert.Equal(expectedWarning ? dpBytes.Length : templateCapacity, compiledCapacity);
            Assert.Equal([templateCapacity], inspection.ExpectedOuterLengths);
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
            SourceEnvelopeRunSummary envelope = Assert.IsType<SourceEnvelopeRunSummary>(result.Report.SourceEnvelope);
            Assert.Equal("dp-ab-input", envelope.SourceSlotId);
            Assert.Equal(ic == "NT51950" && topology == "single" ? 0x80000 : Capacity,
                envelope.LayoutTemplateCapacity);
            Assert.Equal(dpBytes!.Length, envelope.ActualOutputLength);
        }
        else
        {
            Assert.Null(result.Report.SourceEnvelope);
        }
        if (expectedWarning)
        {
            CompositionIssue warning = Assert.Single(result.Report.Issues, issue => issue.Code == WarningCode);
            Assert.Equal("dp-ab-input", warning.OperationId);
            Assert.Contains($"{dpBytes!.Length} bytes", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"expected 0x{(ic == "NT51950" && topology == "single" ? 0x80000 : Capacity):X}",
                warning.Message, StringComparison.Ordinal);
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
