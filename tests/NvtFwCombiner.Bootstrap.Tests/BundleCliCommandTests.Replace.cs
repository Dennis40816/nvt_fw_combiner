using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class BundleCliCommandTests
{
    /// <summary>DP Replace bundle mode commits the canonical output together with its accepted reference and replacement inputs.</summary>
    [Fact]
    public async Task DpReplaceBuildCommitsBundleFromAcceptedInputs()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-bundle-dp-replace");
        string referencePath = workspace.Write("reference.bin", new byte[0x40000]);
        string dpPath = workspace.Write("dp.bin", new byte[0x40000]);
        string reportPath = workspace.PathFor("dp-bundle-report.json");

        CliRunResult result = await CliTestHarness.RunRetainedReplaceAsync(
            [
                "dp-replace",
                "build",
                "--profile",
                "NT51923",
                "--ic-num",
                "single",
                "--base",
                referencePath,
                "--dp",
                dpPath,
                "--bundle-parent",
                workspace.Root,
                "--bundle-name",
                "dp_bundle",
                "--report",
                reportPath,
            ],
            TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.Error + Environment.NewLine + result.Output);
        using JsonDocument report = JsonDocument.Parse(
            await File.ReadAllTextAsync(reportPath, TestContext.Current.CancellationToken));
        Assert.Equal(
            3,
            report.RootElement.GetProperty("BundleDelivery")
                .GetProperty("Artifacts")
                .GetArrayLength());
    }

    /// <summary>Omitted DP output is automatic naming while an explicit path remains an override in the report.</summary>
    [Fact]
    public async Task DpReplaceAutomaticAndExplicitOutputRetainDistinctNamingIdentity()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-dp-naming-identity");
        byte[] dp = new byte[0x40000];
        const int dpcmiStart = 0x3E000 + 20;
        dp[dpcmiStart + 1] = 0xBE;
        dp[dpcmiStart + 2] = 0xEF;
        string referencePath = workspace.Write("reference.bin", new byte[0x40000]);
        string dpPath = workspace.Write("dp.bin", dp);
        string automaticReportPath = workspace.PathFor("automatic-report.json");
        string explicitReportPath = workspace.PathFor("explicit-report.json");
        string explicitOutputPath = workspace.PathFor("explicit.bin");
        string? automaticOutputPath = null;

        try
        {
            CliRunResult automatic = await CliTestHarness.RunRetainedReplaceAsync(
                [
                    "dp-replace",
                    "build",
                    "--profile",
                    "NT51923",
                    "--ic-num",
                    "single",
                    "--base",
                    referencePath,
                    "--dp",
                    dpPath,
                    "--report",
                    automaticReportPath,
                ],
                TestContext.Current.CancellationToken);
            CliRunResult explicitResult = await CliTestHarness.RunRetainedReplaceAsync(
                [
                    "dp-replace",
                    "build",
                    "--profile",
                    "NT51923",
                    "--ic-num",
                    "single",
                    "--base",
                    referencePath,
                    "--dp",
                    dpPath,
                    "--output",
                    explicitOutputPath,
                    "--report",
                    explicitReportPath,
                ],
                TestContext.Current.CancellationToken);

            Assert.True(automatic.ExitCode == 0, automatic.Error + Environment.NewLine + automatic.Output);
            Assert.True(explicitResult.ExitCode == 0, explicitResult.Error + Environment.NewLine + explicitResult.Output);
            using JsonDocument automaticReport = JsonDocument.Parse(
                await File.ReadAllTextAsync(
                    automaticReportPath,
                    TestContext.Current.CancellationToken));
            using JsonDocument explicitReport = JsonDocument.Parse(
                await File.ReadAllTextAsync(
                    explicitReportPath,
                    TestContext.Current.CancellationToken));
            JsonElement automaticNaming = automaticReport.RootElement.GetProperty("OutputNaming");
            JsonElement explicitNaming = explicitReport.RootElement.GetProperty("OutputNaming");
            Assert.False(automaticNaming.GetProperty("IsExplicitOverride").GetBoolean());
            Assert.True(explicitNaming.GetProperty("IsExplicitOverride").GetBoolean());
            Assert.Equal("explicit.bin", explicitNaming.GetProperty("ActualFileName").GetString());
            string automaticFileName = automaticNaming.GetProperty("ActualFileName").GetString()!;
            Assert.Equal(
                automaticNaming.GetProperty("AutomaticFileName").GetString(),
                automaticFileName);
            automaticOutputPath = Path.GetFullPath(automaticFileName);
            Assert.True(File.Exists(automaticOutputPath));
        }
        finally
        {
            if (automaticOutputPath is not null && File.Exists(automaticOutputPath))
            {
                File.Delete(automaticOutputPath);
            }
        }
    }

    /// <summary>General Replace accepts the shared bundle destination without changing its canonical mapped bytes.</summary>
    [Fact]
    public async Task GeneralReplaceBuildCommitsCanonicalBundleOutput()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-bundle-general-replace");
        byte[] baseBytes = await File.ReadAllBytesAsync(
            BootstrapTestData.GoldenArtifactPath("51926", "expected-output"),
            TestContext.Current.CancellationToken);
        string referencePath = workspace.Write("reference.bin", baseBytes);
        string sourcePath = workspace.Write("dp-source.bin", [0xA5, 0x5A]);
        string reportPath = workspace.PathFor("general-replace-bundle-report.json");

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "general-replace",
                "build",
                "--profile",
                "NT51926",
                "--ic-num",
                "single",
                "--base",
                referencePath,
                "--mapping",
                $"0x3E020+0x2={sourcePath}",
                "--bundle-parent",
                workspace.Root,
                "--bundle-name",
                "general_replace_bundle",
                "--report",
                reportPath,
            ],
            TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.Error + Environment.NewLine + result.Output);
        using JsonDocument report = JsonDocument.Parse(
            await File.ReadAllTextAsync(reportPath, TestContext.Current.CancellationToken));
        JsonElement outputArtifact = report.RootElement.GetProperty("BundleDelivery")
            .GetProperty("Artifacts")[0];
        string outputName = outputArtifact.GetProperty("DeliveredFileName").GetString()!;
        byte[] outputBytes = await File.ReadAllBytesAsync(
            Path.Combine(workspace.Root, "general_replace_bundle", outputName),
            TestContext.Current.CancellationToken);
        Assert.Equal([0xA5, 0x5A], outputBytes[0x3E020..0x3E022]);
        Assert.Equal(
            baseBytes,
            await File.ReadAllBytesAsync(referencePath, TestContext.Current.CancellationToken));
    }
}
