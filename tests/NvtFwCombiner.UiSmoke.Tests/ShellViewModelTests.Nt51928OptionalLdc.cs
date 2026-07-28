using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>NT51928 exposes LDC as optional and becomes build-ready from DP and TP alone.</summary>
    [Fact]
    public async Task MergeSlotsExposeOptionalNt51928LdcWithoutBlockingReadiness()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-51928-optional-ldc");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51926";

        Assert.Equal(["DP BIN", "TP BIN"], viewModel.MergeSlots.Select(slot => slot.Title));
        Assert.DoesNotContain(viewModel.MergeSlots, slot => slot.Title.Contains("LD", StringComparison.Ordinal));

        viewModel.SelectedIc = "NT51928";

        Assert.Equal(["DP BIN", "TP BIN", "LD BIN"], viewModel.MergeSlots.Select(slot => slot.Title));
        FirmwareSlotViewModel ldcSlot = viewModel.MergeSlots.Single(
            static slot => slot.SlotId == WorkbenchSlotIds.MergeLd);
        Assert.True(ldcSlot.IsOptional);
        Assert.False(viewModel.CanBuildMerge);

        await viewModel.SetSlotFileAsync(
            WorkbenchSlotIds.MergeDp,
            workspace.Write("dp.bin", new byte[0x40000]),
            TestContext.Current.CancellationToken);
        await viewModel.SetSlotFileAsync(
            WorkbenchSlotIds.MergeTp,
            workspace.Write("tp.bin", new byte[0x35000]),
            TestContext.Current.CancellationToken);

        Assert.False(ldcSlot.HasFile);
        Assert.True(viewModel.CanBuildMerge);
        Assert.Equal("0x00000-0x3FFFF (len 0x40000)", viewModel.MergeMemoryRangeLabel);

        await viewModel.SetSlotFileAsync(
            WorkbenchSlotIds.MergeLd,
            workspace.Write("ld.bin", new byte[0x80000]),
            TestContext.Current.CancellationToken);

        Assert.True(ldcSlot.HasFile);
        Assert.True(viewModel.CanBuildMerge);
        Assert.Equal("0x00000-0x7FFFF (len 0x80000)", viewModel.MergeMemoryRangeLabel);
    }
}
