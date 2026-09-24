using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

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
        if (zero) { bad[0x1017] = 0; }
        else { bad[0x1FFC] = 0xFF; }
        await model.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.MergeDp, workspace.Write("dp.bin", new byte[0x6000]), TestContext.Current.CancellationToken);
        await model.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.MergeTp, workspace.Write("tp.bin", bad), TestContext.Current.CancellationToken);
        FirmwareSlotViewModel slot = model.Merge.MergeSlots.Single(static item => item.SlotId == CompositionSlotIds.MergeTp);
        Assert.True(slot.BlocksBuild);
        Assert.False(model.Merge.CanBuildMerge);
        Assert.Contains(zero ? "0" : "unreadable", slot.InputInspectionStatus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AB_TP_TOPOLOGY_MISMATCH", slot.InputInspectionStatus, StringComparison.Ordinal);
        model.SelectedLanguage = "Traditional Chinese";
        Assert.Contains(zero ? "讀到 0" : "讀不到", slot.InputInspectionStatus, StringComparison.Ordinal);
        await model.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.MergeTp, workspace.Write("fixed.bin", good), TestContext.Current.CancellationToken);
        Assert.False(slot.BlocksBuild);
        Assert.True(model.Merge.CanBuildMerge);
    }
}
