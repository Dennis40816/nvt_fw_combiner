using NvtFwCombiner.DistributionLauncher;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Verifies the fixed release-only embedded-payload extraction surface.</summary>
public sealed class ReleasePayloadExtractionTests
{
    /// <summary>Exact release arguments materialize only descriptor and Bootstrap bytes.</summary>
    [Fact]
    public void ExactCommandExtractsOnlyTheTwoEmbeddedReleasePayloads()
    {
        using var workspace = TempWorkspace.Create();
        byte[] descriptor = "descriptor"u8.ToArray();
        byte[] bootstrap = [0, 1, 2, 3, 255];
        Dictionary<string, byte[]> resources = new(StringComparer.Ordinal)
        {
            [ReleasePayloadExtraction.DescriptorResourceName] = descriptor,
            [ReleasePayloadExtraction.BootstrapResourceName] = bootstrap,
        };
        string output = workspace.PathFor("extracted");

        int exitCode = ReleasePayloadExtraction.Execute(
            [ReleasePayloadExtraction.Command, output],
            name => resources.TryGetValue(name, out byte[]? bytes)
                ? new MemoryStream(bytes, writable: false)
                : null);

        Assert.Equal(0, exitCode);
        Assert.Equal(
            descriptor,
            File.ReadAllBytes(Path.Combine(output, ReleasePayloadExtraction.DescriptorFileName)));
        Assert.Equal(
            bootstrap,
            File.ReadAllBytes(Path.Combine(output, ReleasePayloadExtraction.BootstrapFileName)));
        Assert.Equal(
            [ReleasePayloadExtraction.BootstrapFileName, ReleasePayloadExtraction.DescriptorFileName],
            Directory.EnumerateFiles(output).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    /// <summary>Unknown arguments cannot create or mutate an extraction destination.</summary>
    [Fact]
    public void InvalidCommandDoesNotCreateAnOutputDirectory()
    {
        using var workspace = TempWorkspace.Create();
        string output = workspace.PathFor("not-created");

        int exitCode = ReleasePayloadExtraction.Execute(
            ["--unknown", output],
            _ => throw new InvalidOperationException("Resources must not be opened."));

        Assert.Equal((int)DistributionLauncherExitCode.HostUnavailable, exitCode);
        Assert.False(Directory.Exists(output));
    }

    /// <summary>An existing non-empty destination is preserved and rejected.</summary>
    [Fact]
    public void NonEmptyOutputDirectoryIsRejectedWithoutMutation()
    {
        using var workspace = TempWorkspace.Create();
        string output = workspace.PathFor("existing");
        _ = Directory.CreateDirectory(output);
        string sentinel = Path.Combine(output, "sentinel.txt");
        File.WriteAllText(sentinel, "keep");

        int exitCode = ReleasePayloadExtraction.Execute(
            [ReleasePayloadExtraction.Command, output],
            _ => throw new InvalidOperationException("Resources must not be opened."));

        Assert.Equal((int)DistributionLauncherExitCode.HostUnavailable, exitCode);
        Assert.Equal("keep", File.ReadAllText(sentinel));
        _ = Assert.Single(Directory.EnumerateFileSystemEntries(output));
    }
}
