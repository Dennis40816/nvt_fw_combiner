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

    /// <summary>Unset test-data configuration preserves the repository copy.</summary>
    [Fact]
    public void UnsetTestDataRootUsesRepositoryCopy()
    {
        WithConfiguredTestDataRoot(null, () => Assert.Equal(
            RepositoryPaths.FromRepositoryRoot("testdata"), RepositoryPaths.FindTestDataRoot()));
    }

    /// <summary>An existing absolute directory supplies fixture paths without a repository marker.</summary>
    [Fact]
    public void ConfiguredTestDataRootOverridesRepositoryCopy()
    {
        string root = CreateDirectory("testdata");
        try
        {
            WithConfiguredTestDataRoot(root, () =>
            {
                Assert.Equal(Path.GetFullPath(root), RepositoryPaths.FindTestDataRoot());
                Assert.Equal(Path.Combine(root, "golden", "example.json"),
                    RepositoryPaths.FromTestData("golden/example.json"));
                Assert.Equal(Path.Combine(root, "golden", "example.json"),
                    RepositoryPaths.FromTestData("golden", "example.json"));
                Assert.Equal(Path.Combine(root, "golden", "canonical"), CanonicalGoldenTestData.Root);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Invalid test-data configuration never falls back to committed fixtures.</summary>
    [Fact]
    public void ConfiguredInvalidTestDataRootsFailClosed()
    {
        string root = CreateDirectory("invalid-testdata");
        try
        {
            string file = Path.Combine(root, "example.json");
            File.WriteAllText(file, "{}");
            foreach (string configured in new[] { " ", "relative/example", Path.Combine(root, "missing"), file })
            {
                WithConfiguredTestDataRoot(configured, () =>
                {
                    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                        RepositoryPaths.FindTestDataRoot);
                    Assert.Contains("NFC_TESTDATA_ROOT", exception.Message, StringComparison.Ordinal);
                    _ = Assert.Throws<InvalidOperationException>(() => RepositoryPaths.FromTestData("example.json"));
                });
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WithConfiguredTestDataRoot(string? value, Action assertion)
    {
        const string variable = "NFC_TESTDATA_ROOT";
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
