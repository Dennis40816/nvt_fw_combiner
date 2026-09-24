using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeCliCommandTests
{
    /// <summary>Invalid Config or mismatched formats after readiness refuse before execution side effects.</summary>
    [Theory]
    [InlineData("invalid", false)]
    [InlineData("mismatch", false)]
    [InlineData("invalid", true)]
    [InlineData("mismatch", true)]
    public async Task ChangedConfigAfterCliReadinessReturnsFailureAsync(string change, bool build)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-cli-pre-run-refusal");
        CompositionHostServices host = await CreateFormatCliHostAsync(workspace);
        string[] args = CreateFormatCliArguments(workspace, "NT51950", 0x97);
        args[0] = build ? "build" : "preview";
        int acquisitions = 0;
        var destinations = new CliRefusalDestinations();
        var execution = new CompositionExecutionExperience(host.Catalog, destinations, () =>
        {
            acquisitions++;
            throw new InvalidOperationException("Rejected configuration must not acquire a processor.");
        }, static _ => false, new FakeClock([]), (AbMergeAuthoringExperience)host.AbMergeAuthoring);
        var changingExecution = new BeforeCliExecution(execution, async () =>
        {
            if (change == "invalid") { _ = workspace.Write("format.json", "invalid json"u8.ToArray()); }
            if (change == "mismatch")
            {
                IEventBufferFormatConfigurationSession config = await host.GetEventBufferFormatConfigurationAsync(
                    TestContext.Current.CancellationToken);
                Assert.True((await config.SaveAsync(
                    [.. config.CreateDefaultsDraft().Select(static entry => entry! with { RecognitionValues = [0x97] })],
                    TestContext.Current.CancellationToken)).Succeeded);
            }
        });
        var services = new CliCompositionServices(host.CompositionCapabilityExperience, host.SavedRuleAuthoring,
            host.StandardMergeAuthoring, host.AbMergeAuthoring, host.CtrlRamAuthoring,
            host.GeneralAuthoring, host.CompositionOutputNaming, changingExecution);
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = await AbMergeCliCommandHandler.RunAsync(services, host.LocalFiles, args,
            output, error, TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Equal(1, changingExecution.Calls);
        Assert.Contains(change == "invalid" ? "AB_FORMAT_CONFIGURATION_INVALID" : "AB_FORMAT_MISMATCH",
            error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, acquisitions);
        Assert.Equal(0, destinations.Calls);
        Assert.Empty(output.ToString());
        Assert.False(File.Exists(workspace.PathFor("output.bin")));
        Assert.False(File.Exists(workspace.PathFor("report.json")));
    }

    private sealed class BeforeCliExecution(ICompositionExecution inner, Func<Task> before) : ICompositionExecution
    {
        internal int Calls { get; private set; }

        public async ValueTask<CompositionRunResult> ExecuteAsync(AcceptedCompositionExecutionRequest request,
            CompositionRunProgressFeed progress, CancellationToken cancellationToken)
        {
            Assert.True(request.Build ? request.ActionReadiness!.Build.IsAvailable : request.ActionReadiness!.Preview.IsAvailable);
            Calls++;
            await before();
            return await inner.ExecuteAsync(request, progress, cancellationToken);
        }
    }

    private sealed class CliRefusalDestinations : ICompositionExecutionDestinationProvider
    {
        internal int Calls { get; private set; }

        public CompositionExecutionDestination Prepare(CompositionExecutionDestinationRequest request)
        {
            Calls++;
            throw new InvalidOperationException("Rejected configuration must not prepare a destination.");
        }
    }

    /// <summary>Explicit profile IDs constrain admission; IC selectors retain automatic format selection.</summary>
    [Theory]
    [InlineData("nt51950-ab-merge-desay", 0x84, 64)]
    [InlineData("nt51950-ab-merge-desay", 0x97, 64)]
    [InlineData("nt51950-ab-merge-common-2ic", 0x84, 64)]
    [InlineData("nt51950-ab-merge-common-2ic", 0x97, 64)]
    [InlineData("nt51950-ab-merge", 0x84, 1)]
    [InlineData("nt51950-ab-merge", 0x97, 1)]
    [InlineData("nt51950-ab-merge-cascade", 0x84, 0)]
    [InlineData("nt51950-ab-merge-cascade", 0x97, 0)]
    [InlineData("NT51950", 0x97, 0)]
    [InlineData("51950", 0x84, 0)]
    public async Task ExplicitProfileCannotSilentlySelectAnotherFormatAsync(string selector, byte format, int expectedExit)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-cli-profile-constraint");
        CompositionHostServices host = await CreateFormatCliHostAsync(workspace);
        string[] args = CreateFormatCliArguments(workspace, selector, format);
        CliRunResult result = await CliTestHarness.RunAbAsync(host, args, TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == expectedExit, result.Error + result.Output);
        if (expectedExit == 0)
        {
            Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
            Assert.True(File.Exists(workspace.PathFor("report.json")));
        }
        else
        {
            Assert.Contains(selector, result.Error, StringComparison.Ordinal);
            if (expectedExit == 1) { Assert.Contains("nt51950-ab-merge-cascade", result.Error, StringComparison.Ordinal); }
            Assert.DoesNotContain("Status: Succeeded", result.Output, StringComparison.Ordinal);
            Assert.False(File.Exists(workspace.PathFor("report.json")));
        }
        Assert.False(File.Exists(workspace.PathFor("output.bin")));
    }

    private static async Task<CompositionHostServices> CreateFormatCliHostAsync(TempWorkspace workspace)
    {
        CompositionHostServices host = CompositionHostServices.Create(
            new ExternalProcessorEnvironmentLoader(RepositoryPaths.FromRepositoryRoot("external-tools")),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        Assert.True((await host.ExternalEnvironmentLoader.LoadToCompletionAsync(null,
            TestContext.Current.CancellationToken)).Succeeded);
        IEventBufferFormatConfigurationSession config = await host.GetEventBufferFormatConfigurationAsync(
            TestContext.Current.CancellationToken);
        Assert.True((await config.SaveAsync(config.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        return host;
    }

    private static string[] CreateFormatCliArguments(TempWorkspace workspace, string selector, byte format)
    {
        byte[] tp = new byte[0x37000];
        tp[0x22200] = 0x31;
        tp[0x22201] = 0xCE;
        tp[0x2220C] = format;
        tp[0x36000] = 0x42;
        tp[0x36001] = 0xBD;
        tp[0x36017] = 2;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(tp, 0x36FFC);
        byte[] tpB = (byte[])tp.Clone();
        tpB[0x2220C] = format == 0x97 ? (byte)0xA6 : format;
        return ["preview", "--profile", selector, "--ab-topology", "cascade",
            "--dp-ab", workspace.Write("dp.bin", new byte[0x100000]),
            "--tp-a", workspace.Write("a.bin", tp), "--tp-b", workspace.Write("b.bin", tpB),
            "--output", workspace.PathFor("output.bin"), "--report", workspace.PathFor("report.json")];
    }
}
