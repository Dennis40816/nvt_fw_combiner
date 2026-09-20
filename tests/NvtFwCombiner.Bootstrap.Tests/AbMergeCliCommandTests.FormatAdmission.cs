using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeCliCommandTests
{
    /// <summary>A Config change after readiness is a clean CLI refusal before any execution side effect.</summary>
    [Theory]
    [InlineData("invalid", false)]
    [InlineData("missing", false)]
    [InlineData("changed", false)]
    [InlineData("invalid", true)]
    [InlineData("missing", true)]
    [InlineData("changed", true)]
    public async Task ChangedConfigAfterCliReadinessReturnsFailureAsync(string change, bool build)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-cli-pre-run-refusal");
        CompositionHostServices host = await CreateFormatCliHostAsync(workspace);
        if (change == "missing")
        {
            IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
            Assert.True((await configuration.SaveAsync(
                [.. configuration.CreateDefaultsDraft().Select(static entry => entry! with { RecognitionValues = [] })],
                TestContext.Current.CancellationToken)).Succeeded);
        }
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
            if (change == "missing") { File.Delete(workspace.PathFor("format.json")); }
            if (change == "changed")
            {
                IEventBufferFormatConfigurationSession config = await host.GetEventBufferFormatConfigurationAsync(
                    TestContext.Current.CancellationToken);
                Assert.True((await config.SaveAsync(
                    [.. config.CreateDefaultsDraft().Select(static entry => entry! with { RecognitionValues = [] })],
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
        Assert.Contains(change == "invalid" ? "AB_FORMAT_CONFIGURATION_INVALID" : "AB_FORMAT_CHANGED",
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
    [InlineData("nt51950-ab-merge-desay", 0x84, false)]
    [InlineData("nt51950-ab-merge-common-2ic", 0x97, false)]
    [InlineData("nt51950-ab-merge", 0x84, false)]
    [InlineData("nt51950-ab-merge-desay", 0x97, true)]
    [InlineData("nt51950-ab-merge-common-2ic", 0x84, true)]
    [InlineData("NT51950", 0x97, true)]
    [InlineData("51950", 0x84, true)]
    public async Task ExplicitProfileCannotSilentlySelectAnotherFormatAsync(string selector, byte format, bool succeeds)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-cli-profile-constraint");
        CompositionHostServices host = await CreateFormatCliHostAsync(workspace);
        string[] args = CreateFormatCliArguments(workspace, selector, format);
        CliRunResult result = await CliTestHarness.RunAbAsync(host, args, TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == (succeeds ? 0 : 1), result.Error + result.Output);
        string detected = format == 0x97 ? "nt51950-ab-merge-desay" : "nt51950-ab-merge-common-2ic";
        if (succeeds)
        {
            Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
            Assert.True(File.Exists(workspace.PathFor("report.json")));
        }
        else
        {
            Assert.Contains(selector, result.Error, StringComparison.Ordinal);
            Assert.Contains(detected, result.Error, StringComparison.Ordinal);
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
        return ["preview", "--profile", selector, "--ab-topology", "cascade",
            "--dp-ab", workspace.Write("dp.bin", new byte[0x100000]),
            "--tp-a", workspace.Write("a.bin", tp), "--tp-b", workspace.Write("b.bin", tp),
            "--output", workspace.PathFor("output.bin"), "--report", workspace.PathFor("report.json")];
    }
}
