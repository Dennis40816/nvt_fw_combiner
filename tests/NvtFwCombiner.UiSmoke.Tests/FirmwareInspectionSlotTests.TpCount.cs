using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises typed TP count errors through Standard Merge input cards.</summary>
public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>The standard page shows the individual TP failure and recovers without AB peer admission.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StandardTpCountErrorIsExplicitAndRecoverable(bool zero)
    {
        using var workspace = TempWorkspace.Create("tp-count-ui");
        MainWindowViewModel model = await PresentationTestHost.CreateConfiguredFormatViewModelAsync(workspace);
        model.ShowMergeCommand.Execute(null);
        model.WorkflowSession.SelectedIc = "NT51929";
        model.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
        byte[] good = CreateUiAbTpImage(0x81, 0, 1, 4, 1, 0x5102);
        byte[] bad = [.. good];
        if (zero) { bad[UiTpBackupStart + 0x17] = 0; }
        else { bad[UiTpBackupStart + 0xFFC] = 0xFF; }
        await model.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.MergeDp, workspace.Write("dp.bin", new byte[0x6000]), TestContext.Current.CancellationToken);
        await model.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.MergeTp, workspace.Write("tp.bin", bad), TestContext.Current.CancellationToken);
        FirmwareSlotViewModel slot = model.Merge.MergeSlots.Single(static item => item.SlotId == CompositionSlotIds.MergeTp);
        Assert.True(slot.BlocksBuild);
        Assert.False(model.Merge.CanBuildMerge);
        Assert.Equal(zero ? "The TP firmware IC count is zero; select a BIN with a valid IC count." : "The TP firmware IC count could not be read; select a BIN with valid firmware configuration.", slot.InputInspectionStatus);
        Assert.DoesNotContain("AB_TP_TOPOLOGY_MISMATCH", slot.InputInspectionStatus, StringComparison.Ordinal);
        model.SelectedLanguage = "Traditional Chinese";
        Assert.Equal(zero ? "TP 韌體的 IC 數量為零，請選擇 IC 數量有效的 BIN。" : "無法讀取 TP 韌體的 IC 數量，請選擇韌體設定有效的 BIN。", slot.InputInspectionStatus);
        await model.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.MergeTp, workspace.Write("fixed.bin", good), TestContext.Current.CancellationToken);
        Assert.False(slot.BlocksBuild);
        Assert.True(model.Merge.CanBuildMerge);
    }
}
