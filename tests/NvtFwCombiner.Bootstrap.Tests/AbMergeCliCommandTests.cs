using System.Text.Json;
using NvtFwCombiner.Application.FlashMaps;
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
        Assert.Contains("output-naming.metadata-unknown", result.Error, StringComparison.Ordinal);
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

    /// <summary>AB preview renders the automatic filename from accepted CMI and FWConfig snapshots, not UI text.</summary>
    [Fact]
    public async Task PreviewRendersAutomaticAbCodeOutputNameFromExecutionSnapshotsAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-cli-output-name");
        byte[] dp = new byte[0x80000];
        SetCmiDpVersion(dp, 0, 0x82, 0x0);
        SetCmiDpVersion(dp, 0x40000, 0x83, 0x1);
        string dpPath = workspace.Write("display-name-does-not-matter.bin", dp);
        string tpAPath = workspace.Write("a-version-text-is-not-a-source.bin", CreateTp(0x80, 0x04));
        string tpBPath = workspace.Write("b-version-text-is-not-a-source.bin", CreateTp(0x81, 0x02));
        string reportPath = workspace.PathFor("ab-report.json");

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
                "--tp-b",
                tpBPath,
                "--report",
                reportPath,
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(
            reportPath,
            TestContext.Current.CancellationToken));
        string outputName = report.RootElement.GetProperty("Output").GetProperty("FileName").GetString()!;
        Assert.Matches("^NT51929_A_D8200T8004_B_D8301T8102_[0-9]{8}\\.bin$", outputName);
        JsonElement naming = report.RootElement.GetProperty("OutputNaming");
        Assert.Equal("ab-code-v1", naming.GetProperty("RendererKind").GetString());
        Assert.Equal(outputName, naming.GetProperty("AutomaticFileName").GetString());
        Assert.False(naming.GetProperty("IsExplicitOverride").GetBoolean());
        Assert.Equal("utc", naming.GetProperty("DateSource").GetString());
        Assert.Equal(
            report.RootElement.GetProperty("StartedAtUtc").GetDateTimeOffset(),
            naming.GetProperty("ResolvedAtUtc").GetDateTimeOffset());
        Assert.Contains(
            naming.GetProperty("Tokens").EnumerateArray(),
            token => token.GetProperty("TokenId").GetString() == "ic" &&
                token.GetProperty("Value").GetString() == "51929");
        Assert.Empty(report.RootElement.GetProperty("Issues").EnumerateArray());
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
        Assert.Contains("output-naming.metadata-unknown", result.Error, StringComparison.Ordinal);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains($"Committed: {outputPath}", result.Output, StringComparison.Ordinal);
        Assert.Equal(0x80000, new FileInfo(outputPath).Length);
    }

    private static void SetCmiDpVersion(byte[] image, int bankStart, byte major, byte minor)
    {
        image[bankStart + 0x401B] = major;
        image[bankStart + 0x401C] = (byte)(minor << 4);
    }

    private static byte[] CreateTp(byte firmwareVersion, byte firmwareSubVersion)
    {
        var image = new byte[0x40000];
        image[FirmwareConfigLayout.FirmwareVersionOffset] = firmwareVersion;
        image[FirmwareConfigLayout.FirmwareVersionBarOffset] = unchecked((byte)~firmwareVersion);
        image[FirmwareConfigLayout.FirmwareSubVersionOffset] = firmwareSubVersion;
        image[0xFFC] = 0x00;
        image[0xFFD] = (byte)'N';
        image[0xFFE] = (byte)'V';
        image[0xFFF] = (byte)'T';
        return image;
    }
}
