using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Loads the owner-approved Standard Merge golden manifest for UI smoke tests.</summary>
internal sealed class StandardMergeGoldenManifest : IDisposable
{
    private const string ManifestFileName = "manifest.json";
    private readonly JsonDocument _document;

    private StandardMergeGoldenManifest(string root, JsonDocument document)
    {
        Root = root;
        _document = document;
    }

    public string Root { get; }

    public static StandardMergeGoldenManifest Load()
    {
        string root = RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "standard-merge-gen-flash");
        string manifestPath = Path.Combine(root, ManifestFileName);
        return new StandardMergeGoldenManifest(root, JsonDocument.Parse(File.ReadAllText(manifestPath)));
    }

    public TheoryData<string> CaseIds()
    {
        TheoryData<string> cases = [];
        foreach (JsonElement goldenCase in _document.RootElement.GetProperty("cases").EnumerateArray())
        {
            cases.Add(goldenCase.GetProperty("ic").GetString()!);
        }

        return cases;
    }

    public JsonElement CaseByIc(string ic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ic);

        return _document.RootElement
            .GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("ic").GetString() == ic);
    }

    public string PathFromRelative(string relativePath)
    {
        return RepositoryPaths.PathFromRelative(Root, relativePath);
    }

    public string ManifestPath(JsonElement manifestFile)
    {
        return RepositoryPaths.ManifestPath(Root, manifestFile);
    }

    public string ExpectedOutputPath(JsonElement goldenCase)
    {
        return ManifestPath(goldenCase.GetProperty("expectedOutput"));
    }

    public byte[] ReadExpectedOutput(JsonElement goldenCase)
    {
        return File.ReadAllBytes(ExpectedOutputPath(goldenCase));
    }

    public void CopyInputFilesToMergeSlots(
        MainWindowViewModel viewModel,
        TempWorkspace workspace,
        JsonElement goldenCase)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(workspace);

        foreach (JsonProperty input in goldenCase.GetProperty("inputs").EnumerateObject())
        {
            string sourcePath = ManifestPath(input.Value);
            string copiedPath = workspace.PathFor($"{input.Name}.bin");
            File.Copy(sourcePath, copiedPath);
            viewModel.SetSlotFile(SlotIdForAddressSpace(input.Name), copiedPath);
        }
    }

    public void Dispose()
    {
        _document.Dispose();
    }

    public static string SlotIdForAddressSpace(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            "dp-input" => "merge-dp",
            "tp-input" => "merge-tp",
            "ld-input" => "merge-ld",
            _ => throw new InvalidOperationException($"Unknown address space '{addressSpaceId}'."),
        };
    }
}
