using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Real accepted proposals rendered in all approved delivery states.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class OutputConfirmationTests
{
    /// <summary>Shared modal admission rejects pending preparation after a newer Open or Cancel.</summary>
    [Fact]
    public async Task OutputConfirmationPreparationGenerationRejectsSupersededOpen()
    {
        using TempWorkspace workspace = TempWorkspace.Create("confirmation-generation");
        CompositionHostServices host = CompositionHostServices.Create();
        CompositionOutputBundleProposal proposal = await PrepareLegacyProposalAsync(host, workspace);
        var vm = new OutputDeliveryConfirmationViewModel(host.CompositionOutputNaming, () => ShellTextResources.For(ShellLanguage.English));
        long pending = vm.BeginPreparation();
        Assert.True(vm.IsPreparationCurrent(pending));
        vm.Open(new OutputDeliveryRequest(proposal, false, null, () => true, null, null, null, _ => Task.CompletedTask));
        Assert.False(vm.IsPreparationCurrent(pending));
        long second = vm.BeginPreparation();
        vm.CancelCommand.Execute(null);
        Assert.False(vm.IsPreparationCurrent(second));
    }

    /// <summary>Real confirmation cannot execute an old request after asynchronous freshness yields.</summary>
    [Theory]
    [InlineData("reopen")]
    [InlineData("cancel")]
    [InlineData("session")]
    [InlineData("config")]
    public async Task OutputConfirmationFreshnessRechecksOwnership(string change)
    {
        using TempWorkspace workspace = TempWorkspace.Create("confirmation-race");
        CompositionHostServices host = CompositionHostServices.Create();
        CompositionOutputBundleProposal proposal = await PrepareLegacyProposalAsync(host, workspace);
        var naming = new DelayedConfirmationNaming(host.CompositionOutputNaming);
        var vm = new OutputDeliveryConfirmationViewModel(naming, () => ShellTextResources.For(ShellLanguage.English));
        bool current = true;
        int executions = 0;
        var request = new OutputDeliveryRequest(proposal, false, null, () => current, null, null, null,
            _ => { executions++; return Task.CompletedTask; });
        vm.Open(request);
        Task confirmation = vm.ConfirmLooseAsync(workspace.PathFor("output.bin"), null, true, false, prepareModeSpecific: false);
        await naming.Entered.Task;
        if (change == "reopen") { vm.Open(request with { IsReplaceOutput = true }); }
        if (change == "cancel") { vm.CancelCommand.Execute(null); }
        if (change == "session") { current = false; }
        naming.Completed.SetResult(change != "config");
        await confirmation;
        Assert.Equal(0, executions);
        if (change is "session" or "config")
        {
            Assert.False(vm.CanConfirm);
            Assert.True(vm.HasValidationMessage);
            vm.ApplyLanguageChanged();
            vm.SetBundleEnabled(true);
            Assert.True(vm.HasValidationMessage);
            vm.SetBundleEnabled(false);
            Assert.True(vm.HasValidationMessage);
            Assert.False(vm.CanConfirm);
        }
        if (change == "reopen") { Assert.True(vm.IsOpen); Assert.True(vm.IsReplaceOutput); }
    }

    private static async Task<CompositionOutputBundleProposal> PrepareLegacyProposalAsync(CompositionHostServices host, TempWorkspace workspace)
    {
        CompiledAuthoringSessionPreparation prepared = host.AbMergeAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51932", null,
            [new("tp-a-input", workspace.PathFor("a.bin"), new byte[0x40000]), new("tp-b-input", workspace.PathFor("b.bin"), new byte[0x40000])], AbMergeDpMode.Dummy);
        Assert.True(prepared.Succeeded);
        return await host.CompositionOutputNaming.PrepareBundleProposalAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
    }

    private sealed class DelayedConfirmationNaming(ICompositionOutputNaming inner) : ICompositionOutputNaming
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<bool> IsProposalCurrentAsync(CompositionOutputBundleProposal proposal, CancellationToken cancellationToken)
        {
            Entered.SetResult();
            return await Completed.Task.WaitAsync(cancellationToken);
        }
        public ValueTask<CompositionOutputBundleProposal> PrepareBundleProposalAsync(ActiveSessionSnapshot session, CancellationToken cancellationToken, CtrlRamFirmwareVersionDraftState? edit = null)
        {
            return inner.PrepareBundleProposalAsync(session, cancellationToken, edit);
        }
        public CompositionOutputPreparation ResolveAcceptedOutput(ActiveSessionSnapshot session, CtrlRamFirmwareVersionDraftState? edit = null)
        {
            return inner.ResolveAcceptedOutput(session, edit);
        }
        public CompositionOutputBundleProposal ResolveAcceptedBundleProposal(ActiveSessionSnapshot session, CtrlRamFirmwareVersionDraftState? edit = null)
        {
            return inner.ResolveAcceptedBundleProposal(session, edit);
        }
        public CompositionOutputBundleDestinationValidation ValidateBundleDestination(CompositionOutputBundleIntent intent)
        {
            return inner.ValidateBundleDestination(intent);
        }
        public ValueTask<CompositionOutputPreparation> PrepareAutomaticOutputAsync(ActiveSessionSnapshot session, CancellationToken cancellationToken)
        {
            return inner.PrepareAutomaticOutputAsync(session, cancellationToken);
        }
    }

    /// <summary>Delivery options cannot change primary size or automatic names; the aligned summary fits.</summary>
    [AvaloniaTheory]
    [InlineData(false, false, false, 1, 1)]
    [InlineData(true, false, false, 1, 1)]
    [InlineData(false, true, false, 1, 1)]
    [InlineData(true, true, false, 1, 1)]
    [InlineData(false, false, true, 1, 1)]
    [InlineData(true, false, true, 1, 1)]
    [InlineData(false, true, true, 1, 1)]
    [InlineData(true, true, true, 1, 1)]
    [InlineData(false, false, false, 2, 3)]
    [InlineData(true, false, true, 1, 1, true)]
    public async Task ApprovedOutputConfirmationStates(bool bundle, bool additional, bool chineseDark, byte countA, byte countB, bool tallerCjkMetrics = false)
    {
        using TempWorkspace workspace = TempWorkspace.Create("output-confirmation-reference");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(), loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        string ic = additional ? "NT51932" : "NT51950";
        byte[] tp = new byte[additional ? 0x40000 : 0x37000];
        tp[0x22200] = 0x31;
        tp[0x22201] = 0xCE;
        tp[0x2220C] = 0x97;
        tp[0x36000] = 0x42;
        tp[0x36001] = 0xBD;
        tp[0x36017] = countA;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(tp, 0x36FFC);
        byte[] tpB = (byte[])tp.Clone();
        tpB[0x36017] = countB;
        tpB[0x2220C] = 0xA6;
        var inputs = new List<CompiledAuthoringSelectedInput>
        {
            new("tp-a-input", workspace.PathFor("input-tpa.bin"), tp),
            new("tp-b-input", workspace.PathFor("input-tpb.bin"), tpB),
        };
        if (!additional) { inputs.Insert(0, new("dp-ab-input", workspace.PathFor("input-dp-ab.bin"), new byte[countA != countB ? 0x100010 : 0x100000])); }
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(
            new AuthoringSessionState(ExperienceIds.AbMerge), ic, additional ? null : countA == 1 ? "single" : "cascade",
            inputs, additional ? AbMergeDpMode.Dummy : AbMergeDpMode.Normal, TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues.Select(issue => issue.Code)));
        CompositionOutputBundleProposal proposal = await host.CompositionOutputNaming.PrepareBundleProposalAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
        ShellTextResources text = ShellTextResources.For(chineseDark ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        var vm = new OutputDeliveryConfirmationViewModel(host.CompositionOutputNaming, () => text);
        vm.Open(new OutputDeliveryRequest(proposal, false,
            proposal.OutputPreparation.AdditionalDeliveries.SingleOrDefault(), () => true, null, null, null, _ => Task.CompletedTask));
        string originalSize = vm.FlashOutputSize;
        vm.SetBundleEnabled(bundle);
        vm.SetAdditionalDeliveryEnabled(additional);
        vm.SetSourcesExpanded(bundle && !additional);
        vm.SetParentDirectory(workspace.Root);
        Assert.Equal(proposal.OutputPreparation.OutputName.FileName, vm.OutputFileName);
        Assert.Equal(originalSize, vm.FlashOutputSize);
        Assert.Equal(additional ? "512 KiB (524,288 bytes)" : "1 MiB (1,048,576 bytes)", vm.FlashOutputSize);
        Assert.Equal(additional ? "AB Code" : "AB Code / Desay", vm.ModeFormatSummary);
        Assert.Equal(additional ? 2 : 3, vm.InputRows.Count);
        if (countA != countB)
        {
            Assert.Contains("Cascade", vm.TargetSummary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("2 IC", vm.TargetSummary, StringComparison.Ordinal);
            Assert.True(vm.HasInputWarnings);
            Assert.Contains(vm.InputRows, row => row.Role == "DP AB Code" && row.HasWarning);
        }
        Assert.Equal(additional, vm.HasGeneratedInputs);
        Assert.All(vm.InputRows, row => Assert.NotEmpty(row.Sha256));
        if (additional)
        {
            Assert.Equal("256 KiB (262,144 bytes)", vm.AdditionalOutputSize);
            Assert.False(vm.HasEventBufferChecks);
        }
        else
        {
            Assert.Equal([new OutputConfirmationCheck("TP A", "0x97 - Desay"), new OutputConfirmationCheck("TP B", "0xA6 - Desay")], vm.EventBufferChecks);
            Assert.Equal("1,048,576 bytes", Assert.Single(vm.ExpectedInputChecks).Value);
            Assert.Equal(chineseDark ? "預期 DP 大小" : "Expected DP size", Assert.Single(vm.ExpectedInputChecks).Label);
            Assert.Equal(["DP AB Code", "TP A", "TP B"], vm.InputRows.Select(row => row.Role));
        }

        var modal = new OutputDeliveryConfirmationModal { DataContext = vm, IsOpen = true };
        var window = new Window
        {
            Width = 980,
            Height = 820,
            Content = modal,
            RequestedThemeVariant = chineseDark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        foreach (string path in new[] { "Styles/MainWindowStyles.axaml", "Styles/MainWindowButtonStyles.axaml", "Styles/MainWindowVisualStyles.axaml" })
        {
            var uri = new Uri($"avares://NvtFwCombiner.Presentation.Avalonia/{path}");
            window.Styles.Add(new StyleInclude(uri) { Source = uri });
        }
        try
        {
            window.Show();
            if (tallerCjkMetrics)
            {
                // Replay the CI fallback-font metric delta without depending on an installed CJK font.
                foreach (TextBlock block in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(modal).OfType<TextBlock>()
                    .Where(block => block.IsEffectivelyVisible && block.FontSize != 12 &&
                        block.Text?.Any(character => character is >= '\u4E00' and <= '\u9FFF') == true))
                {
                    block.LineHeight = block.TextLayout.Height + 1;
                }
            }
            window.Measure(new Size(980, 820));
            window.Arrange(new Rect(0, 0, 980, 820));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            TextBlock target = modal.FindControl<TextBlock>("OutputTargetValue")!;
            TextBlock mode = modal.FindControl<TextBlock>("OutputModeValue")!;
            TextBlock size = modal.FindControl<TextBlock>("FlashOutputSizeValue")!;
            Assert.Equal(target.Bounds.X, mode.Bounds.X);
            Assert.Equal(target.Bounds.X, size.Bounds.X);
            Assert.Equal(vm.FlashOutputSize, size.Text);
            Border surface = modal.FindControl<Border>("BuildSettingsSurface")!;
            Assert.InRange(surface.Bounds.Width, 759, 761);
            Assert.True(surface.Bounds.Height <= 720);
            ScrollViewer viewport = modal.FindControl<ScrollViewer>("BuildSettingsViewport")!;
            if (bundle && !additional)
            {
                Control files = Assert.IsType<ItemsControl>(modal.FindControl<ItemsControl>("SourceFilesList"));
                Control checks = Assert.IsType<Border>(modal.FindControl<Border>("SourceChecksPanel"));
                Assert.True(checks.IsVisible);
                Assert.True(checks.TranslatePoint(default, modal)!.Value.Y >=
                    files.TranslatePoint(default, modal)!.Value.Y + files.Bounds.Height);
            }
            Assert.True(viewport.Extent.Height <= viewport.Viewport.Height + 1,
                $"The approved four states must show the complete destination panel: extent={viewport.Extent.Height}, viewport={viewport.Viewport.Height}." +
                Environment.NewLine + string.Join(Environment.NewLine,
                    Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(modal).OfType<TextBlock>()
                        .Where(block => block.IsEffectivelyVisible)
                        .Select(block => $"text={block.Text}; bounds={block.Bounds}; desired={block.DesiredSize}; font={block.FontFamily}; size={block.FontSize}; lines={block.TextLayout.TextLines.Count}")));
            Assert.True(modal.FindControl<Button>("ConfirmButton")!.IsEffectivelyVisible);
            string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                _ = Directory.CreateDirectory(outputDirectory);
                using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(Path.Combine(outputDirectory, $"output-confirmation-{(bundle ? "bundle" : "loose")}-{(additional ? "extra" : "single")}-{(chineseDark ? "dark-zh" : "light-en")}{(countA != countB ? "-cascade" : "")}.png"));
            }
        }
        finally { window.Close(); }
    }
}
