using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Loads the optional owner-approved CtrlRAM Replace fixture manifest for UI smoke tests.</summary>
internal sealed class CtrlRamReplaceFixtureManifest : IDisposable
{
    private const string ManifestFileName = "manifest.json";
    private readonly JsonDocument _document;

    private CtrlRamReplaceFixtureManifest(string root, JsonDocument document)
    {
        Root = root;
        _document = document;
    }

    public string Root { get; }

    public bool EnforceExpectedOutput =>
        _document.RootElement.TryGetProperty("runnerStatus", out JsonElement runnerStatus) &&
        runnerStatus.GetString() == "ready-for-private-golden";

    public IEnumerable<JsonElement> Cases => _document.RootElement.GetProperty("cases").EnumerateArray();

    public static CtrlRamReplaceFixtureManifest? LoadIfPresent()
    {
        string root = RepositoryPaths.FromRepositoryRoot("testdata", "golden", "ctrlram-replace");
        string manifestPath = Path.Combine(root, ManifestFileName);
        return File.Exists(manifestPath)
            ? new CtrlRamReplaceFixtureManifest(root, JsonDocument.Parse(File.ReadAllText(manifestPath)))
            : null;
    }

    public string PathFor(JsonElement manifestFile)
    {
        return RepositoryPaths.ManifestPath(Root, manifestFile);
    }

    public void SetBaseSlot(MainWindowViewModel viewModel, JsonElement fixtureCase)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        viewModel.SetSlotFile("replace-base", PathFor(fixtureCase.GetProperty("base")));
    }

    public void SetReplacementSlots(MainWindowViewModel viewModel, JsonElement fixtureCase)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        foreach (JsonElement replacement in fixtureCase.GetProperty("replacementInputs").EnumerateArray())
        {
            string slotId = replacement.GetProperty("slotId").GetString()!;
            Assert.Contains(viewModel.ReplaceSlots, slot => slot.SlotId == slotId);
            viewModel.SetSlotFile(slotId, PathFor(replacement.GetProperty("file")));
        }
    }

    public bool TryGetExpectedOutputPath(JsonElement fixtureCase, out string? expectedOutputPath)
    {
        if (!fixtureCase.TryGetProperty("expectedOutput", out JsonElement expectedOutput))
        {
            expectedOutputPath = null;
            return false;
        }

        expectedOutputPath = PathFor(expectedOutput);
        return true;
    }

    public void Dispose()
    {
        _document.Dispose();
    }
}
