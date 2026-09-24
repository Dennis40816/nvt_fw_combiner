using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class BundleCliCommandTests
{
    /// <summary>General Replace bundle mode commits the canonical output together with its accepted reference and replacement inputs.</summary>
    [Fact]
    public async Task ReferenceReplaceBuildCommitsBundleFromAcceptedInputs()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-bundle-dp-replace");
        string referencePath = workspace.Write("reference.bin", File.ReadAllBytes(BootstrapTestData.GoldenArtifactPath("51926", "expected-output")));
        string dpPath = workspace.Write("dp.bin", new byte[2]);
        string reportPath = workspace.PathFor("dp-bundle-report.json");

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
                $"0x3E020+0x2={dpPath}",
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

    /// <summary>Public automatic and explicit outputs retain renderer-specific naming evidence and identical full bytes.</summary>
    [Theory]
    [InlineData("standard-merge")]
    [InlineData("general-replace")]
    [InlineData("ctrlram-replace")]
    public async Task AutomaticAndExplicitOutputRetainDistinctNamingIdentity(string workflow)
    {
        using var workspace = TempWorkspace.Create("nfc-cli-naming-identity");
        byte[] expected = File.ReadAllBytes(BootstrapTestData.GoldenArtifactPath("51926", "expected-output"));
        string[] inputs;
        if (workflow == "standard-merge")
        {
            inputs = ["--dp", BootstrapTestData.GoldenArtifactPath("51926", "dp-input"),
                "--tp", BootstrapTestData.GoldenArtifactPath("51926", "tp-input")];
        }
        else if (workflow == "general-replace")
        {
            string reference = workspace.Write("reference.bin", expected);
            string source = workspace.Write("source.bin", [0xBE, 0xEF]);
            inputs = ["--ic-num", "single", "--base", reference, "--mapping", $"0x3E020+0x2={source}"];
            expected[0x3E020] = 0xBE;
            expected[0x3E021] = 0xEF;
        }
        else
        {
            ReplaceCliCommandTests.Nt51926SelectiveVnRegression fixture = ReplaceCliCommandTests.LoadNt51926SelectiveVnRegression();
            inputs = ["--ic-num", "cascade", "--base", CanonicalGoldenTestData.ArtifactPath(fixture.BaseArtifact),
                "--ctrlram", $"replace-ctrlram-vn={CanonicalGoldenTestData.ArtifactPath(fixture.VnArtifact)}"];
            expected = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(fixture.ExpectedArtifact));
        }
        string automaticReportPath = workspace.PathFor("automatic-report.json");
        string explicitReportPath = workspace.PathFor("explicit-report.json");
        string explicitOutputPath = workspace.PathFor("explicit.bin");
        string? automaticOutputPath = null;
        try
        {
            CliRunResult automatic = await CliTestHarness.RunAsync(
                [workflow, "build", "--profile", "NT51926", .. inputs, "--report", automaticReportPath],
                TestContext.Current.CancellationToken);
            CliRunResult explicitResult = await CliTestHarness.RunAsync(
                [workflow, "build", "--profile", "NT51926", .. inputs, "--output", explicitOutputPath,
                    "--report", explicitReportPath], TestContext.Current.CancellationToken);
            Assert.True(automatic.ExitCode == 0, automatic.Error + Environment.NewLine + automatic.Output);
            Assert.True(explicitResult.ExitCode == 0, explicitResult.Error + Environment.NewLine + explicitResult.Output);
            using JsonDocument automaticReport = JsonDocument.Parse(await File.ReadAllTextAsync(
                automaticReportPath, TestContext.Current.CancellationToken));
            using JsonDocument explicitReport = JsonDocument.Parse(await File.ReadAllTextAsync(
                explicitReportPath, TestContext.Current.CancellationToken));
            string automaticFileName = automaticReport.RootElement.GetProperty("Output").GetProperty("FileName").GetString()!;
            Assert.Equal("explicit.bin", explicitReport.RootElement.GetProperty("Output").GetProperty("FileName").GetString());
            Assert.NotEqual("explicit.bin", automaticFileName);
            JsonElement automaticNaming = automaticReport.RootElement.GetProperty("OutputNaming");
            JsonElement explicitNaming = explicitReport.RootElement.GetProperty("OutputNaming");
            if (workflow == "standard-merge")
            {
                Assert.False(automaticNaming.GetProperty("IsExplicitOverride").GetBoolean());
                Assert.True(explicitNaming.GetProperty("IsExplicitOverride").GetBoolean());
                Assert.Equal("explicit.bin", explicitNaming.GetProperty("ActualFileName").GetString());
                Assert.Equal(automaticFileName, automaticNaming.GetProperty("AutomaticFileName").GetString());
                Assert.Equal(automaticFileName, automaticNaming.GetProperty("ActualFileName").GetString());
            }
            else
            {
                Assert.Equal(JsonValueKind.Null, automaticNaming.ValueKind);
                Assert.Equal(JsonValueKind.Null, explicitNaming.ValueKind);
            }
            automaticOutputPath = Path.GetFullPath(automaticFileName);
            string expectedSha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(expected));
            foreach (JsonElement output in new[]
            {
                automaticReport.RootElement.GetProperty("Output"), explicitReport.RootElement.GetProperty("Output"),
            })
            {
                Assert.Equal(expected.LongLength, output.GetProperty("Size").GetInt64());
                Assert.Equal(expectedSha256, output.GetProperty("Sha256").GetString());
            }
            Assert.Equal(expected, await File.ReadAllBytesAsync(explicitOutputPath, TestContext.Current.CancellationToken));
            Assert.Equal(expected, await File.ReadAllBytesAsync(automaticOutputPath, TestContext.Current.CancellationToken));
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
        byte[] expected = [.. baseBytes];
        expected[0x3E020] = 0xA5;
        expected[0x3E021] = 0x5A;
        Assert.Equal(expected, outputBytes);
        Assert.Equal(
            baseBytes,
            await File.ReadAllBytesAsync(referencePath, TestContext.Current.CancellationToken));
    }
}
