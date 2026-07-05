using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Coverage for shared repository fixture path helpers used by UI smoke tests.</summary>
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
}
