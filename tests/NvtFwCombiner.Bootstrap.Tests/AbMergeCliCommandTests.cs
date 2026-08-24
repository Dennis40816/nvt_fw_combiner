using System.Buffers.Binary;
using System.Text.Json;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>CLI admission tests for the owner-approved AB Merge pilot.</summary>
public sealed class AbMergeCliCommandTests
{
    /// <summary>Global help advertises the optional topology token required by profiles such as NT51950.</summary>
    [Fact]
    public async Task GlobalUsageAdvertisesOptionalAbTopologyAsync()
    {
        CliRunResult result = await CliTestHarness.RunAsync(
            ["--help"],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "ab-merge preview --profile <id|ic> --dp-ab <path> --tp-a <path> --tp-b <path> [--ab-topology <single|cascade>]",
            result.Output,
            StringComparison.Ordinal);
        Assert.DoesNotContain("--overwrite", result.Output, StringComparison.Ordinal);
    }

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
        JsonElement naming = report.RootElement.GetProperty("OutputNaming");
        Assert.True(naming.GetProperty("IsExplicitOverride").GetBoolean());
        Assert.Equal("logical-ab-output.bin", naming.GetProperty("ActualFileName").GetString());
        Assert.Matches(
            $"^{expectedIcId}_FlashCode_A_D0000Txxxx_B_D0000Txxxx_[0-9]{{8}}\\.bin$",
            naming.GetProperty("AutomaticFileName").GetString());
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
        Assert.Matches("^NT51929_FlashCode_A_D8200T8004_B_D8301T8102_[0-9]{8}\\.bin$", outputName);
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

