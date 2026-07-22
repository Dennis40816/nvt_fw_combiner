using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>CLI admission tests for the owner-approved AB Merge pilot.</summary>
public sealed class AbMergeCliCommandTests
{
    /// <summary>AB preview routes all three named sources through the supported V2 profile and exports evidence.</summary>
    [Theory]
    [InlineData("51919", "NT51919")]
    [InlineData("NT51929", "NT51929")]
    [InlineData("nt51932-ab-merge", "NT51932")]
    public async Task PreviewWritesStructuredReportAsync(string selector, string expectedIcId)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-cli");
        string dpPath = workspace.Write("dp-ab.bin", new byte[0x80000]);
        string tpAPath = workspace.Write("tp-a.bin", new byte[0x40000]);
        string tpBPath = workspace.Write("tp-b.bin", new byte[0x40000]);
        string reportPath = workspace.PathFor("ab-report.json");
        string logicalOutputPath = workspace.PathFor("logical-ab-output.bin");

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "ab-merge",
                "preview",
                "--profile",
                selector,
                "--dp-ab",
                dpPath,
                "--tp-a",
                tpAPath,
                "--tp-b",
                tpBPath,
                "--output",
                logicalOutputPath,
                "--report",
                reportPath,
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(
            reportPath,
            TestContext.Current.CancellationToken));
        Assert.Equal(expectedIcId, report.RootElement.GetProperty("IcId").GetString());
        Assert.Equal("ab-merge", report.RootElement.GetProperty("ExperienceId").GetString());
        Assert.Equal(6, report.RootElement.GetProperty("Operations").GetArrayLength());
        Assert.Equal(3, report.RootElement.GetProperty("Inputs").GetArrayLength());
        Assert.Equal("logical-ab-output.bin", report.RootElement.GetProperty("Output").GetProperty("FileName").GetString());
        Assert.False(File.Exists(logicalOutputPath));
    }

    /// <summary>A missing named source is rejected by the CLI before composition starts.</summary>
    [Fact]
    public async Task PreviewRequiresEveryDeclaredSourceAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-cli-missing");
        string dpPath = workspace.Write("dp-ab.bin", new byte[0x80000]);
        string tpAPath = workspace.Write("tp-a.bin", new byte[0x40000]);

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "ab-merge",
                "preview",
                "--profile",
                "NT51929",
                "--dp-ab",
                dpPath,
                "--tp-a",
                tpAPath,
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("--tp-b is required", result.Error, StringComparison.Ordinal);
    }

    /// <summary>AB build sends the requested path only to the committed-output policy.</summary>
    [Fact]
    public async Task BuildWritesRequestedOutputWithoutPreviewNameOverrideAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-cli-build");
        string dpPath = workspace.Write("dp-ab.bin", new byte[0x80000]);
        string tpAPath = workspace.Write("tp-a.bin", new byte[0x40000]);
        string tpBPath = workspace.Write("tp-b.bin", new byte[0x40000]);
        string outputPath = workspace.PathFor("requested-ab-output.bin");

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "ab-merge",
                "build",
                "--profile",
                "NT51929",
                "--dp-ab",
                dpPath,
                "--tp-a",
                tpAPath,
                "--tp-b",
                tpBPath,
                "--output",
                outputPath,
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains($"Committed: {outputPath}", result.Output, StringComparison.Ordinal);
        Assert.Equal(0x80000, new FileInfo(outputPath).Length);
    }
}
