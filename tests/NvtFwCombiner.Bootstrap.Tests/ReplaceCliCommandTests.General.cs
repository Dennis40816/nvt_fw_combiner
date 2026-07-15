using System.Text.Json;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
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

    /// <summary>Verifies CLI hexadecimal patches use the same virtual General Replace workbench path.</summary>
    [Fact]
    public async Task GeneralReplaceBuildAcceptsVirtualPatchAndFill()
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

        Assert.Equal(0, result.ExitCode);
        byte[] bytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal([0xA5, 0x5A], bytes[0x100..0x102]);
        Assert.Equal([0xFF, 0xFF, 0xFF], bytes[0x110..0x113]);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(reference, TestContext.Current.CancellationToken));

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(report, TestContext.Current.CancellationToken));
        Assert.Contains(
            document.RootElement.GetProperty("Inputs").EnumerateArray(),
            input => input.GetProperty("ArtifactId").GetString() == "general-patch-1");
        Assert.Contains(
            document.RootElement.GetProperty("Inputs").EnumerateArray(),
            input => input.GetProperty("ArtifactId").GetString() == "general-fill-1");
        Assert.Equal(2, document.RootElement.GetProperty("Operations").GetArrayLength());
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

    /// <summary>Rejects fixed-profile range options instead of silently ignoring them on real IC runs.</summary>
    [Theory]
    [InlineData("--input", "ignored.bin")]
    [InlineData("--source-start", "0")]
    [InlineData("--target-start", "0x100")]
    [InlineData("--length", "1")]
    public async Task GeneralReplaceRealIcRejectsFixedProfileOptions(string option, string value)
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
        Assert.Contains("does not accept fixed-profile option", result.Error, StringComparison.Ordinal);
        Assert.Contains(option, result.Error, StringComparison.Ordinal);
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
}
