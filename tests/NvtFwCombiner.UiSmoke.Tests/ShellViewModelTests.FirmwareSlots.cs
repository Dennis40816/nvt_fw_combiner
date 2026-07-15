using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies slot completion retains required and optional semantics for XAML state selectors.</summary>
    [Fact]
    public void FirmwareSlotCompletionToneHighlightsOnlyRequiredInputs()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-slot-tone");
        FirmwareSlotViewModel required = new("merge-dp", "DP BIN", "Display payload");

        Assert.False(required.IsOptional);
        Assert.False(required.HasFile);
        Assert.Equal(FirmwareSlotKind.Dp, required.SlotKind);
        Assert.Equal("DP BIN", required.SlotIconTooltip);
        AssertIconGeometry(required);
        Assert.Equal("No BIN selected", required.DisplayName);
        Assert.Equal(string.Empty, required.DisplayDetail);
        Assert.Equal("Required", required.RequirementLabel);

        required.FilePath = workspace.PathFor("dp.bin").Replace('\\', '/');

        Assert.True(required.HasFile);
        Assert.Equal("dp.bin", required.DisplayName);
        Assert.Equal(required.FilePath.Replace('/', '\\'), required.DisplayDetail);
        AssertIconGeometry(required);
        Assert.Equal("Required", required.RequirementLabel);

        FirmwareSlotViewModel optional = new("merge-ld", "LD BIN", "Optional payload", isOptional: true);

        Assert.True(optional.IsOptional);
        Assert.Equal(FirmwareSlotKind.Dp, optional.SlotKind);
        AssertIconGeometry(optional);
        Assert.Equal("Optional", optional.RequirementLabel);

        optional.FilePath = workspace.PathFor("ld.bin");

        Assert.True(optional.HasFile);
        Assert.Equal("Optional", optional.RequirementLabel);
    }

    /// <summary>Verifies slot type icons distinguish DP, TP, CtrlRAM and base BIN inputs.</summary>
    [Fact]
    public void FirmwareSlotTypeIconsExposeInputCategories()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Assert.Contains(viewModel.MergeSlots, slot =>
            slot.Title == "DP BIN" &&
            slot.SlotKind == FirmwareSlotKind.Dp &&
            HasDrawableIcon(slot));
        Assert.Contains(viewModel.MergeSlots, slot =>
            slot.Title == "TP BIN" &&
            slot.SlotKind == FirmwareSlotKind.Tp &&
            HasDrawableIcon(slot));
        Assert.Equal(FirmwareSlotKind.Base, viewModel.ReplaceBaseSlot.SlotKind);
        AssertIconGeometry(viewModel.ReplaceBaseSlot);
        Assert.Equal("Base firmware BIN", viewModel.ReplaceBaseSlot.SlotIconTooltip);

        viewModel.ShowDpReplaceCommand.Execute(null);

        Assert.Contains(viewModel.ReplaceSlots, slot =>
            slot.SlotId == "replace-dp" &&
            slot.SlotKind == FirmwareSlotKind.Dp &&
            HasDrawableIcon(slot));

        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        Assert.All(
            viewModel.ReplaceSlots.Where(slot => !ReferenceEquals(slot, viewModel.ReplaceBaseSlot)),
            slot =>
            {
                Assert.Equal(FirmwareSlotKind.CtrlRam, slot.SlotKind);
                Assert.Equal("CtrlRAM BIN", slot.SlotIconTooltip);
                AssertIconGeometry(slot);
            });
    }

    /// <summary>Verifies base BIN slots expose FWConfig facts decoded from the selected flash image.</summary>
    [Fact]
    public void BaseFirmwareSlotShowsFwConfigFacts()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        using var golden = StandardMergeGoldenManifest.Load();
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));

        viewModel.SetSlotFile("replace-base", basePath);

        Assert.True(viewModel.ReplaceBaseSlot.HasFirmwareFacts);
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "Common FW" && fact.Value == "1.4.1");
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "TP" &&
            fact.Value == "T01-00");
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "PID" && fact.Value == "0x5102");
        Assert.DoesNotContain(viewModel.ReplaceBaseSlot.FirmwareFacts, fact => fact.Label == "Refresh");
    }

    /// <summary>Verifies DP BIN slots expose gen_flash DP version facts and mark missing evidence.</summary>
    [Fact]
    public void DpFirmwareSlotShowsGenFlashVersionOrTodo()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement nt51926 = golden.CaseByIc("51926");
        string dpPath = golden.ManifestPath(nt51926.GetProperty("inputs").GetProperty("dp-input"));
        string tpPath = golden.ManifestPath(nt51926.GetProperty("inputs").GetProperty("tp-input"));

        viewModel.SetSlotFile("merge-dp", dpPath);
        viewModel.SetSlotFile("merge-tp", tpPath);

        FirmwareSlotViewModel dpSlot = viewModel.MergeSlots.Single(slot => slot.SlotId == "merge-dp");
        Assert.Contains(dpSlot.FirmwareFacts, fact =>
            fact.Label == "DP" &&
            fact.Value == "D01-02" &&
            !fact.IsWarning);
        Assert.Contains(dpSlot.FirmwareFacts, fact =>
            fact.Label == "Jira" &&
            fact.Value == "AUTO_PRJ-597" &&
            !fact.IsWarning);
        Assert.StartsWith(
            "NT51926_FlashCode_D0102T0100_",
            viewModel.MergeOutputFileName,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "NT51926_FlashCode_DxxxxTxxxx_",
            viewModel.ReplaceOutputFileName,
            StringComparison.Ordinal);

        viewModel.SelectedIc = "NT51950";
        JsonElement nt51950 = golden.CaseByIc("51950");
        string nt51950DpPath = golden.ManifestPath(nt51950.GetProperty("inputs").GetProperty("dp-input"));
        string nt51950TpPath = golden.ManifestPath(nt51950.GetProperty("inputs").GetProperty("tp-input"));
        viewModel.SetSlotFile("merge-dp", nt51950DpPath);
        viewModel.SetSlotFile("merge-tp", nt51950TpPath);

        dpSlot = viewModel.MergeSlots.Single(slot => slot.SlotId == "merge-dp");
        Assert.Contains(dpSlot.FirmwareFacts, fact =>
            fact.Label == "DP" &&
            fact.Value == "DCC-00" &&
            !fact.IsWarning);
        Assert.Contains(dpSlot.FirmwareFacts, fact =>
            fact.Label == "Jira" &&
            fact.Value == "AUTO_PRJ-576" &&
            !fact.IsWarning);
    }

    /// <summary>Verifies an unobserved DP size keeps the concise DP/Jira slot badge set.</summary>
    [Fact]
    public void DpFirmwareSlotKeepsCmiSizeDiagnosticsOutOfBadges()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement nt51926 = golden.CaseByIc("51926");
        string sourcePath = golden.ManifestPath(nt51926.GetProperty("inputs").GetProperty("dp-input"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-cmi-dp-size");
        byte[] oversizedDp = [.. File.ReadAllBytes(sourcePath), 0x00];
        string oversizedPath = workspace.Write("nt51926-unexpected-size.bin", oversizedDp);

        viewModel.SetSlotFile("merge-dp", oversizedPath);

        FirmwareSlotViewModel dpSlot = viewModel.MergeSlots.Single(slot => slot.SlotId == "merge-dp");
        Assert.Contains(dpSlot.FirmwareFacts, fact =>
            fact.Label == "Jira" &&
            fact.Value == "AUTO_PRJ-597" &&
            !fact.IsWarning);
        Assert.DoesNotContain(dpSlot.FirmwareFacts, fact => fact.Label == "DP size");
    }

    /// <summary>Verifies profile size diagnostics do not create an additional DP card badge.</summary>
    [Fact]
    public void DpFirmwareSlotKeepsProfileSizeDiagnosticsOutOfBadges()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51920";
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement nt51920 = golden.CaseByIc("51920");
        string sourcePath = golden.ManifestPath(nt51920.GetProperty("inputs").GetProperty("dp-input"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-profile-dp-size");
        byte[] oversizedDp = [.. File.ReadAllBytes(sourcePath), 0x00];
        string oversizedPath = workspace.Write("nt51920-unexpected-size.bin", oversizedDp);

        viewModel.SetSlotFile("merge-dp", oversizedPath);

        FirmwareSlotViewModel dpSlot = viewModel.MergeSlots.Single(slot => slot.SlotId == "merge-dp");
        Assert.Contains(dpSlot.FirmwareFacts, fact =>
            fact.Label == "DP" &&
            fact.Value == "D01-01" &&
            !fact.IsWarning);
        Assert.DoesNotContain(dpSlot.FirmwareFacts, fact => fact.Label == "DP size");
    }
}
