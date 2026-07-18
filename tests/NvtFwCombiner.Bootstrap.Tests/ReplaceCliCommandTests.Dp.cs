using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    /// <summary>Verifies every NT51930 selector reaches the canonical-map V2 DP Replace route.</summary>
    [Theory]
    [InlineData("NT51930")]
    [InlineData("51930")]
    [InlineData("nt51930-dp-replace-flashmap")]
    public async Task DpReplaceBuildUsesNt51930CanonicalDpRange(string profileSelector)
    {
        using var workspace = TempWorkspace.Create();
        string output = workspace.PathFor("nt51930-dp-replace.bin");
        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "build",
            "--profile",
            profileSelector,
            "--ic-num",
            "single",
            "--base",
            workspace.Write("reference.bin", [.. Enumerable.Repeat((byte)0xA5, 0x40000)]),
            "--dp",
            workspace.Write("dp.bin", [.. Enumerable.Repeat((byte)0x11, 0x40000)]),
            "--output",
            output,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("nt51930-dp-replace-flashmap", result.Output, StringComparison.Ordinal);
        byte[] bytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal(0x40000, bytes.Length);
        Assert.Equal(0x11, bytes[0]);
        Assert.Equal(0x11, bytes[0x5FFF]);
        Assert.Equal(0xA5, bytes[0x6000]);
        Assert.Equal(0xA5, bytes[^1]);
    }

    /// <summary>Verifies NT51950 DP Replace restores TP only while customer information follows replacement DP.</summary>
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
        Assert.Equal(0x11, bytes[0x37000]);
        Assert.Equal(0x11, bytes[0x37FFF]);
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
        Assert.Contains(CompositionIssueCodes.InputAddressSpaceLengthMismatch, result.Error, StringComparison.Ordinal);
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
        Assert.Contains("unknown option '--ic-family'", result.Error, StringComparison.Ordinal);
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
}
