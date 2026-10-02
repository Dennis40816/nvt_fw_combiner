using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeCliCommandTests
{
    /// <summary>Explicit DP modes reach the shared executor with the Application-owned input contract.</summary>
    [Theory]
    [InlineData("preview", true, "dummy")]
    [InlineData("build", true, "dummy")]
    [InlineData("build", true, " DuMmY ")]
    [InlineData("preview", false, "normal")]
    [InlineData("preview", false, " NoRmAl ")]
    public async Task ExplicitDpModeReachesApplicationInputContractAsync(string action, bool dummy, string modeToken)
    {
        using var workspace = TempWorkspace.Create("ab-cli-dummy");
        CompositionHostServices host = BootstrapTestHost.Services;
        var execution = new CliChoiceRecordingExecution(host.CompositionExecution);
        var services = new CliCompositionServices(host.CompositionCapabilityExperience, host.SavedRuleAuthoring,
            host.StandardMergeAuthoring, host.AbMergeAuthoring, host.CtrlRamAuthoring,
            host.GeneralAuthoring, host.CompositionOutputNaming, execution);
        string outputPath = workspace.PathFor("dummy.bin");
        string[] modeOptions = dummy
            ? ["--dp-mode", modeToken, "--acknowledge-non-tp-ff"]
            : ["--dp-mode", modeToken, "--dp-ab", workspace.Write("dp.bin", new byte[0x80000])];
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exit = await AbMergeCliCommandHandler.RunAsync(services, host.LocalFiles,
            [action, "--profile", "NT51929", .. modeOptions,
                "--tp-a", workspace.Write("tp-a.bin", CreateTp(0x80, 0)),
                "--tp-b", workspace.Write("tp-b.bin", CreateTp(0x81, 2)), "--output", outputPath],
            output, error, TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        AcceptedCompositionExecutionRequest request = Assert.Single(execution.Requests);
        Assert.Equal(action == "build", request.Build);
        string[] expectedSlots = dummy
            ? [CompositionAddressSpaceIds.TpAInput, CompositionAddressSpaceIds.TpBInput]
            : [CompositionAddressSpaceIds.DpAbInput, CompositionAddressSpaceIds.TpAInput, CompositionAddressSpaceIds.TpBInput];
        Assert.Equal(expectedSlots,
            request.AcceptedSession.Slots.Select(static slot => slot.DefinitionId).Order(StringComparer.Ordinal));
        if (dummy)
        {
            Assert.Equal((byte)0xFF, request.AcceptedSession.ExactCapability!.CompiledComposition.Plan.OutputInitialization.FillByte);
            Assert.DoesNotContain(CompositionAddressSpaceIds.DpAbInput, request.SlotPaths.Keys);
        }

        Assert.Equal(action == "build", File.Exists(outputPath));
    }

    /// <summary>Missing acknowledgement, invalid modes and duplicate options refuse without output or JSON.</summary>
    [Theory]
    [InlineData(1, "--dp-mode", "dummy")]
    [InlineData(64, "--dp-mode", "unknown")]
    [InlineData(64, "--acknowledge-non-tp-ff")]
    [InlineData(64, "--dp-mode", "dummy", "--dp-mode", "dummy")]
    [InlineData(64, "--dp-mode", "dummy", "--acknowledge-non-tp-ff", "--acknowledge-non-tp-ff")]
    [InlineData(64, "--bank", "a")]
    public async Task DummyOptionRefusalsWriteNoArtifactsAsync(int expectedExit, params string[] options)
    {
        using var workspace = TempWorkspace.Create("ab-cli-dummy-refusal");
        string bin = workspace.PathFor("must-not-exist.bin");
        string report = workspace.PathFor("must-not-exist.json");
        CliRunResult result = await CliTestHarness.RunAsync(
            ["ab-merge", "build", "--profile", "NT51929", .. options, "--output", bin, "--report", report],
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedExit, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.StartsWith("error:", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(bin));
        Assert.False(File.Exists(report));
        if (expectedExit == 1)
        {
            Assert.Contains("0xFF replaces every non-TP output", result.Error, StringComparison.Ordinal);
        }
    }

    /// <summary>Dummy uses Application input applicability, so DP is rejected and both TP sources remain required.</summary>
    [Theory]
    [InlineData(true, "--dp-ab is not used with --dp-mode dummy")]
    [InlineData(false, "--tp-b is required")]
    public async Task DummyKeepsApplicationInputContractAsync(bool includeDp, string expectedError)
    {
        using var workspace = TempWorkspace.Create("ab-cli-dummy-inputs");
        string report = workspace.PathFor("report.json");
        string bin = workspace.PathFor("output.bin");
        string[] inputOptions = includeDp
            ? ["--dp-ab", workspace.PathFor("unread-dp.bin"), "--tp-b", workspace.PathFor("unread-b.bin")]
            : [];
        CliRunResult result = await CliTestHarness.RunAsync(
            ["ab-merge", "build", "--profile", "NT51929", "--dp-mode", "dummy", "--acknowledge-non-tp-ff",
                "--tp-a", workspace.PathFor("unread-a.bin"), .. inputOptions, "--output", bin, "--report", report],
            TestContext.Current.CancellationToken);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains(expectedError, result.Error, StringComparison.Ordinal);
        Assert.Empty(result.Output);
        Assert.False(File.Exists(bin));
        Assert.False(File.Exists(report));
    }
}