    /// <summary>NT51950 Cascade names read its profile-owned DP CMI locations, never TP metadata or a presentation bank offset.</summary>
    [Fact]
    public async Task Nt51950CascadePreviewUsesProfileOwnedCmiPositionsForAutomaticNameAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-cli-output-name");
        byte[] dp = new byte[0x100000];
        SetCmiDpVersionAt(dp, 0x5016, 0x82, 0x0);
        SetCmiDpVersionAt(dp, 0x45016, 0x83, 0x1);
        byte[] tpA = CreateTp(0x80, 0x04, chipCount: 2, length: 0x37000);
        byte[] tpB = CreateTp(0x81, 0x02, chipCount: 2, length: 0x37000);
        BinaryPrimitives.WriteUInt32LittleEndian(tpB.AsSpan(0xA100, sizeof(uint)), 0x0000A100);
        BinaryPrimitives.WriteUInt32LittleEndian(tpB.AsSpan(0xA110, sizeof(uint)), 0x0000A110);
        BinaryPrimitives.WriteUInt32LittleEndian(tpB.AsSpan(0xA120, sizeof(uint)), 0x0000A120);
        string reportPath = workspace.PathFor("ab-report.json");

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "ab-merge",
                "preview",
                "--profile",
                "NT51950",
                "--ab-topology",
                "cascade",
                "--dp-ab",
                workspace.Write("dp.bin", dp),
                "--tp-a",
                workspace.Write("tp-a.bin", tpA),
                "--tp-b",
                workspace.Write("tp-b.bin", tpB),
                "--report",
                reportPath,
            ],
            TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.Error + Environment.NewLine + result.Output);
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(
            reportPath,
            TestContext.Current.CancellationToken));
        string outputName = report.RootElement.GetProperty("Output").GetProperty("FileName").GetString()!;
        Assert.Matches("^NT51950_FlashCode_A_D8200T8004_B_D8301T8102_[0-9]{8}\\.bin$", outputName);
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

    /// <summary>CLI automatic builds keep the CLI-owned working directory while preserving the dynamic output name.</summary>
    [Fact]
    public async Task CliBuildUsesAutomaticOutputDirectoryWithoutTurningItIntoAnOverrideAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-cli-automatic-output");
        string dpPath = workspace.Write("dp-ab.bin", new byte[0x80000]);
        string tpAPath = workspace.Write("tp-a.bin", new byte[0x40000]);
        string tpBPath = workspace.Write("tp-b.bin", new byte[0x40000]);
        IReadOnlyDictionary<string, string> slots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = dpPath,
            [CompositionAddressSpaceIds.TpAInput] = tpAPath,
            [CompositionAddressSpaceIds.TpBInput] = tpBPath,
        };

        CompositionRunResult result = await AbMergeTestSupport.RunAsync(BootstrapTestHost.Services,
            "NT51929",
            slots,
            build: true,
            outputPath: null,
            previewOutputFileName: null,
            topologySelection: null,
            automaticOutputDirectory: workspace.Root,
            reportPath: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Status.ToString());
        Assert.NotNull(result.CommittedOutputId);
        Assert.Equal(workspace.Root, Path.GetDirectoryName(result.CommittedOutputId));
        using var report = JsonDocument.Parse(CompositionRunReportJson.Serialize(result));
        Assert.False(report.RootElement.GetProperty("OutputNaming").GetProperty("IsExplicitOverride").GetBoolean());
    }

    /// <summary>Dynamic AB output commit protects a report path after the profile-name preflight.</summary>
    [Fact]
    public async Task CliAutomaticBuildRejectsReportPathMatchingTheResolvedOutputAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-cli-automatic-report-alias");
        byte[] dp = new byte[0x80000];
        SetCmiDpVersion(dp, 0, 0x82, 0x0);
        SetCmiDpVersion(dp, 0x40000, 0x83, 0x1);
        IReadOnlyDictionary<string, string> slots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = workspace.Write("dp-ab.bin", dp),
            [CompositionAddressSpaceIds.TpAInput] = workspace.Write("tp-a.bin", CreateTp(0x80, 0x04)),
            [CompositionAddressSpaceIds.TpBInput] = workspace.Write("tp-b.bin", CreateTp(0x81, 0x02)),
        };
        string outputFileName = (await AbMergeTestSupport.PrepareOutputAsync(BootstrapTestHost.Services,
            "NT51929",
            slots,
            TestContext.Current.CancellationToken)).OutputName.FileName;
        string reportPath = workspace.PathFor(outputFileName);

        _ = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await AbMergeTestSupport.RunAsync(BootstrapTestHost.Services,
                "NT51929",
                slots,
                build: true,
                outputPath: null,
                previewOutputFileName: null,
                topologySelection: null,
                automaticOutputDirectory: workspace.Root,
                reportPath: reportPath,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(File.Exists(reportPath));
    }

    /// <summary>Automatic Build reports readiness without fabricating a run report when admission fails.</summary>
    [Fact]
    public async Task CliAutomaticBuildDoesNotCreateRunReportWhenInputAdmissionFailsAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-cli-automatic-failure-report");
        string reportPath = workspace.PathFor("failed-build.json");

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "ab-merge",
                "build",
                "--profile",
                "NT51929",
                "--dp-ab",
                workspace.Write("dp-short.bin", new byte[0x7FFFF]),
                "--tp-a",
                workspace.Write("tp-a.bin", CreateTp(0x80, 0x04)),
                "--tp-b",
                workspace.Write("tp-b.bin", CreateTp(0x81, 0x02)),
                "--report",
                reportPath,
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ExitCode);
        Assert.False(File.Exists(reportPath));
        Assert.Contains("Issues:", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Status:", result.Output, StringComparison.Ordinal);
    }

    /// <summary>An oversized sparse AB source is rejected by the compiled slot ceiling before any output is created.</summary>
    [Fact]
    public async Task BuildRejectsOversizedSparseInputBeforeMaterializationAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-cli-bounded-read");
        string dpPath = workspace.PathFor("oversized-dp-ab.bin");
        await using (var stream = new FileStream(
            dpPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1,
            FileOptions.Asynchronous))
        {
            stream.SetLength(100_000_001);
            await stream.FlushAsync(TestContext.Current.CancellationToken);
        }
        string outputPath = workspace.PathFor("must-not-exist.bin");
        string reportPath = workspace.PathFor("must-not-exist.json");

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "ab-merge",
                "build",
                "--profile",
                "NT51929",
                "--dp-ab",
                dpPath,
                "--tp-a",
                workspace.Write("tp-a.bin", new byte[0x40000]),
                "--tp-b",
                workspace.Write("tp-b.bin", new byte[0x40000]),
                "--output",
                outputPath,
                "--report",
                reportPath,
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(70, result.ExitCode);
        Assert.Contains("input.artifact.read-failed [dp-ab-input]", result.Error, StringComparison.Ordinal);
        Assert.Contains("100000001", result.Error, StringComparison.Ordinal);
        Assert.Contains("524288-byte limit", result.Error, StringComparison.Ordinal);
        Assert.Empty(result.Output);
        Assert.False(File.Exists(outputPath));
        Assert.False(File.Exists(reportPath));
    }

    private static void SetCmiDpVersion(byte[] image, int bankStart, byte major, byte minor)
    {
        image[bankStart + 0x401B] = major;
        image[bankStart + 0x401C] = (byte)(minor << 4);
    }

    private static void SetCmiDpVersionAt(byte[] image, int register16Start, byte major, byte minor)
    {
        image[register16Start + 1] = major;
        image[register16Start + 2] = (byte)(minor << 4);
    }

    private static byte[] CreateTp(
        byte firmwareVersion,
        byte firmwareSubVersion,
        byte chipCount = 0,
        int length = 0x40000)
    {
        byte[] image = new byte[length];
        image[FirmwareConfigLayout.FirmwareVersionOffset] = firmwareVersion;
        image[FirmwareConfigLayout.FirmwareVersionBarOffset] = unchecked((byte)~firmwareVersion);
        image[FirmwareConfigLayout.FirmwareSubVersionOffset] = firmwareSubVersion;
        image[FirmwareConfigLayout.ChipNumberOffset] = chipCount;
        image[0xFFC] = 0x00;
        image[0xFFD] = (byte)'N';
        image[0xFFE] = (byte)'V';
        image[0xFFF] = (byte)'T';
        return image;
    }
}
