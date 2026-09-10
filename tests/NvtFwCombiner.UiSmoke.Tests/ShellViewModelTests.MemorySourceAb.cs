using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>The canonical AB map has command/code regions, not invented customer-information roles.</summary>
    [Fact]
    public async Task AbMemorySourceDoesNotInventCustomerInformationFromCmiNames()
    {
        using var workspace = TempWorkspace.Create("nfc-memory-ab-source");
        MainWindowViewModel vm = PrepareAbSameTpViewModel();
        string dpPath = workspace.Write("dp.bin", new byte[0x80000]);
        await vm.WorkflowSession.SetSlotFileAsync(CompositionAddressSpaceIds.DpAbInput, dpPath, TestContext.Current.CancellationToken);
        Assert.Contains(vm.Merge.MergeCoverageSegments, segment => segment.SourceLabel == "DP AB");
        Assert.DoesNotContain(vm.Merge.MergeCoverageSegments, segment => segment.ContentRole == MemoryContentRole.CustomerInformation);
        Assert.All(vm.Merge.MergeCoverageSegments, segment =>
        {
            Assert.Equal(segment.SourceLabel, segment.DisplayTitle);
            Assert.False(segment.HasSourceCaption);
        });
    }
}
