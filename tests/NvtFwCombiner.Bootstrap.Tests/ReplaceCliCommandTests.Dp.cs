using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    /// <summary>Selector-hidden DP profiles remain compiled internally but cannot be invoked from CLI.</summary>
    [Theory]
    [InlineData("NT51920")]
    [InlineData("51920")]
    [InlineData("nt51920-dp-replace-gen-flash")]
    [InlineData("NT51930")]
    [InlineData("51930")]
    [InlineData("nt51930-dp-replace-flashmap")]
    [InlineData("NT51931")]
    [InlineData("51931")]
    [InlineData("nt51931-dp-replace-gen-flash")]
    public async Task DpReplaceRejectsSelectorHiddenProfiles(string profileSelector)
    {
        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "preview",
            "--profile",
            profileSelector,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains($"unknown dp-replace profile '{profileSelector}'", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("--ic-num is required", result.Error, StringComparison.Ordinal);
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

    /// <summary>NT51928 CLI accepts Initial Code-only and preserves Reference LDC bytes.</summary>
    [Fact]
    public async Task Nt51928DpReplaceBuildAcceptsInitialCodeWithoutLdc()
    {
        using var workspace = TempWorkspace.Create();
        byte[] referenceBytes = [.. Enumerable.Repeat((byte)0x22, 0x80000)];
        byte[] initialCodeBytes = [.. Enumerable.Repeat((byte)0xA1, 0x80000)];
        string output = workspace.PathFor("nt51928-dp-replace.bin");

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "build",
            "--profile",
            "NT51928",
            "--ic-num",
            "single",
            "--base",
            workspace.Write("reference.bin", referenceBytes),
            "--dp",
            workspace.Write("initial-code.bin", initialCodeBytes),
            "--output",
            output,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        byte[] actual = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal(0xA1, actual[0x3C000]);
        Assert.Equal(0xA1, actual[0x3FFFF]);
        Assert.Equal(0x22, actual[0x40000]);
        Assert.Equal(0x22, actual[0x61FFF]);
    }

    /// <summary>NT51928 CLI accepts LDC-only and preserves Reference Initial Code bytes.</summary>
    [Fact]
    public async Task Nt51928DpReplaceBuildAcceptsLdcWithoutInitialCode()
    {
        using var workspace = TempWorkspace.Create();
        byte[] referenceBytes = [.. Enumerable.Repeat((byte)0x22, 0x80000)];
        byte[] ldcBytes = [.. Enumerable.Repeat((byte)0xB2, 0x80000)];
        string output = workspace.PathFor("nt51928-dp-replace.bin");

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "build",
            "--profile",
            "NT51928",
            "--ic-num",
            "single",
            "--base",
            workspace.Write("reference.bin", referenceBytes),
            "--ldc",
            workspace.Write("ldc.bin", ldcBytes),
            "--output",
            output,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        byte[] actual = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal(0x22, actual[0x3C000]);
        Assert.Equal(0x22, actual[0x3FFFF]);
        Assert.Equal(0xB2, actual[0x40000]);
        Assert.Equal(0xB2, actual[0x61FFF]);
    }

    /// <summary>NT51928 CLI rejects Reference-only requests before any output is created.</summary>
    [Fact]
    public async Task Nt51928DpReplaceBuildRequiresAtLeastOneReplacement()
    {
        using var workspace = TempWorkspace.Create();
        string output = workspace.PathFor("nt51928-dp-replace.bin");

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "build",
            "--profile",
            "NT51928",
            "--ic-num",
            "single",
            "--base",
            workspace.Write("reference.bin", new byte[0x80000]),
            "--output",
            output,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("at least one of --dp or --ldc is required", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    /// <summary>An invalid selected LDC blocks a valid Initial Code instead of downgrading the request.</summary>
    [Fact]
    public async Task Nt51928DpReplaceBuildDoesNotIgnoreInvalidSelectedLdc()
    {
        using var workspace = TempWorkspace.Create();
        string output = workspace.PathFor("nt51928-dp-replace.bin");

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "build",
            "--profile",
            "NT51928",
            "--ic-num",
            "single",
            "--base",
            workspace.Write("reference.bin", new byte[0x80000]),
            "--dp",
            workspace.Write("initial-code.bin", new byte[0x80000]),
            "--ldc",
            workspace.Write("invalid-ldc.bin", new byte[0x40000]),
            "--output",
            output,
        ]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(CompositionIssueCodes.InputAddressSpaceLengthMismatch, result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    /// <summary>An invalid selected Initial Code blocks a valid LDC instead of downgrading the request.</summary>
    [Fact]
    public async Task Nt51928DpReplaceBuildDoesNotIgnoreInvalidSelectedInitialCode()
    {
        using var workspace = TempWorkspace.Create();
        string output = workspace.PathFor("nt51928-dp-replace.bin");

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "build",
            "--profile",
            "NT51928",
            "--ic-num",
            "single",
            "--base",
            workspace.Write("reference.bin", new byte[0x80000]),
            "--dp",
            workspace.Write("invalid-initial-code.bin", new byte[0x3FFFF]),
            "--ldc",
            workspace.Write("ldc.bin", new byte[0x80000]),
            "--output",
            output,
        ]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(CompositionIssueCodes.InputAddressSpaceLengthMismatch, result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }
}
