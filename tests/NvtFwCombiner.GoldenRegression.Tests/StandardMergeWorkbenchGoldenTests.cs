using System.Text.Json;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.GoldenRegression.Tests;

/// <summary>Golden tests that exercise the workbench command path for Standard Merge.</summary>
public sealed class StandardMergeWorkbenchGoldenTests
{
    /// <summary>Verifies the workbench facade can build every Standard Merge golden case byte-for-byte.</summary>
    [Fact]
    public async Task WorkbenchBuildStandardMergeMatchesGoldenBytes()
    {
        string goldenRoot = CanonicalGoldenTestData.Root;
        using JsonDocument manifestDocument = CanonicalGoldenTestData.LoadDirectWorkflowManifest("standard-merge");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-golden");

        JsonElement[] goldenCases = [.. manifestDocument.RootElement.GetProperty("cases").EnumerateArray()];

        foreach (JsonElement goldenCase in goldenCases)
        {
            await VerifyGoldenCaseAsync(goldenRoot, workspace.Root, goldenCase);
        }
    }

    /// <summary>Verifies the workbench command path can build owner-confirmed Standard Merge aliases.</summary>
    [Theory]
    [MemberData(nameof(StandardMergeAliases))]
    public async Task WorkbenchBuildStandardMergeAliasMatchesReferenceGoldenBytes(string aliasIc, string referenceIc)
    {
        string goldenRoot = CanonicalGoldenTestData.Root;
        using JsonDocument manifestDocument = CanonicalGoldenTestData.LoadDirectWorkflowManifest("standard-merge");
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(item => item.GetProperty("ic").GetString() == referenceIc)
            .Clone();
        using var workspace = TempWorkspace.Create($"nvt-fw-combiner-ui-alias-{aliasIc}");

        Dictionary<string, string> slotPaths = [];
        foreach (JsonProperty input in goldenCase.GetProperty("inputs").EnumerateObject())
        {
            string originalPath = RepositoryPaths.ManifestPath(goldenRoot, input.Value);
            string copiedPath = workspace.PathFor($"{input.Name}.bin");
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
            File.ReadAllBytes(RepositoryPaths.ManifestPath(goldenRoot, goldenCase.GetProperty("expectedOutput"))),
            File.ReadAllBytes(outputPath));
    }

    /// <inheritdoc/>
    public static TheoryData<string, string> StandardMergeAliases()
    {
        TheoryData<string, string> cases = [];
        foreach (CanonicalGoldenAlias alias in CanonicalGoldenTestData.LoadWorkflowAliases("standard-merge"))
        {
            cases.Add(alias.Ic[2..], alias.SourceIc[2..]);
        }

        return cases;
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
            string originalPath = RepositoryPaths.ManifestPath(goldenRoot, input.Value);
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
            File.ReadAllBytes(RepositoryPaths.ManifestPath(goldenRoot, goldenCase.GetProperty("expectedOutput"))),
            File.ReadAllBytes(outputPath));
    }
}
