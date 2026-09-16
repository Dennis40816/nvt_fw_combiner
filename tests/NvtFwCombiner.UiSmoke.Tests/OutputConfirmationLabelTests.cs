using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Confirmation labels distinguish input roles from delivered files and canonical selector tokens.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class OutputConfirmationLabelTests
{
    /// <summary>Using the same file in two roles shows two input rows but copies just one bundle source.</summary>
    [AvaloniaFact]
    public async Task SharedInputFileCountsRolesSeparatelyFromBundleCopies()
    {
        using TempWorkspace workspace = TempWorkspace.Create("confirmation-shared-source");
        CompositionHostServices host = CompositionHostServices.Create();
        byte[] input = new byte[0x40000];
        string path = workspace.PathFor("shared.bin");
        CompiledAuthoringSessionPreparation prepared = host.AbMergeAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51932", null,
            [new("tp-a-input", path, input), new("tp-b-input", path, input)], AbMergeDpMode.Dummy);
        Assert.True(prepared.Succeeded);
        CompositionOutputBundleProposal proposal = await host.CompositionOutputNaming.PrepareBundleProposalAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
        var vm = new OutputDeliveryConfirmationViewModel(host.CompositionOutputNaming, () => ShellTextResources.For(ShellLanguage.English));
        vm.Open(new OutputDeliveryRequest(proposal, false, null, () => true, null, null, null, _ => Task.CompletedTask));
        vm.SetBundleEnabled(true);
        var modal = new OutputDeliveryConfirmationModal { DataContext = vm, IsOpen = true };
        var window = new Window { Content = modal };
        try
        {
            window.Show();
            Assert.Equal(2, vm.InputRows.Count);
            _ = Assert.Single(vm.Sources);
            Assert.Equal("2 input sources", modal.FindControl<TextBlock>("SourcesCountLabel")!.Text);
            Assert.Equal("1 input sources", vm.SourcesSummary);
            Assert.Equal("Flash BIN + 1 source files", vm.DeliveryDescription);
        }
        finally { window.Close(); }
    }

    /// <summary>Display labels never expose internal cascade tokens and retain numeric units.</summary>
    [Theory]
    [InlineData("single", "Single", "單 IC")]
    [InlineData("2", "2 IC", "2 IC")]
    [InlineData("cascade", "Cascade", "串接")]
    [InlineData("cascade_2to8", "Cascade (2–8 IC)", "串接（2–8 IC）")]
    public void ConfirmationNumberUsesReadableLabels(string token, string english, string chinese)
    {
        IcNumberSelection selection = IcNumberSelection.FromToken(token);
        Assert.Equal(english, ShellTextResources.For(ShellLanguage.English).FormatOutputNumber(selection));
        Assert.Equal(chinese, ShellTextResources.For(ShellLanguage.ChineseTraditional).FormatOutputNumber(selection));
    }
}
