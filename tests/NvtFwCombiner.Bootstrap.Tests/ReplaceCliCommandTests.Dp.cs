using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    /// <summary>The retired command rejects before input or delivery handling and preserves every existing artifact.</summary>
    [Theory]
    [InlineData("preview")]
    [InlineData("build")]
    public async Task RetiredDpReplaceRejectsWithoutInputOrDeliverySideEffects(string action)
    {
        using var workspace = TempWorkspace.Create("nfc-retired-dp-cli");
        byte[] outputSentinel = [0xA5, 0x5A];
        byte[] reportSentinel = [0x31, 0x32, 0x33];
        string outputPath = workspace.Write("existing.bin", outputSentinel);
        string reportPath = workspace.Write("existing-report.json", reportSentinel);
        string[] before = [.. Directory.GetFileSystemEntries(workspace.Root).Order(StringComparer.Ordinal)];

        CliRunResult result = await CliTestHarness.RunAsync(
        [
            "dp-replace", action, "--profile", "NT51950", "--ic-num", "single",
            "--base", "\0must-not-be-read.bin", "--dp", "\0must-not-be-read-either.bin",
            "--output", outputPath, "--report", reportPath,
            "--bundle-parent", workspace.Root, "--bundle-name", "must-not-be-created",
        ], TestContext.Current.CancellationToken);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("cli.retired-experience", result.Error, StringComparison.Ordinal);
        Assert.Empty(result.Output);
        Assert.Equal(before, Directory.GetFileSystemEntries(workspace.Root).Order(StringComparer.Ordinal));
        Assert.Equal(outputSentinel, await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken));
        Assert.Equal(reportSentinel, await File.ReadAllBytesAsync(reportPath, TestContext.Current.CancellationToken));
    }
}
