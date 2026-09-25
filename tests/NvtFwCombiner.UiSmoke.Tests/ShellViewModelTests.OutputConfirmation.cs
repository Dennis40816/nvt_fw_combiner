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

    /// <summary>A picker result cannot execute either a canceled request or its reopened successor.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputPickerRejectsCancelAndReopen(bool reopen)
    {
        using TempWorkspace workspace = TempWorkspace.Create("confirmation-picker-race");
        CompositionHostServices host = CompositionHostServices.Create();
        CompositionOutputBundleProposal proposal = await PrepareLegacyProposalAsync(host, workspace);
        var vm = new OutputDeliveryConfirmationViewModel(host.CompositionOutputNaming,
            () => ShellTextResources.For(ShellLanguage.English));
        int executions = 0;
        var request = new OutputDeliveryRequest(proposal, false, null, () => true, null, null, null,
            _ => { executions++; return Task.CompletedTask; });
        vm.Open(request);
        var picker = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task confirmation = OutputDeliveryConfirmationModal.ConfirmPreparedLooseWithPickersAsync(vm,
            () => picker.Task, () => throw new InvalidOperationException("No additional output was requested."));
        Assert.False(confirmation.IsCompleted);
        vm.CancelCommand.Execute(null);
        if (reopen) { vm.Open(request); }
        picker.SetResult(workspace.PathFor("obsolete.bin"));
        await confirmation;

        Assert.Equal(0, executions);
        Assert.Equal(reopen, vm.IsOpen);
        Assert.False(File.Exists(workspace.PathFor("obsolete.bin")));
    }

    /// <summary>A folder picker belongs to the confirmation that opened it.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParentDirectoryPickerRejectsCancelAndReopen(bool reopen)
    {
        using TempWorkspace workspace = TempWorkspace.Create("confirmation-directory-race");
        CompositionHostServices host = CompositionHostServices.Create();
        CompositionOutputBundleProposal proposal = await PrepareLegacyProposalAsync(host, workspace);
        var vm = new OutputDeliveryConfirmationViewModel(host.CompositionOutputNaming,
            () => ShellTextResources.For(ShellLanguage.English));
        var request = new OutputDeliveryRequest(proposal, false, null, () => true, null, null, null,
            _ => Task.CompletedTask);
        vm.Open(request);
        string original = vm.ParentDirectory;
        var picker = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task selection = OutputDeliveryConfirmationModal.ChooseParentWithPickerAsync(vm, () => picker.Task);
        vm.CancelCommand.Execute(null);
        if (reopen) { vm.Open(request); }
        picker.SetResult(workspace.Root);
        await selection;

        Assert.Equal(original, vm.ParentDirectory);
        Assert.Equal(reopen, vm.IsOpen);
    }

    private static async Task<CompositionOutputBundleProposal> PrepareLegacyProposalAsync(CompositionHostServices host, TempWorkspace workspace)
    {
        CompiledAuthoringSessionPreparation prepared = host.AbMergeAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51932", null,
            [new("tp-a-input", workspace.PathFor("a.bin"), CreateLegacyTp()), new("tp-b-input", workspace.PathFor("b.bin"), CreateLegacyTp())], AbMergeDpMode.Dummy);
        Assert.True(prepared.Succeeded);
        return await host.CompositionOutputNaming.PrepareBundleProposalAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
    }

    private static byte[] CreateLegacyTp()
    {
        byte[] tp = new byte[0x40000];
        tp[0x36001] = 0xFF;
        tp[0x36017] = 1;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(tp, 0x36FFC);
        return tp;
    }

    private sealed class DelayedConfirmationNaming(ICompositionOutputNaming inner) : ICompositionOutputNaming
    {
        public CompositionOutputBundleValidationIssue? ValidateName(string value)
        {
            return inner.ValidateName(value);
        }

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
    [InlineData(false, false, false, 3, 3, false, true)]
    [InlineData(true, false, true, 1, 1, true)]
    [InlineData(true, false, false, 1, 1, false, false, 0xA3, 0xA4, true)]
    [InlineData(true, false, true, 1, 1, false, false, 0xF1, 0xF2, true)]
    public async Task ApprovedOutputConfirmationStates(bool bundle, bool additional, bool chineseDark, byte countA,
        byte countB, bool tallerCjkMetrics = false, bool oversizedTp = false, byte rawA = 0x97, byte rawB = 0xA6,
        bool customAlias = false)
    {
        using TempWorkspace workspace = TempWorkspace.Create("output-confirmation-reference");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(), loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(customAlias ? [new("desay", "My_vendor", [rawA, rawB])] :
            configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        string ic = additional ? "NT51932" : "NT51950";
        byte[] tp = new byte[additional ? 0x40000 : oversizedTp ? 0x37010 : 0x37000];
        tp[0x22200] = 0x31;
        tp[0x22201] = 0xCE;
        tp[0x2220C] = rawA;
        tp[0x36000] = 0x42;
        tp[0x36001] = 0xBD;
        tp[0x36017] = countA;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(tp, 0x36FFC);
        byte[] tpB = (byte[])tp.Clone();
        tpB[0x36017] = countB;
        tpB[0x2220C] = rawB;
        var inputs = new List<CompiledAuthoringSelectedInput>
        {
            new("tp-a-input", workspace.PathFor("input-tpa.bin"), tp),
            new("tp-b-input", workspace.PathFor("input-tpb.bin"), tpB),
        };
        if (!additional) { inputs.Insert(0, new("dp-ab-input", workspace.PathFor("input-dp-ab.bin"), new byte[countA == 1 ? 0x80000 : 0x100000])); }
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
        Assert.Equal(additional || countA == 1 ? "512 KiB (524,288 bytes)" : "1 MiB (1,048,576 bytes)", vm.FlashOutputSize);
        Assert.Equal("AB Code", vm.ModeSummary);
        Assert.Equal(additional ? 2 : 3, vm.InputRows.Count);
        if (oversizedTp)
        {
            Assert.Contains("Cascade", vm.TargetSummary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("2 IC", vm.TargetSummary, StringComparison.Ordinal);
            Assert.False(vm.HasInputWarnings);
            Assert.DoesNotContain(vm.InputRows, row => row.HasWarning);
            foreach (CompositionOutputInputSummary input in proposal.Confirmation!.Inputs.Where(input => input.BindingId is "tp-a-input" or "tp-b-input"))
            {
                Assert.Equal(new ByteRange(0, 0x37000), input.Inspection!.AcceptedSnapshotRange);
                Assert.Equal(new ByteRange(0x37000, 0x10), input.Inspection.IgnoredTrailingRange);
                Assert.False(input.Inspection.BlocksBuild);
            }
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
            string expectedA = rawA == 0xA3 ? "0xA3 - Auto STLA v1" : rawA == 0xF1 ? "0xF1 - My_vendor" : "0x97 - Auto Desay";
            string expectedB = rawB == 0xA4 ? "0xA4 - Auto INX v7" : rawB == 0xF2 ? "0xF2 - My_vendor" : "0xA6 - Auto Desay Palminfo";
            Assert.Equal([new OutputConfirmationCheck("TP A", expectedA), new OutputConfirmationCheck("TP B", expectedB)], vm.EventBufferChecks);
            Assert.Equal(countA == 1 ? "524,288 bytes" : "1,048,576 bytes", Assert.Single(vm.ExpectedInputChecks).Value);
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
            TextBlock flashMap = Assert.IsType<TextBlock>(modal.FindControl<TextBlock>("OutputFlashMapValue"));
            Assert.Equal("Common", flashMap.Text);
            Assert.Equal(target.Bounds.X, flashMap.Bounds.X);
            Assert.True(flashMap.Bounds.Y >= mode.Bounds.Bottom);
            TextBlock size = modal.FindControl<TextBlock>("FlashOutputSizeValue")!;
            Assert.True(size.Bounds.Y >= flashMap.Bounds.Bottom);
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
                TextBlock eventLabel = Assert.IsType<TextBlock>(modal.FindControl<TextBlock>("OutputEventBufferFormatLabel"));
                Assert.Equal(chineseDark ? "Event Buffer 格式" : "Event Buffer Format", eventLabel.Text);
                ItemsControl eventRows = Assert.IsType<ItemsControl>(modal.FindControl<ItemsControl>("OutputEventBufferFormatChecks"));
                TextBlock[] eventValues = [.. Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(eventRows).OfType<TextBlock>()
                    .Where(block => block.Text?.StartsWith("0x", StringComparison.Ordinal) == true)];
                Assert.Equal(2, eventValues.Length);
                Assert.Equal(vm.EventBufferChecks.Select(check => check.Value), eventValues.Select(block => block.Text));
                Point first = eventValues[0].TranslatePoint(default, modal)!.Value;
                Point second = eventValues[1].TranslatePoint(default, modal)!.Value;
                Assert.Equal(first.X, second.X);
                Assert.True(second.Y >= first.Y + eventValues[0].Bounds.Height);
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
                frame.Save(Path.Combine(outputDirectory, $"output-confirmation-{(bundle ? "bundle" : "loose")}-{(additional ? "extra" : "single")}-{(chineseDark ? "dark-zh" : "light-en")}{(oversizedTp ? "-cascade" : "")}{(customAlias ? $"-{rawA:X2}-{rawB:X2}" : "")}.png"));
            }
        }
        finally { window.Close(); }
    }
}
