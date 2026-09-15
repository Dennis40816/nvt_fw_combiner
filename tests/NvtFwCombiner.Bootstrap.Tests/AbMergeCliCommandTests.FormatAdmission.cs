using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeCliCommandTests
{
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
