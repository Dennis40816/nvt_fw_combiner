using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeRuntimeAdmissionTests
{
    /// <summary>
    /// The secondary A FlashCode destination is checked against the primary name rendered from the
    /// accepted execution snapshots, rather than only against the native dialog's stale suggestion.
    /// </summary>
    [Fact]
    public async Task AFlashCodeAliasWithRenderedAutomaticPrimaryBlocksBeforeBuildAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-a-rendered-primary-alias");
        Dictionary<string, string> paths = WriteInputs(workspace);
        string dpPath = paths[CompositionAddressSpaceIds.DpAbInput];
        byte[] dp = await File.ReadAllBytesAsync(dpPath, TestContext.Current.CancellationToken);
        WriteCmi(dp, bankStart: 0, major: 0x06, minor: 0x05, jira: 0x123);
        WriteCmi(dp, bankStart: TpLength, major: 0x07, minor: 0x08, jira: 0x456);
        await File.WriteAllBytesAsync(dpPath, dp, TestContext.Current.CancellationToken);
        string staleSuggestedName = (await AbMergeTestSupport.PrepareOutputAsync(BootstrapTestHost.Services,
            "NT51929",
            paths,
            TestContext.Current.CancellationToken)).OutputName.FileName;

        WriteCmi(dp, bankStart: 0, major: 0x0A, minor: 0x01, jira: 0x123);
        await File.WriteAllBytesAsync(dpPath, dp, TestContext.Current.CancellationToken);
        string renderedName = (await AbMergeTestSupport.PrepareOutputAsync(BootstrapTestHost.Services,
            "NT51929",
            paths,
            TestContext.Current.CancellationToken)).OutputName.FileName;
        string stalePrimaryPath = workspace.PathFor($"output/{staleSuggestedName}");
        string aFlashCodePath = workspace.PathFor($"output/{renderedName}");
        var progress = new CompositionRunProgressFeed();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            AbMergeTestSupport.RunAsync(BootstrapTestHost.Services,
                "NT51929",
                paths,
                build: true,
                progress: progress,
                cancellationToken: TestContext.Current.CancellationToken,
                outputPath: stalePrimaryPath,
                topologySelection: null,
                aFlashCodeOutputPath: aFlashCodePath,
                outputPathUsesAutomaticName: true).AsTask());

        Assert.Contains("A FlashCode output", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(stalePrimaryPath));
        Assert.False(File.Exists(aFlashCodePath));
        var phases = new List<CompositionRunPhase>();
        await foreach (CompositionRunProgressSnapshot snapshot in progress.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            phases.Add(snapshot.CurrentPhase);
        }

        Assert.Equal([CompositionRunPhase.Preparing, CompositionRunPhase.ReadingInputs], phases);
    }
}
