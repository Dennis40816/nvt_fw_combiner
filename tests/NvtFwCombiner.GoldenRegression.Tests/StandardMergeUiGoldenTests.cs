using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.GoldenRegression.Tests;

/// <summary>Golden tests that exercise the UI shell command path for Standard Merge.</summary>
public sealed class StandardMergeUiGoldenTests
{
    /// <summary>Verifies the UI shell can build every gen_flash golden case byte-for-byte.</summary>
    [Fact]
    public async Task UiShellBuildStandardMergeMatchesGenFlashGoldenBytes()
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

    private static async ValueTask VerifyGoldenCaseAsync(
        string goldenRoot,
        string tempRoot,
        JsonElement goldenCase)
    {
        string ic = goldenCase.GetProperty("ic").GetString()!;
        string caseRoot = Path.Combine(tempRoot, ic);
        _ = Directory.CreateDirectory(caseRoot);

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = $"NT{ic}";
        viewModel.SelectedNumber = "single";

        foreach (JsonProperty input in goldenCase.GetProperty("inputs").EnumerateObject())
        {
            string originalPath = ManifestPath(goldenRoot, input.Value);
            string copiedPath = Path.Combine(caseRoot, $"{input.Name}.bin");
            File.Copy(originalPath, copiedPath);
            viewModel.SetSlotFile(SlotId(input.Name), copiedPath);
        }

        Assert.True(viewModel.BuildMergeCommand.CanExecute(null), $"{ic} UI Build command should be enabled.");
        await viewModel.BuildMergeCommand.ExecuteAsync(null).ConfigureAwait(false);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(File.Exists(viewModel.LastRunResult.Output), viewModel.LastRunResult.Output);
        Assert.Equal(
            File.ReadAllBytes(ManifestPath(goldenRoot, goldenCase.GetProperty("expectedOutput"))),
            File.ReadAllBytes(viewModel.LastRunResult.Output));
    }

    private static string SlotId(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            "dp-input" => "merge-dp",
            "tp-input" => "merge-tp",
            "ld-input" => "merge-ld",
            _ => throw new ArgumentOutOfRangeException(nameof(addressSpaceId), addressSpaceId, "Unknown UI merge slot."),
        };
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
