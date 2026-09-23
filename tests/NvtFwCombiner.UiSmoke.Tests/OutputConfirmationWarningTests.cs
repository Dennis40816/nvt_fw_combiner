using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Accepted input warnings remain visible outside the collapsed source disclosure.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class OutputConfirmationWarningTests
{
    /// <summary>The production modal keeps typed warnings between summary and delivery without blocking Build.</summary>
    [AvaloniaTheory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task WarningsRemainVisibleWhenSourcesAreCollapsed(bool chineseDark, bool hasWarnings)
    {
        using TempWorkspace workspace = TempWorkspace.Create("confirmation-warnings");
        CompositionHostServices host = CompositionHostServices.Create();
        CompositionOutputBundleProposal proposal = await PrepareAsync(host, workspace, hasWarnings);
        var vm = new OutputDeliveryConfirmationViewModel(host.CompositionOutputNaming,
            () => ShellTextResources.For(chineseDark ? ShellLanguage.ChineseTraditional : ShellLanguage.English));
        Open(vm, proposal);
        var modal = new OutputDeliveryConfirmationModal { DataContext = vm, IsOpen = true };
        var window = new Window
        {
            Width = 980,
            Height = 820,
            Content = modal,
            RequestedThemeVariant = chineseDark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        foreach (string style in new[] { "MainWindowStyles", "MainWindowButtonStyles", "MainWindowVisualStyles" })
        {
            var uri = new Uri($"avares://NvtFwCombiner.Presentation.Avalonia/Styles/{style}.axaml");
            window.Styles.Add(new StyleInclude(uri) { Source = uri });
        }

        try
        {
            window.Show();
            Render(window);
            Assert.False(vm.AreSourcesExpanded);
            Assert.Equal(hasWarnings, vm.HasInputWarnings);
            Assert.True(vm.CanConfirm);
            Border warnings = Assert.IsType<Border>(modal.FindControl<Border>("BuildWarningsPanel"));
            Assert.Equal(hasWarnings, warnings.IsEffectivelyVisible);
            TextBlock footer = Assert.IsType<TextBlock>(modal.FindControl<TextBlock>("BuildReadinessSummary"));
            if (hasWarnings)
            {
                Grid summary = modal.FindControl<Grid>("OutputConfirmationSummary")!;
                Border delivery = modal.FindControl<Border>("DeliverySettingSection")!;
                double top = warnings.TranslatePoint(default, modal)!.Value.Y;
                Assert.True(top >= summary.TranslatePoint(default, modal)!.Value.Y + summary.Bounds.Height);
                Assert.True(top + warnings.Bounds.Height <= delivery.TranslatePoint(default, modal)!.Value.Y);
                TextBlock[] warningText = [.. warnings.GetVisualDescendants().OfType<TextBlock>()
                    .Where(block => block.Text == vm.InputRows[0].Warning)];
                Assert.Equal(2, warningText.Length);
                Assert.All(warningText, block => Assert.True(block.IsEffectivelyVisible));
                Assert.Equal(chineseDark ? "2 則警告 · 可繼續 Build" : "2 warnings · Can continue Build", footer.Text);
            }
            else
            {
                Assert.Equal(vm.Text.OutputDeliveryReadySummary, footer.Text);
            }

            string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
                using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(Path.Combine(directory, $"output-warnings-{hasWarnings}-{(chineseDark ? "dark-zh" : "light-en")}.png"));
            }

            vm.CancelCommand.Execute(null);
            Open(vm, await PrepareAsync(host, workspace, false));
            Render(window);
            Assert.False(warnings.IsEffectivelyVisible);
            Assert.Equal(vm.Text.OutputDeliveryReadySummary, footer.Text);
            Assert.True(vm.CanConfirm);
        }
        finally { window.Close(); }
    }

    private static void Open(OutputDeliveryConfirmationViewModel vm, CompositionOutputBundleProposal proposal)
    {
        vm.Open(new OutputDeliveryRequest(proposal, false, null, () => true, null, null, null, _ => Task.CompletedTask));
    }

    private static async Task<CompositionOutputBundleProposal> PrepareAsync(
        CompositionHostServices host, TempWorkspace workspace, bool hasWarnings)
    {
        // Synthetic canonical TP prefix with valid version complement, IC Count and marker.
        byte[] bytes = new byte[0x40000];
        bytes[0x36001] = 0xFF;
        bytes[0x36017] = 1;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(bytes, 0x36FFC);
        string path = workspace.PathFor("shared.bin");
        CompiledAuthoringSessionPreparation prepared = host.AbMergeAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51932", null,
            [new("tp-a-input", path, bytes), new("tp-b-input", path, bytes)], AbMergeDpMode.Dummy);
        Assert.True(prepared.Succeeded);
        CompositionOutputBundleProposal proposal = await host.CompositionOutputNaming.PrepareBundleProposalAsync(
            prepared.Snapshot!, TestContext.Current.CancellationToken);
        if (!hasWarnings) { return proposal; }
        // Inject accepted typed warnings at the UI boundary, not a firmware-support/inspection fixture.
        // Current profiles accept extra TP bytes silently; invalid TP version bars block IC Count admission.
        return new CompositionOutputBundleProposal(proposal.FolderName, proposal.OutputPreparation,
            proposal.ResolvedAtUtc, proposal.Admission)
        {
            Confirmation = proposal.Confirmation! with
            {
                Inputs = [.. proposal.Confirmation.Inputs.Select(input => input with
                {
                    InspectionLifecycle = AuthoringSlotLifecycle.Warning,
                    InspectionIssueCode = InputArtifactInspectionIssueCodes.AbVersionMetadataUnknown,
                    InspectionAdvisories = [new(InputArtifactInspectionIssueCodes.AbVersionMetadataUnknown,
                        CompiledInputArtifactInspectionNextAction.ReviewUnknownVersion)],
                })],
            },
        };
    }

    private static void Render(Window window)
    {
        window.Measure(new Size(980, 820));
        window.Arrange(new Rect(0, 0, 980, 820));
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
