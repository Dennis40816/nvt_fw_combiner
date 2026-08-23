using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>CLI parity for the shared atomic output-bundle options.</summary>
public sealed class BundleCliCommandTests
{
    /// <summary>Every CLI route that can commit one composition output.</summary>
    public static TheoryData<string> BuildCommands =>
    [
        "standard-merge",
        "ab-merge",
        "general-merge",
        "dp-replace",
        "ctrlram-replace",
        "general-replace",
    ];

    /// <summary>Bundle delivery is a Build-only commit and cannot be requested by Preview.</summary>
    [Theory]
    [MemberData(nameof(BuildCommands))]
    public async Task PreviewRejectsBundleBeforeReadingInputs(string command)
    {
        using var workspace = TempWorkspace.Create("nfc-cli-bundle-preview");

        CliRunResult result = await CliTestHarness.RunAsync(
            [command, "preview", "--bundle-parent", workspace.Root],
            TestContext.Current.CancellationToken);

        Assert.Equal(64, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("--bundle-parent is available only for build", result.Error, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>An edited folder name has no meaning without opting into a parent directory.</summary>
    [Theory]
    [MemberData(nameof(BuildCommands))]
    public async Task BundleNameAloneIsRejectedBeforeReadingInputs(string command)
    {
        CliRunResult result = await CliTestHarness.RunAsync(
            [command, "build", "--bundle-name", "edited_bundle"],
            TestContext.Current.CancellationToken);

        Assert.Equal(64, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("--bundle-name requires --bundle-parent", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Atomic bundle delivery and loose output delivery are mutually exclusive.</summary>
    [Theory]
    [MemberData(nameof(BuildCommands))]
    public async Task BundleParentAndOutputAreRejectedBeforeReadingInputs(string command)
    {
        using var workspace = TempWorkspace.Create("nfc-cli-bundle-output");

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                command,
                "build",
                "--bundle-parent",
                workspace.Root,
                "--output",
                workspace.PathFor("must-not-exist.bin"),
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(64, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("--bundle-parent cannot be combined with --output", result.Error, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>The CLI consumes the exact Application proposal, preserves its canonical BIN name, and reports the actual collision-resolved folder.</summary>
    [Fact]
    public async Task StandardMergeBundleUsesProposalSupportsEditAndReportsCollisionReceipt()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-bundle-standard");
        byte[] dp = new byte[0x40000];
        byte[] tp = new byte[0x3C000];
        const int dpcmiStart = 0x3E000 + 20;
        dp[dpcmiStart + 1] = 0xB6;
        dp[dpcmiStart + 2] = 0xD4;
        tp[0] = 0xA7;
        tp[1] = 0x58;
        tp[17] = 0xC9;
        tp[4092] = 0x00;
        tp[4093] = 0x4E;
        tp[4094] = 0x56;
        tp[4095] = 0x54;
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);
        string reportOne = workspace.PathFor("bundle-one.json");
        string reportTwo = workspace.PathFor("bundle-two.json");
        string reportEdited = workspace.PathFor("bundle-edited.json");

        string[] commonArgs =
        [
            "standard-merge",
            "build",
            "--profile",
            "NT51923",
            "--dp",
            dpPath,
            "--tp",
            tpPath,
            "--bundle-parent",
            workspace.Root,
        ];
        CliRunResult first = await CliTestHarness.RunAsync(
            [.. commonArgs, "--report", reportOne],
            TestContext.Current.CancellationToken);
        CliRunResult second = await CliTestHarness.RunAsync(
            [.. commonArgs, "--report", reportTwo],
            TestContext.Current.CancellationToken);
        CliRunResult edited = await CliTestHarness.RunAsync(
            [.. commonArgs, "--bundle-name", "edited_bundle", "--report", reportEdited],
            TestContext.Current.CancellationToken);

        Assert.True(first.ExitCode == 0, first.Error + Environment.NewLine + first.Output);
        Assert.True(second.ExitCode == 0, second.Error + Environment.NewLine + second.Output);
        Assert.True(edited.ExitCode == 0, edited.Error + Environment.NewLine + edited.Output);
        using JsonDocument firstReport = JsonDocument.Parse(
            await File.ReadAllTextAsync(reportOne, TestContext.Current.CancellationToken));
        using JsonDocument secondReport = JsonDocument.Parse(
            await File.ReadAllTextAsync(reportTwo, TestContext.Current.CancellationToken));
        using JsonDocument editedReport = JsonDocument.Parse(
            await File.ReadAllTextAsync(reportEdited, TestContext.Current.CancellationToken));
        JsonElement firstBundle = firstReport.RootElement.GetProperty("BundleDelivery");
        JsonElement secondBundle = secondReport.RootElement.GetProperty("BundleDelivery");
        JsonElement editedBundle = editedReport.RootElement.GetProperty("BundleDelivery");
        string firstDirectory = firstBundle.GetProperty("ResolvedDirectory").GetString()!;
        string secondDirectory = secondBundle.GetProperty("ResolvedDirectory").GetString()!;
        string editedDirectory = Path.Combine(workspace.Root, "edited_bundle");
        Assert.Matches(
            "^NT51923_DB60DTA7C9_[0-9]{8}_bundle$",
            Path.GetFileName(firstDirectory));
        Assert.Equal(firstDirectory + " (2)", secondDirectory);
        Assert.Equal(editedDirectory, editedBundle.GetProperty("ResolvedDirectory").GetString());
        Assert.Contains($"Bundle: {firstDirectory}", first.Output, StringComparison.Ordinal);
        Assert.Contains($"Bundle: {secondDirectory}", second.Output, StringComparison.Ordinal);
        Assert.Contains($"Bundle: {editedDirectory}", edited.Output, StringComparison.Ordinal);
        Assert.True(Directory.Exists(firstDirectory));
        Assert.True(Directory.Exists(secondDirectory));
        Assert.True(Directory.Exists(editedDirectory));
        JsonElement[] artifacts = [.. firstBundle.GetProperty("Artifacts").EnumerateArray()];
        Assert.Equal(3, artifacts.Length);
        Assert.Equal("output", artifacts[0].GetProperty("Role").GetString());
        string canonicalBin = artifacts[0].GetProperty("DeliveredFileName").GetString()!;
        Assert.Matches("^NT51923_FlashCode_DB60DTA7C9_[0-9]{8}\\.bin$", canonicalBin);
        Assert.True(File.Exists(Path.Combine(firstDirectory, canonicalBin)));
        Assert.Equal(dp, await File.ReadAllBytesAsync(
            Path.Combine(firstDirectory, "dp.bin"), TestContext.Current.CancellationToken));
        Assert.Equal(tp, await File.ReadAllBytesAsync(
            Path.Combine(firstDirectory, "tp.bin"), TestContext.Current.CancellationToken));
        Assert.Equal(
            canonicalBin,
            firstReport.RootElement.GetProperty("OutputNaming").GetProperty("ActualFileName").GetString());
    }

    /// <summary>Malformed edited names surface the shared stable validation code and commit nothing.</summary>
    [Fact]
    public async Task InvalidEditedBundleNameReturnsStableIssueWithoutCommit()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-bundle-invalid");
        string dpPath = workspace.Write("dp.bin", new byte[0x40000]);
        string tpPath = workspace.Write("tp.bin", new byte[0x3C000]);

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "standard-merge",
                "build",
                "--profile",
                "NT51923",
                "--dp",
                dpPath,
                "--tp",
                tpPath,
                "--bundle-parent",
                workspace.Root,
                "--bundle-name",
                "../invalid",
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(64, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("bundle-destination.name.invalid", result.Error, StringComparison.Ordinal);
        Assert.Equal(2, Directory.EnumerateFiles(workspace.Root).Count());
        Assert.Empty(Directory.EnumerateDirectories(workspace.Root));
    }

    /// <summary>AB Merge sends bundle delivery through the same non-interactive intent and retains all three accepted sources.</summary>
    [Fact]
    public async Task AbMergeBuildCommitsBundleAndSourceReceipt()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-bundle-ab");
        string dpPath = workspace.Write("dp-ab.bin", new byte[0x80000]);
        string tpAPath = workspace.Write("tp-a.bin", new byte[0x40000]);
        string tpBPath = workspace.Write("tp-b.bin", new byte[0x40000]);
        string reportPath = workspace.PathFor("ab-bundle-report.json");

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
                "--bundle-parent",
                workspace.Root,
                "--bundle-name",
                "ab_bundle",
                "--include-a-flashcode",
                "--report",
                reportPath,
            ],
            TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.Error + Environment.NewLine + result.Output);
        string bundleDirectory = Path.Combine(workspace.Root, "ab_bundle");
        Assert.Contains($"Bundle: {bundleDirectory}", result.Output, StringComparison.Ordinal);
        using JsonDocument report = JsonDocument.Parse(
            await File.ReadAllTextAsync(reportPath, TestContext.Current.CancellationToken));
        JsonElement bundle = report.RootElement.GetProperty("BundleDelivery");
        Assert.Equal(bundleDirectory, bundle.GetProperty("ResolvedDirectory").GetString());
        JsonElement[] artifacts = [.. bundle.GetProperty("Artifacts").EnumerateArray()];
        Assert.Equal(5, artifacts.Length);
        Assert.Equal<string>(
            ["output", "additional-delivery", "source", "source", "source"],
            [.. artifacts.Select(artifact => artifact.GetProperty("Role").GetString()!)]);
        JsonElement additional = Assert.Single(
            artifacts,
            artifact => artifact.GetProperty("Role").GetString() == "additional-delivery");
        Assert.Equal(
            CompiledAdditionalDelivery.AbAFlashCodeKind,
            additional.GetProperty("BindingId").GetString());
        Assert.True(File.Exists(Path.Combine(
            bundleDirectory,
            additional.GetProperty("DeliveredFileName").GetString()!)));
        JsonElement primary = Assert.Single(
            artifacts,
            artifact => artifact.GetProperty("Role").GetString() == "output");
        byte[] primaryBytes = await File.ReadAllBytesAsync(
            Path.Combine(bundleDirectory, primary.GetProperty("DeliveredFileName").GetString()!),
            TestContext.Current.CancellationToken);
        byte[] additionalBytes = await File.ReadAllBytesAsync(
            Path.Combine(bundleDirectory, additional.GetProperty("DeliveredFileName").GetString()!),
            TestContext.Current.CancellationToken);
        JsonElement delivery = Assert.Single(
            report.RootElement.GetProperty("DeliveryArtifacts").EnumerateArray());
        Assert.Equal(CompiledAdditionalDelivery.AbAFlashCodeKind, delivery.GetProperty("DeliveryKind").GetString());
        Assert.Equal(0, delivery.GetProperty("SourceRange").GetProperty("Start").GetInt64());
        Assert.Equal(additionalBytes.LongLength, delivery.GetProperty("SourceRange").GetProperty("Length").GetInt64());
        Assert.True(primaryBytes.AsSpan(0, additionalBytes.Length).SequenceEqual(additionalBytes));
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(additionalBytes)),
            delivery.GetProperty("Sha256").GetString());
        string[] remainingInputs =
        [
            .. Directory.EnumerateFiles(workspace.Root, "*.bin")
                .Order(StringComparer.Ordinal),
        ];
        Assert.Equal<string>([dpPath, tpAPath, tpBPath], remainingInputs);
    }

    /// <summary>The AB-only artifact cannot escape the atomic bundle delivery transaction.</summary>
    [Fact]
    public async Task AbAdditionalDeliveryRequiresAtomicBundle()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-bundle-ab-additional-option");

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "ab-merge",
                "build",
                "--include-a-flashcode",
                "--profile",
                "NT51929",
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains(
            "--include-a-flashcode requires --bundle-parent",
            result.Error,
            StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>The AB-only bundle selection is never accepted by Preview.</summary>
    [Fact]
    public async Task AbAdditionalDeliveryIsRejectedForPreview()
    {
        CliRunResult result = await CliTestHarness.RunAsync(
            ["ab-merge", "preview", "--include-a-flashcode"],
            TestContext.Current.CancellationToken);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains(
            "--include-a-flashcode is available only for build",
            result.Error,
            StringComparison.Ordinal);
    }

    /// <summary>The CLI retains exact compiled authority when an AB profile has no A-only delivery.</summary>
    [Fact]
    public async Task AbAdditionalDeliveryRequiresCompiledDeclaration()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-bundle-ab-additional-undeclared");
        string dpPath = workspace.Write("dp-ab.bin", new byte[0x80000]);
        string tpAPath = workspace.Write("tp-a.bin", new byte[0x40000]);
        string tpBPath = workspace.Write("tp-b.bin", new byte[0x40000]);

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "ab-merge",
                "build",
                "--profile",
                "NT51950",
                "--ab-topology",
                "single",
                "--dp-ab",
                dpPath,
                "--tp-a",
                tpAPath,
                "--tp-b",
                tpBPath,
                "--bundle-parent",
                workspace.Root,
                "--include-a-flashcode",
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains(
            "bundle.additional-delivery-unavailable",
            result.Error,
            StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateDirectories(workspace.Root));
    }

    /// <summary>General Merge bundle mode keeps the canonical static BIN without producing a loose duplicate.</summary>
    [Fact]
    public async Task GeneralMergeBuildCommitsBundleWithoutLooseOutput()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-bundle-general-merge");
        string sourcePath = workspace.Write("source.bin", [0x10, 0x11]);
        string reportPath = workspace.PathFor("general-bundle-report.json");

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "general-merge",
                "build",
                "--profile",
                "NT51950",
                "--size",
                "0x4",
                "--mapping",
                $"0x0+0x1+0x2={sourcePath}",
                "--bundle-parent",
                workspace.Root,
                "--bundle-name",
                "general_bundle",
                "--report",
                reportPath,
            ],
            TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.Error + Environment.NewLine + result.Output);
        string bundleDirectory = Path.Combine(workspace.Root, "general_bundle");
        using JsonDocument report = JsonDocument.Parse(
            await File.ReadAllTextAsync(reportPath, TestContext.Current.CancellationToken));
        JsonElement[] artifacts =
        [
            .. report.RootElement.GetProperty("BundleDelivery")
                .GetProperty("Artifacts")
                .EnumerateArray(),
        ];
        string outputName = artifacts[0].GetProperty("DeliveredFileName").GetString()!;
        Assert.Equal(
            [0, 0x10, 0x11, 0],
            await File.ReadAllBytesAsync(
                Path.Combine(bundleDirectory, outputName),
                TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(workspace.Root, outputName)));
    }

    /// <summary>CtrlRAM bundle delivery commits from the same readiness-bound accepted snapshot and retains base plus replacement inputs.</summary>
    [Fact]
    public async Task CtrlRamReplaceBuildCommitsReadinessBoundBundle()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-bundle-ctrlram");
        ReplaceCliCommandTests.Nt51926SelectiveVnRegression fixture =
            ReplaceCliCommandTests.LoadNt51926SelectiveVnRegression();
        string basePath = CanonicalGoldenTestData.ArtifactPath(fixture.BaseArtifact);
        string vnPath = CanonicalGoldenTestData.ArtifactPath(fixture.VnArtifact);
        string expectedPath = CanonicalGoldenTestData.ArtifactPath(fixture.ExpectedArtifact);
        string reportPath = workspace.PathFor("ctrlram-bundle-report.json");

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "ctrlram-replace",
                "build",
                "--profile",
                "NT51926",
                "--ic-num",
                "cascade",
                "--base",
                basePath,
                "--ctrlram",
                $"replace-ctrlram-vn={vnPath}",
                "--bundle-parent",
                workspace.Root,
                "--bundle-name",
                "ctrlram_bundle",
                "--report",
                reportPath,
            ],
            TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.Error + Environment.NewLine + result.Output);
        using JsonDocument report = JsonDocument.Parse(
            await File.ReadAllTextAsync(reportPath, TestContext.Current.CancellationToken));
        JsonElement[] artifacts =
        [
            .. report.RootElement.GetProperty("BundleDelivery")
                .GetProperty("Artifacts")
                .EnumerateArray(),
        ];
        Assert.Equal(3, artifacts.Length);
        string outputName = artifacts[0].GetProperty("DeliveredFileName").GetString()!;
        Assert.Equal(
            await File.ReadAllBytesAsync(expectedPath, TestContext.Current.CancellationToken),
            await File.ReadAllBytesAsync(
                Path.Combine(workspace.Root, "ctrlram_bundle", outputName),
                TestContext.Current.CancellationToken));
    }

    /// <summary>DP Replace bundle mode commits the canonical output together with its accepted reference and replacement inputs.</summary>
    [Fact]
    public async Task DpReplaceBuildCommitsBundleFromAcceptedInputs()
    {
        using var workspace = TempWorkspace.Create("nfc-cli-bundle-dp-replace");
        string referencePath = workspace.Write("reference.bin", new byte[0x40000]);
        string dpPath = workspace.Write("dp.bin", new byte[0x40000]);
        string reportPath = workspace.PathFor("dp-bundle-report.json");

        CliRunResult result = await CliTestHarness.RunAsync(
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
            CliRunResult automatic = await CliTestHarness.RunAsync(
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
            CliRunResult explicitResult = await CliTestHarness.RunAsync(
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
