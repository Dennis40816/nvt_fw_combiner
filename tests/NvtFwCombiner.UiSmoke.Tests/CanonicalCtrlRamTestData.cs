using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Applies mandatory canonical CtrlRAM evidence artifacts to UI test slots.</summary>
internal static class CanonicalCtrlRamTestData
{
    public static void SetBaseSlot(MainWindowViewModel viewModel, JsonElement goldenCase)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        JsonElement baseArtifact = goldenCase.GetProperty("artifacts")
            .EnumerateArray()
            .Single(item => item.GetProperty("slotId").GetString() == "replace-base");
        viewModel.SetSlotFile("replace-base", CanonicalGoldenTestData.ArtifactPath(baseArtifact));
    }

    public static void SetReplacementSlots(MainWindowViewModel viewModel, JsonElement goldenCase)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        foreach (JsonElement replacement in goldenCase.GetProperty("artifacts")
                     .EnumerateArray()
                     .Where(item => item.GetProperty("slotId").GetString() != "replace-base"))
        {
            string slotId = replacement.GetProperty("slotId").GetString()!;
            Assert.Contains(viewModel.Replace.ReplaceSlots, slot => slot.SlotId == slotId);
            viewModel.SetSlotFile(slotId, CanonicalGoldenTestData.ArtifactPath(replacement));
        }
    }

    public static string ReplacementPathFor(JsonElement goldenCase, string slotId)
    {
        JsonElement replacement = goldenCase.GetProperty("artifacts")
            .EnumerateArray()
            .Single(candidate => StringComparer.Ordinal.Equals(
                candidate.GetProperty("slotId").GetString(),
                slotId));
        return CanonicalGoldenTestData.ArtifactPath(replacement);
    }
}
