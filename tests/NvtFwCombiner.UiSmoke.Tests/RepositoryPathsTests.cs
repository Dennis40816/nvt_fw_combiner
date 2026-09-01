using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Coverage for shared repository fixture path helpers used by UI smoke tests.</summary>
[Collection(UiProcessWideObservationCollection.Name)]
public sealed class RepositoryPathsTests
{
    /// <summary>Verifies manifest fixture paths cannot escape their declared root.</summary>
    [Fact]
    public void ManifestPathRejectsRootEscape()
    {
        using var document = JsonDocument.Parse("""{ "path": "../outside.bin" }""");
        string root = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-root-{Guid.NewGuid():N}");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => RepositoryPaths.ManifestPath(root, document.RootElement));

        Assert.Contains("escapes root", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Configured repository identity wins even when the assembly lives elsewhere.</summary>
    [Fact]
    public void ConfiguredAbsoluteRepositoryRootOverridesAssemblyDiscovery()
    {
        string root = CreateDirectory("configured");
        try
        {
            File.WriteAllText(Path.Combine(root, "NvtFwCombiner.slnx"), string.Empty);

            WithConfiguredRepositoryRoot(
                root,
                () => Assert.Equal(Path.GetFullPath(root), RepositoryPaths.FindRepositoryRoot()));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Invalid configured repository identity fails closed instead of falling back.</summary>
    [Fact]
    public void ConfiguredInvalidRepositoryRootsFailClosed()
    {
        string owner = CreateDirectory("invalid");
        try
        {
            string missingDirectory = Path.Combine(owner, "missing");
            string missingMarker = Path.Combine(owner, "missing-marker");
            _ = Directory.CreateDirectory(missingMarker);
            string nonFileMarker = Path.Combine(owner, "non-file-marker");
            _ = Directory.CreateDirectory(Path.Combine(nonFileMarker, "NvtFwCombiner.slnx"));

            foreach (string configured in new[]
            {
                " ",
                "relative-repository",
                missingDirectory,
                missingMarker,
                nonFileMarker,
            })
            {
                WithConfiguredRepositoryRoot(
                    configured,
                    () => Assert.Throws<InvalidOperationException>(
                        RepositoryPaths.FindRepositoryRoot));
            }
        }
        finally
        {
            Directory.Delete(owner, recursive: true);
        }
    }

    private static string CreateDirectory(string label)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"nvt-fw-combiner-repository-paths-{label}-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        return path;
    }

    private static void WithConfiguredRepositoryRoot(string value, Action assertion)
    {
        const string variable = "NFC_TEST_REPOSITORY_ROOT";
        string? previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, value);
            assertion();
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }
}
