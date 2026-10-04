using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>Bound Standard labels reuse the exact required-space projection applied to slot optionality.</summary>
    [Fact]
    public async Task StandardMergeBoundLabelsDoNotRequeryRequiredSpacesAfterCatalogCommit()
    {
        var policy = new MutableAbCatalogPolicy();
        PresentationHostServices services = PresentationTestHost.CreateServicesWithCatalogPolicy(
            "0.10.6-standard-bound-projection-test",
            policy.Load);
        var authoring = new RecordingStandardMergeAuthoring(
            services.Composition.StandardMergeAuthoring);
        services = WithStandardMergeAuthoring(services, authoring);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(
            services,
            ShellLanguage.English);
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        CapabilitySelectorPublication original = services.Composition.Capabilities
            .GetSelectorPublication();
        string icId = original.IcIds.First(ic =>
            original.IsWorkflowAuthorable(ic, ExperienceIds.StandardMerge));
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = icId;
        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
        policy.DisableAbFor(original.AbMergeIcIds[0]);
        authoring.Reset(static () => false);

        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        string first = viewModel.Merge.MergeReadinessStatus;
        string second = viewModel.Merge.MergeReadinessStatus;

        Assert.Equal(first, second);
        _ = Assert.Single(authoring.Calls, call =>
            call.Method == nameof(IStandardMergeAuthoring.GetRequiredAddressSpaces));
        Assert.All(
            viewModel.Merge.MergeSlots.Where(static slot => !slot.IsOptional),
            slot => Assert.Contains(
                slot.AddressSpaceId switch
                {
                    CompositionAddressSpaceIds.DpInput => "DP",
                    CompositionAddressSpaceIds.TpInput => "TP",
                    CompositionAddressSpaceIds.LdcInput => "LDC",
                    _ => slot.AddressSpaceId!,
                },
                first,
                StringComparison.Ordinal));
    }
}
