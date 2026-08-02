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

    public JsonElement CaseById(string caseId)
    {
        return Cases.Single(candidate =>
            StringComparer.Ordinal.Equals(candidate.GetProperty("id").GetString(), caseId));
    }

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

        JsonElement baseArtifact = fixtureCase.TryGetProperty("artifacts", out JsonElement artifacts)
            ? artifacts.EnumerateArray().Single(item =>
                item.GetProperty("slotId").GetString() == "replace-base")
            : fixtureCase.GetProperty("base");
        viewModel.SetSlotFile("replace-base", PathForCaseArtifact(baseArtifact));
    }

    public void SetReplacementSlots(MainWindowViewModel viewModel, JsonElement fixtureCase)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        IEnumerable<JsonElement> replacements = fixtureCase.TryGetProperty("artifacts", out JsonElement artifacts)
            ? artifacts.EnumerateArray().Where(item =>
                item.GetProperty("slotId").GetString() != "replace-base")
            : fixtureCase.GetProperty("replacementInputs").EnumerateArray();
        foreach (JsonElement replacement in replacements)
        {
            string slotId = replacement.GetProperty("slotId").GetString()!;
            Assert.Contains(viewModel.Replace.ReplaceSlots, slot => slot.SlotId == slotId);
            viewModel.SetSlotFile(
                slotId,
                PathForCaseArtifact(replacement.TryGetProperty("file", out JsonElement file) ? file : replacement));
        }
    }

    public string ReplacementPathFor(JsonElement fixtureCase, string slotId)
    {
        IEnumerable<JsonElement> replacements = fixtureCase.TryGetProperty("artifacts", out JsonElement artifacts)
            ? artifacts.EnumerateArray()
            : fixtureCase.GetProperty("replacementInputs").EnumerateArray();
        JsonElement replacement = replacements.Single(candidate =>
            StringComparer.Ordinal.Equals(candidate.GetProperty("slotId").GetString(), slotId));
        return PathForCaseArtifact(
            replacement.TryGetProperty("file", out JsonElement file) ? file : replacement);
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

    private string PathForCaseArtifact(JsonElement artifact)
    {
        return artifact.TryGetProperty("legacyPaths", out _)
            ? CanonicalGoldenTestData.ArtifactPath(artifact)
            : PathFor(artifact);
    }
}
