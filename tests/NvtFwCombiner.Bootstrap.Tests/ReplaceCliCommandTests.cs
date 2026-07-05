using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>CLI tests for Replace command groups.</summary>
public sealed class ReplaceCliCommandTests
{
    /// <summary>Verifies DP Replace build accepts separate DP and LD replacement inputs.</summary>
    [Fact]
    public async Task DpReplaceBuildWritesSeparateDpAndLdPayloads()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [0, 0, 0, 0, 0, 0, 0, 0]);
        string dp = workspace.Write("dp.bin", [0x11, 0x22]);
        string ld = workspace.Write("ld.bin", [0x33]);
        string output = workspace.PathFor("out.bin");
        string report = workspace.PathFor("report.json");

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "build",
            "--profile",
            "synthetic-dp-replace",
            "--ic-num",
            "51920",
            "--base",
            reference,
            "--dp",
            dp,
            "--ld",
            ld,
            "--output",
            output,
            "--report",
            report,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("Committed:", result.Output, StringComparison.Ordinal);
        Assert.Contains("Report:", result.Output, StringComparison.Ordinal);
        Assert.Contains("replace-dp", result.Output, StringComparison.Ordinal);
        Assert.Contains("replace-ld", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Issues:", result.Error, StringComparison.Ordinal);
        byte[] bytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal([0x11, 0x22, 0xFF, 0xFF, 0, 0, 0x33, 0xFF], bytes);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            report,
            TestContext.Current.CancellationToken));
        JsonElement root = document.RootElement;
        Assert.Equal("synthetic-dp-replace", root.GetProperty("ProfileId").GetString());
        JsonElement operation = root.GetProperty("Operations")[0];
        Assert.Equal("replace-dp", operation.GetProperty("OperationId").GetString());
        Assert.Equal("dp-replacement", operation.GetProperty("SourceSpaceId").GetString());
        Assert.Equal("output-image", operation.GetProperty("TargetSpaceId").GetString());
        Assert.Equal(4, operation.GetProperty("TargetRange").GetProperty("Length").GetInt64());
        Assert.Equal("Replace synthetic DP declared partition.", operation.GetProperty("Reason").GetString());
    }

    /// <summary>Verifies NT51950 DP Replace uses the selected base length instead of the static maximum container.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("51950")]
    [InlineData("nt51950-dp-replace-dp-perspective")]
    public async Task DpReplaceBuildUsesNt51950SelectedBaseLength(string profileSelector)
    {
        using var workspace = TempWorkspace.Create();
        byte[] referenceBytes = [.. Enumerable.Repeat((byte)0xA5, 0x80000)];
        Array.Fill(referenceBytes, (byte)0x22, 0x0A000, 0x2D000);
        Array.Fill(referenceBytes, (byte)0x33, 0x37000, 0x1000);
        byte[] dpBytes = [.. Enumerable.Repeat((byte)0x11, 0x40000)];
        string reference = workspace.Write("reference.bin", referenceBytes);
        string dp = workspace.Write("dp.bin", dpBytes);
        string output = workspace.PathFor("nt51950-dp-replace.bin");

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "build",
            "--profile",
            profileSelector,
            "--ic-num",
            "single",
            "--base",
            reference,
            "--dp",
            dp,
            "--output",
            output,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("nt51950-dp-replace-dp-perspective", result.Output, StringComparison.Ordinal);
        byte[] bytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal(0x80000, bytes.Length);
        Assert.Equal(0x11, bytes[0x00000]);
        Assert.Equal(0x11, bytes[0x09FFF]);
        Assert.Equal(0x22, bytes[0x0A000]);
        Assert.Equal(0x22, bytes[0x36FFF]);
        Assert.Equal(0x33, bytes[0x37000]);
        Assert.Equal(0x33, bytes[0x37FFF]);
        Assert.Equal(0x11, bytes[0x38000]);
        Assert.Equal(0x00, bytes[0x40000]);
        Assert.Equal(0x00, bytes[0x7FFFF]);
    }

    /// <summary>Verifies NT51950 DP Replace rejects replacement inputs larger than the selected base length before output commit.</summary>
    [Fact]
    public async Task DpReplaceBuildRejectsOversizedNt51950ReplacementSize()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [.. Enumerable.Repeat((byte)0xA5, 0x40000)]);
        string dp = workspace.Write("dp.bin", [.. Enumerable.Repeat((byte)0x11, 0x40001)]);
        string output = workspace.PathFor("nt51950-dp-replace.bin");

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "build",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--dp",
            dp,
            "--output",
            output,
        ]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("input.address-space.length-mismatch", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    /// <summary>Verifies NT51950 DP Replace rejects cascade-only IC family input before workbench execution.</summary>
    [Fact]
    public async Task DpReplacePreviewRejectsNt51950IcFamilyOption()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [.. Enumerable.Repeat((byte)0xA5, 0x40000)]);
        string dp = workspace.Write("dp.bin", [.. Enumerable.Repeat((byte)0x11, 0x40000)]);

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "preview",
            "--profile",
            "NT51950",
            "--ic-family",
            "NT51",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--dp",
            dp,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("--ic-family is used only by cascade IC num profiles", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Verifies NT51950 DP Replace rejects numeric IC number input before workbench execution.</summary>
    [Fact]
    public async Task DpReplacePreviewRejectsNt51950NumericIcNumber()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [.. Enumerable.Repeat((byte)0xA5, 0x40000)]);
        string dp = workspace.Write("dp.bin", [.. Enumerable.Repeat((byte)0x11, 0x40000)]);

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "preview",
            "--profile",
            "NT51950",
            "--ic-num",
            "51950",
            "--base",
            reference,
            "--dp",
            dp,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("requires --ic-num single", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Verifies CtrlRAM Replace preview reports truncation warnings while succeeding.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewReportsOversizedInputTruncation()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [0, 0, 0, 0, 0, 0, 0, 0]);
        string ctrlram = workspace.Write("ctrlram.bin", [0xAA, 0xBB, 0xCC, 0xDD]);

        CliRunResult result = await RunCliAsync([
            "ctrlram-replace",
            "preview",
            "--profile",
            "synthetic-ctrlram-replace",
            "--ic-family",
            "NT51",
            "--ic-num",
            "932",
            "--base",
            reference,
            "--ctrlram",
            ctrlram,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("replace-ctrlram", result.Output, StringComparison.Ordinal);
        Assert.Contains("input.address-space.truncated", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Verifies real IC CtrlRAM Replace accepts multiple slot-specific replacement inputs in one CLI run.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewAcceptsRepeatedWorkbenchSlotInputs()
    {
        using var workspace = TempWorkspace.Create();
        string fixtureRoot = RepositoryPaths.FromRepositoryRoot("testdata", "golden", "ctrlram-replace");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtureRoot, "manifest.json")));
        JsonElement fixtureCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("id").GetString() == "nt51927-2chip-self-20260705");
        string basePath = ManifestPath(fixtureRoot, fixtureCase.GetProperty("base").GetProperty("path"));
        JsonElement normalMaster = fixtureCase.GetProperty("replacementInputs")
            .EnumerateArray()
            .Single(input => input.GetProperty("slotId").GetString() == "replace-ctrlram-normal-master");
        JsonElement vnSlaveRight = fixtureCase.GetProperty("replacementInputs")
            .EnumerateArray()
            .Single(input => input.GetProperty("slotId").GetString() == "replace-ctrlram-vn-slave-r");
        string normalMasterPath = ManifestPath(fixtureRoot, normalMaster.GetProperty("file").GetProperty("path"));
        string vnSlaveRightPath = ManifestPath(fixtureRoot, vnSlaveRight.GetProperty("file").GetProperty("path"));
        string report = workspace.PathFor("ctrlram-report.json");

        CliRunResult result = await RunCliAsync([
            "ctrlram-replace",
            "preview",
            "--profile",
            "NT51927",
            "--ic-num",
            "2",
            "--base",
            basePath,
            "--ctrlram",
            $"replace-ctrlram-normal-master={normalMasterPath}",
            "--ctrlram",
            $"vn-slave-r={vnSlaveRightPath}",
            "--report",
            report,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("Profile: nt51927-ctrlram-replace-workbench (NT51927)", result.Output, StringComparison.Ordinal);
        Assert.Contains("postbuild-twochip", result.Output, StringComparison.Ordinal);
        Assert.True(File.Exists(report), report);
        using var reportDocument = JsonDocument.Parse(await File.ReadAllTextAsync(
            report,
            TestContext.Current.CancellationToken));
        JsonElement root = reportDocument.RootElement;
        Assert.Equal("nt51927-ctrlram-replace-workbench", root.GetProperty("ProfileId").GetString());
        Assert.Equal(3, root.GetProperty("Inputs").GetArrayLength());
        Assert.Contains(root.GetProperty("Inputs").EnumerateArray(), input =>
            input.GetProperty("AddressSpaceId").GetString() == "replace-ctrlram-normal-master");
        Assert.Contains(root.GetProperty("Inputs").EnumerateArray(), input =>
            input.GetProperty("AddressSpaceId").GetString() == "replace-ctrlram-vn-slave-r");
        JsonElement operation = Assert.Single(root.GetProperty("Operations").EnumerateArray());
        Assert.Equal("RunExternalProcessor", operation.GetProperty("Kind").GetString());
    }

    /// <summary>Verifies General Replace build writes the mapped output after internal preview approval.</summary>
    [Fact]
    public async Task GeneralReplaceBuildWritesMappedOutput()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [0, 1, 2, 3, 4, 5, 6, 7]);
        string input = workspace.Write("input.bin", [0xAA, 0xBB]);
        string output = workspace.PathFor("out.bin");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "synthetic-general-replace",
            "--ic-num",
            "51920",
            "--base",
            reference,
            "--input",
            input,
            "--source-start",
            "0",
            "--target-start",
            "0x2",
            "--length",
            "2",
            "--output",
            output,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Committed:", result.Output, StringComparison.Ordinal);
        byte[] bytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal([0, 1, 0xAA, 0xBB, 4, 5, 6, 7], bytes);
    }

    /// <summary>Verifies real IC General Replace CLI accepts repeated workbench mapping rows.</summary>
    [Fact]
    public async Task GeneralReplaceBuildAcceptsRepeatedWorkbenchMappings()
    {
        using var workspace = TempWorkspace.Create();
        byte[] baseBytes = CreatePattern(0x40000, 0x40);
        string reference = workspace.Write("reference.bin", baseBytes);
        string firstInput = workspace.Write("first.bin", [0xA5, 0x5A]);
        string secondInput = workspace.Write("second.bin", [0xC3]);
        string output = workspace.PathFor("general-replace.bin");
        string report = workspace.PathFor("general-replace-report.json");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--mapping",
            $"0x100+0x2={firstInput}",
            "--mapping",
            $"0x38000+0x1={secondInput}",
            "--output",
            output,
            "--report",
            report,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("Experience: general-replace", result.Output, StringComparison.Ordinal);
        Assert.Contains("nt51950-general-replace-workbench", result.Output, StringComparison.Ordinal);
        byte[] bytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal(baseBytes.Length, bytes.Length);
        Assert.Equal(0xA5, bytes[0x100]);
        Assert.Equal(0x5A, bytes[0x101]);
        Assert.Equal(0xC3, bytes[0x38000]);
        Assert.Equal(baseBytes[0x102], bytes[0x102]);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            report,
            TestContext.Current.CancellationToken));
        JsonElement root = document.RootElement;
        Assert.Equal("nt51950-general-replace-workbench", root.GetProperty("ProfileId").GetString());
        Assert.Equal("general-replace", root.GetProperty("ExperienceId").GetString());
        Assert.Equal(3, root.GetProperty("Inputs").GetArrayLength());
        Assert.Equal(2, root.GetProperty("Operations").GetArrayLength());
    }

    /// <summary>Verifies real IC General Replace CLI runs postbuild when a mapping touches TP/CtrlRAM.</summary>
    [Fact]
    public async Task GeneralReplacePreviewRunsPostbuildForWorkbenchTpMapping()
    {
        using var workspace = TempWorkspace.Create();
        string reference = GoldenPath("expected/51950/dp-256k/flash.bin");
        byte[] baseBytes = await File.ReadAllBytesAsync(reference, TestContext.Current.CancellationToken);
        string input = workspace.Write("input.bin", baseBytes[0x22C00..0x22C02]);
        string report = workspace.PathFor("general-replace-tp-report.json");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "preview",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--mapping",
            $"0x22C00+0x2={input}",
            "--report",
            report,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("postbuild-singlechip", result.Output, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            report,
            TestContext.Current.CancellationToken));
        JsonElement root = document.RootElement;
        Assert.Equal("general-replace", root.GetProperty("ExperienceId").GetString());
        Assert.Collection(
            root.GetProperty("Operations").EnumerateArray(),
            operation => Assert.Equal("ReplaceRange", operation.GetProperty("Kind").GetString()),
            operation =>
            {
                Assert.Equal("RunExternalProcessor", operation.GetProperty("Kind").GetString());
                Assert.Equal("nfc.nt51950.ctrlram-postbuild-v1", operation.GetProperty("ProcessorId").GetString());
                Assert.Equal("legacy-combiner-1.13.0", operation.GetProperty("ToolBindingId").GetString());
            });
    }

    /// <summary>Verifies malformed real IC General Replace mapping paths are rejected before planning.</summary>
    [Fact]
    public async Task GeneralReplacePreviewRejectsEmptyWorkbenchMappingPath()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", CreatePattern(0x40000, 0x30));

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "preview",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--mapping",
            "0x100+0x2=  ",
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("--mapping path must not be empty", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Rejects Replace build outputs that would overwrite an input BIN.</summary>
    [Fact]
    public async Task DpReplaceBuildRejectsOutputPathThatAliasesInput()
    {
        using var workspace = TempWorkspace.Create();
        byte[] referenceBytes = [0, 0, 0, 0, 0, 0, 0, 0];
        string reference = workspace.Write("reference.bin", referenceBytes);
        string dp = workspace.Write("dp.bin", [0x11, 0x22]);
        string ld = workspace.Write("ld.bin", [0x33]);

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "build",
            "--profile",
            "synthetic-dp-replace",
            "--ic-num",
            "51920",
            "--base",
            reference,
            "--dp",
            dp,
            "--ld",
            ld,
            "--output",
            reference,
            "--overwrite",
        ]);

        Assert.Equal(70, result.ExitCode);
        Assert.Contains("Output path must not overwrite input artifact", result.Error, StringComparison.Ordinal);
        Assert.Equal(referenceBytes, await File.ReadAllBytesAsync(reference, TestContext.Current.CancellationToken));
    }

    /// <summary>Rejects Replace report paths that would overwrite the build output.</summary>
    [Fact]
    public async Task DpReplaceBuildRejectsReportPathThatAliasesOutput()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [0, 0, 0, 0, 0, 0, 0, 0]);
        string dp = workspace.Write("dp.bin", [0x11, 0x22]);
        string ld = workspace.Write("ld.bin", [0x33]);
        string output = workspace.PathFor("out.bin");

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "build",
            "--profile",
            "synthetic-dp-replace",
            "--ic-num",
            "51920",
            "--base",
            reference,
            "--dp",
            dp,
            "--ld",
            ld,
            "--output",
            output,
            "--report",
            output,
        ]);

        Assert.Equal(70, result.ExitCode);
        Assert.Contains("Report path must not overwrite built firmware output", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    /// <summary>Verifies Replace commands require IC number selection before planning.</summary>
    [Fact]
    public async Task ReplacePreviewRejectsMissingIcNumber()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [0, 0, 0, 0, 0, 0, 0, 0]);
        string dp = workspace.Write("dp.bin", [0x11, 0x22]);
        string ld = workspace.Write("ld.bin", [0x33]);

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "preview",
            "--profile",
            "synthetic-dp-replace",
            "--base",
            reference,
            "--dp",
            dp,
            "--ld",
            ld,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("--ic-num is required", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Verifies Replace commands reject firmware inputs that the selected profile would ignore.</summary>
    [Fact]
    public async Task ReplacePreviewRejectsUnusedFirmwareInputOption()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [0, 0, 0, 0, 0, 0, 0, 0]);
        string ctrlram = workspace.Write("ctrlram.bin", [0xAA, 0xBB]);
        string dp = workspace.Write("dp.bin", [0x11, 0x22]);

        CliRunResult result = await RunCliAsync([
            "ctrlram-replace",
            "preview",
            "--profile",
            "synthetic-ctrlram-replace",
            "--ic-family",
            "NT51",
            "--ic-num",
            "932",
            "--base",
            reference,
            "--ctrlram",
            ctrlram,
            "--dp",
            dp,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("option '--dp' is not used", result.Error, StringComparison.Ordinal);
    }

    private static async Task<CliRunResult> RunCliAsync(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = await CliApplication
            .RunAsync(args, output, error, TestContext.Current.CancellationToken);
        return new CliRunResult(exitCode, output.ToString(), error.ToString());
    }

    private static string ManifestPath(string fixtureRoot, JsonElement pathElement)
    {
        return Path.Combine(fixtureRoot, pathElement.GetString()!.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string GoldenPath(string relativePath)
    {
        return Path.Combine(
            RepositoryPaths.FindRepositoryRoot(),
            "testdata",
            "golden",
            "standard-merge-gen-flash",
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static byte[] CreatePattern(int length, byte seed)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(seed + index));
        }

        return bytes;
    }

    private sealed record CliRunResult(int ExitCode, string Output, string Error);

}
