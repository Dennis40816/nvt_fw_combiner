using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>NT51928 requires Reference plus either optional replacement and projects the selected coverage.</summary>
    [Fact]
    public void Nt51928DpReplaceRequiresAtLeastOneOptionalReplacement()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-51928-dp-optional");
        string referencePath = workspace.Write("reference.bin", new byte[0x80000]);
        string initialCodePath = workspace.Write("initial-code.bin", new byte[0x80000]);
        string ldcPath = workspace.Write("ldc.bin", new byte[0x80000]);

        MainWindowViewModel baseOnly = CreateNt51928DpReplace();
        baseOnly.SetSlotFile("replace-base", referencePath);

        Assert.False(baseOnly.CanBuildReplace);
        Assert.Contains(
            baseOnly.ReplaceSelectionMissingRows,
            row => row.Title == "DP replacement" &&
                row.Meta.Contains("Initial Code or LDC", StringComparison.Ordinal));
        Assert.All(baseOnly.ReplaceCoverageSegments, static segment => Assert.False(segment.IsChanged));

        MainWindowViewModel initialCodeOnly = CreateNt51928DpReplace();
        initialCodeOnly.SetSlotFile("replace-base", referencePath);
        initialCodeOnly.SetSlotFile("replace-dp", initialCodePath);

        Assert.True(initialCodeOnly.CanBuildReplace);
        Assert.Contains(initialCodeOnly.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "Changed DP BIN" && segment.IsChanged);
        Assert.DoesNotContain(initialCodeOnly.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "Changed LDC BIN" && segment.IsChanged);

        MainWindowViewModel ldcOnly = CreateNt51928DpReplace();
        ldcOnly.SetSlotFile("replace-base", referencePath);
        ldcOnly.SetSlotFile("replace-ldc", ldcPath);

        Assert.True(ldcOnly.CanBuildReplace);
        Assert.Contains(ldcOnly.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "Changed LDC BIN" && segment.IsChanged);
        Assert.DoesNotContain(ldcOnly.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "Changed DP BIN" && segment.IsChanged);
    }

    private static MainWindowViewModel CreateNt51928DpReplace()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51928";
        OpenReplace(viewModel, "DP");
        return viewModel;
    }
}
