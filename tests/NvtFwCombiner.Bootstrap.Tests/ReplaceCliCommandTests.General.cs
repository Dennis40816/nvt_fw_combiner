using System.Text.Json;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    /// <summary>Locks NT51926 single full-Flash DP-only General Replace to the reviewed V2 candidate.</summary>
    [Fact]
    public async Task Nt51926GeneralReplaceDpOnlyBuildUsesV2Candidate()
    {
        using var workspace = TempWorkspace.Create();
        byte[] baseBytes = await File.ReadAllBytesAsync(
            GoldenPath("expected/51926/flash.bin"),
            TestContext.Current.CancellationToken);
        string reference = workspace.Write("reference.bin", baseBytes);
        string source = workspace.Write("dp-source.bin", [0xA5, 0x5A]);
        string output = workspace.PathFor("general-replace.bin");
        string report = workspace.PathFor("general-replace-report.json");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "NT51926",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--mapping",
            $"0x3E020+0x2={source}",
            "--output",
            output,
            "--report",
            report,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(reference, TestContext.Current.CancellationToken));
        byte[] outputBytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal([0xA5, 0x5A], outputBytes[0x3E020..0x3E022]);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            report,
            TestContext.Current.CancellationToken));
        JsonElement root = document.RootElement;
        Assert.Equal("nt51926-general-replace-dp-single-candidate", root.GetProperty("ProfileId").GetString());
        JsonElement operation = Assert.Single(root.GetProperty("Operations").EnumerateArray());
        Assert.Equal("ReplaceRange", operation.GetProperty("Kind").GetString());
        Assert.Equal(JsonValueKind.Null, operation.GetProperty("ProcessorId").ValueKind);
    }

    /// <summary>Verifies retired real-IC General Replace mappings fail closed without output.</summary>
    [Fact]
    public async Task GeneralReplaceBuildWithRepeatedWorkbenchMappingsFailsClosed()
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

        await AssertGeneralReplaceWorkflowNotSupportedAsync(result, report, output);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(reference, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies retired TP-touching General Replace preview fails closed.</summary>
    [Fact]
    public async Task GeneralReplacePreviewWithWorkbenchTpMappingFailsClosed()
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

        await AssertGeneralReplaceWorkflowNotSupportedAsync(result, report, outputPath: null);
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

    /// <summary>Verifies valid CLI patches fail closed after legacy General Replace retirement.</summary>
    [Fact]
    public async Task GeneralReplaceBuildWithVirtualPatchAndFillFailsClosed()
    {
        using var workspace = TempWorkspace.Create();
        byte[] baseBytes = CreatePattern(0x40000, 0x60);
        string reference = workspace.Write("reference.bin", baseBytes);
        string output = workspace.PathFor("general-replace-patch.bin");
        string report = workspace.PathFor("general-replace-patch-report.json");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--patch",
            "0x100+0x2=A55A",
            "--fill",
            "0x110+0x3=FF",
            "--output",
            output,
            "--report",
            report,
        ]);

        await AssertGeneralReplaceWorkflowNotSupportedAsync(result, report, output);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(reference, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies a real IC General Replace build cannot overwrite its immutable base BIN.</summary>
    [Fact]
    public async Task GeneralReplaceBuildRejectsOutputPathThatAliasesBase()
    {
        using var workspace = TempWorkspace.Create();
        byte[] baseBytes = CreatePattern(0x40000, 0x63);
        string reference = workspace.Write("reference.bin", baseBytes);

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--patch",
            "0x100+0x1=A5",
            "--output",
            reference,
            "--overwrite",
        ]);

        Assert.Equal(70, result.ExitCode);
        Assert.Contains("Output path must not overwrite input artifact", result.Error, StringComparison.Ordinal);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(reference, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies malformed CLI patch bytes receive the shared workbench validation issue.</summary>
    [Fact]
    public async Task GeneralReplacePreviewRejectsMalformedVirtualPatch()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", CreatePattern(0x40000, 0x65));

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "preview",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--patch",
            "0x100+0x2=ABC",
        ]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("ui.general-replace.patch-hex-invalid", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Rejects retired fixed-profile range options at the workflow allowlist.</summary>
    [Theory]
    [InlineData("--input", "ignored.bin")]
    [InlineData("--source-start", "0")]
    [InlineData("--target-start", "0x100")]
    [InlineData("--length", "1")]
    public async Task GeneralReplaceRejectsRetiredFixedProfileOptions(string option, string value)
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", CreatePattern(0x40000, 0x66));

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "preview",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--patch",
            "0x100+0x1=A5",
            option,
            value,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains($"unknown option '{option}'", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Rejects General Replace-only mapping and patch options in other Replace command groups.</summary>
    [Theory]
    [InlineData("dp-replace", "--mapping")]
    [InlineData("dp-replace", "--patch")]
    [InlineData("ctrlram-replace", "--fill")]
    public async Task NonGeneralReplaceRejectsGeneralAuthoringOptions(string command, string option)
    {
        CliRunResult result = await RunCliAsync([
            command,
            "preview",
            option,
            "0x100+0x1=FF",
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains($"unknown option '{option}'", result.Error, StringComparison.Ordinal);
    }

    private static async Task AssertGeneralReplaceWorkflowNotSupportedAsync(
        CliRunResult result,
        string reportPath,
        string? outputPath)
    {
        Assert.Equal(1, result.ExitCode);
        if (outputPath is not null)
        {
            Assert.False(File.Exists(outputPath));
        }

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            reportPath,
            TestContext.Current.CancellationToken));
        Assert.Contains(
            document.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == "replace.workflow.not-supported");
        Assert.False(document.RootElement.GetProperty("Output").GetProperty("Committed").GetBoolean());
    }
}
