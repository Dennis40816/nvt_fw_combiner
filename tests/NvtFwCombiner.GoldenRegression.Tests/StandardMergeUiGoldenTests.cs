using System.Text.Json;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.GoldenRegression.Tests;

/// <summary>Golden tests that exercise the workbench command path for Standard Merge.</summary>
public sealed class StandardMergeUiGoldenTests
{
    /// <summary>Verifies the workbench facade can build every Standard Merge golden case byte-for-byte.</summary>
    [Fact]
    public async Task UiShellBuildStandardMergeMatchesGoldenBytes()
    {
        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-golden-{Guid.NewGuid():N}");

        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            foreach (JsonElement goldenCase in manifestDocument.RootElement.GetProperty("cases").EnumerateArray())
            {
                await VerifyGoldenCaseAsync(goldenRoot, tempRoot, goldenCase);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies the workbench command path can build owner-confirmed Standard Merge aliases.</summary>
    [Theory]
    [InlineData("51917", "51927")]
    [InlineData("51919", "51929")]
    public async Task UiShellBuildStandardMergeAliasMatchesReferenceGoldenBytes(string aliasIc, string referenceIc)
    {
        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(item => item.GetProperty("ic").GetString() == referenceIc)
            .Clone();
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-ui-alias-{aliasIc}-{Guid.NewGuid():N}");

        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            Dictionary<string, string> slotPaths = [];
            foreach (JsonProperty input in goldenCase.GetProperty("inputs").EnumerateObject())
            {
                string originalPath = ManifestPath(goldenRoot, input.Value);
                string copiedPath = Path.Combine(tempRoot, $"{input.Name}.bin");
                File.Copy(originalPath, copiedPath);
                slotPaths[input.Name] = copiedPath;
            }

            WorkbenchRunResult result = await WorkbenchCompositionService.RunStandardMergeAsync(
                    $"NT{aliasIc}",
                    slotPaths,
                    build: true,
                    CancellationToken.None);

            string outputPath = result.CommittedOutputId ?? result.OutputFileName;
            Assert.True(result.Succeeded, result.Status);
            Assert.True(File.Exists(outputPath), outputPath);
            Assert.Equal(
                File.ReadAllBytes(ManifestPath(goldenRoot, goldenCase.GetProperty("expectedOutput"))),
                File.ReadAllBytes(outputPath));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static async ValueTask VerifyGoldenCaseAsync(
        string goldenRoot,
        string tempRoot,
        JsonElement goldenCase)
    {
        string ic = goldenCase.GetProperty("ic").GetString()!;
        string caseRoot = Path.Combine(tempRoot, ic);
        _ = Directory.CreateDirectory(caseRoot);

        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal);
        foreach (JsonProperty input in goldenCase.GetProperty("inputs").EnumerateObject())
        {
            string originalPath = ManifestPath(goldenRoot, input.Value);
            string copiedPath = Path.Combine(caseRoot, $"{input.Name}.bin");
            File.Copy(originalPath, copiedPath);
            slotPaths[input.Name] = copiedPath;
        }

        WorkbenchRunResult result = await WorkbenchCompositionService.RunStandardMergeAsync(
                $"NT{ic}",
                slotPaths,
                build: true,
                CancellationToken.None)
            .ConfigureAwait(false);

        string outputPath = result.CommittedOutputId ?? result.OutputFileName;
        Assert.True(result.Succeeded, result.Status);
        Assert.True(File.Exists(outputPath), outputPath);
        Assert.Equal(
            File.ReadAllBytes(ManifestPath(goldenRoot, goldenCase.GetProperty("expectedOutput"))),
            File.ReadAllBytes(outputPath));
    }

    private static string ManifestPath(string goldenRoot, JsonElement manifestFile)
    {
        string relativePath = manifestFile.GetProperty("path").GetString()!;
        return Path.Combine(goldenRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SPEC.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
