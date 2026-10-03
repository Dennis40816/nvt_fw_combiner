using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.Behaviors;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using Path = Avalonia.Controls.Shapes.Path;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises source details through the actual delivery modal's pointer and keyboard routes.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class OutputDeliverySourceTooltipTests
{
    /// <summary>Source details share one tooltip; warning icons expose their own warning details.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PointerUsesOneCompleteSourceDetailsTooltip(bool chineseDark, bool hasWarning)
    {
        using TempWorkspace workspace = TempWorkspace.Create("output-source-tooltip-pointer");
        OutputDeliveryConfirmationViewModel vm = await CreateViewModelAsync(workspace, chineseDark);
        var modal = new OutputDeliveryConfirmationModal { DataContext = vm, IsOpen = true };
        modal.FindControl<ItemsControl>("SourceFilesList")!.ItemsSource = vm.InputRows.Select(input =>
            input with { Warning = hasWarning ? (chineseDark ? "Build 前請確認輸入。" : "Review input before Build.") : string.Empty });
        var window = new Window
        {
            Content = modal,
            Width = 980,
            Height = 720,
            RequestedThemeVariant = chineseDark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        try
        {
            window.Show();
            Render();
            Grid[] rows = SourceRows(modal);
            Assert.Equal(2, rows.Length);
            IInputElement? focus = window.FocusManager?.GetFocusedElement();
            SelectableTextBlock output = modal.FindControl<SelectableTextBlock>("OutputFileNameDisplay")!;
            Assert.Equal(vm.OutputFileName, Assert.IsType<string>(ToolTip.GetTip(output)));

            foreach (Grid row in rows)
            {
                OutputConfirmationInputRow input = Assert.IsType<OutputConfirmationInputRow>(row.DataContext);
                TextBlock fileName = Assert.Single(row.Children.OfType<TextBlock>(), block => block.Text == input.FileName);
                TextBlock size = Assert.Single(row.Children.OfType<TextBlock>(), block => block.Text == input.Size);
                Grid roleColumn = Assert.Single(row.Children.OfType<Grid>());
                TextBlock role = Assert.Single(roleColumn.Children.OfType<TextBlock>());
                Path warning = Assert.Single(roleColumn.Children.OfType<Path>());
                Assert.Equal(input.HasWarning, warning.IsVisible);
                Assert.Equal(input.Warning, AutomationProperties.GetName(warning));
                Assert.Equal(input.Warning, ToolTip.GetTip(warning));
                Assert.All(row.GetVisualDescendants().OfType<Control>().Where(control => control != warning),
                    control => Assert.Null(ToolTip.GetTip(control)));
                _ = Assert.Single(row.GetVisualDescendants().OfType<Control>().Prepend(row),
                    control => ToolTip.GetTip(control) is Control);
                ToolTip.SetShowDelay(row, 0);
                ToolTip.SetShowDelay(warning, 0);
                Control[] targets = [fileName, size, role];
                Point[] points = [.. targets.Select(control => control.TranslatePoint(
                    new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value),
                    row.TranslatePoint(new Point(110 + (row.ColumnSpacing / 2), row.Bounds.Height / 2), window)!.Value,
                ];
                foreach (Point point in points)
                {
                    window.MouseMove(point);
                    Render();
                    Assert.True(row.IsPointerOver);
                    Assert.True(ToolTip.GetIsOpen(row));
                    _ = Assert.Single(rows, ToolTip.GetIsOpen);
                    Assert.All(targets, control => Assert.False(ToolTip.GetIsOpen(control)));
                    AssertDetails(row, input);
                    Assert.Same(focus, window.FocusManager?.GetFocusedElement());
                }

                if (input.HasWarning)
                {
                    window.MouseMove(warning.TranslatePoint(
                        new Point(warning.Bounds.Width / 2, warning.Bounds.Height / 2), window)!.Value);
                    Render();
                    Assert.True(warning.IsPointerOver);
                    Assert.True(ToolTip.GetIsOpen(warning));
                    Assert.All(rows, source => Assert.False(ToolTip.GetIsOpen(source)));
                    Assert.Same(focus, window.FocusManager?.GetFocusedElement());
                }

                window.MouseMove(new Point(5, 5));
                Render();
                Assert.All(rows, source => Assert.False(ToolTip.GetIsOpen(source)));
                Assert.False(ToolTip.GetIsOpen(warning));
            }

            ToolTip.SetShowDelay(output, 0);
            window.MouseMove(output.TranslatePoint(new Point(5, output.Bounds.Height / 2), window)!.Value);
            Render();
            Assert.True(ToolTip.GetIsOpen(output));
            Assert.All(rows, source => Assert.False(ToolTip.GetIsOpen(source)));
        }
        finally { window.Close(); }
    }

    /// <summary>Tab discloses one row, Escape dismisses details, and moving focus closes and restores them.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeyboardFocusOpensDetailsWithoutDuplicateOrModalDismissal(bool chineseDark)
    {
        using TempWorkspace workspace = TempWorkspace.Create("output-source-tooltip-keyboard");
        OutputDeliveryConfirmationViewModel vm = await CreateViewModelAsync(workspace, chineseDark);
        var modal = new OutputDeliveryConfirmationModal { DataContext = vm, IsOpen = true };
        var window = new Window
        {
            Content = modal,
            Width = 980,
            Height = 720,
            RequestedThemeVariant = chineseDark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        try
        {
            window.Show();
            Render();
            Grid[] rows = SourceRows(modal);
            Assert.Equal(2, rows.Length);
            Assert.All(rows, row =>
            {
                Assert.True(row.Focusable);
                Assert.True(FocusToolTipBehavior.GetIsEnabled(row));
                OutputConfirmationInputRow input = Assert.IsType<OutputConfirmationInputRow>(row.DataContext);
                Assert.Equal(input.FileName, AutomationProperties.GetName(row));
                Assert.Equal($"{input.FileName}\n{input.Size}\n{input.Sha256}", AutomationProperties.GetHelpText(row));
            });
            Assert.True(modal.FindControl<ToggleButton>("SourcesDisclosureToggle")!.Focus(NavigationMethod.Tab));
            foreach ((Grid row, int index) in rows.Select((row, index) => (row, index)))
            {
                Press(window, Key.Tab, PhysicalKey.Tab);
                Assert.Same(row, window.FocusManager?.GetFocusedElement());
                Assert.True(ToolTip.GetIsOpen(row));
                _ = Assert.Single(rows, ToolTip.GetIsOpen);
                AssertDetails(row, vm.InputRows[index]);
            }

            Press(window, Key.Escape, PhysicalKey.Escape);
            Assert.True(vm.IsOpen);
            Assert.Same(rows[1], window.FocusManager?.GetFocusedElement());
            Assert.All(rows, row => Assert.False(ToolTip.GetIsOpen(row)));
            Assert.False(ToolTip.GetServiceEnabled(rows[1]));

            Press(window, Key.Tab, PhysicalKey.Tab, RawInputModifiers.Shift);
            Assert.Same(rows[0], window.FocusManager?.GetFocusedElement());
            Assert.True(ToolTip.GetIsOpen(rows[0]));
            Assert.False(ToolTip.GetIsOpen(rows[1]));
            Assert.True(ToolTip.GetServiceEnabled(rows[1]));
            Press(window, Key.Tab, PhysicalKey.Tab);
            Assert.Same(rows[1], window.FocusManager?.GetFocusedElement());
            Assert.True(ToolTip.GetIsOpen(rows[1]));
            Assert.False(ToolTip.GetIsOpen(rows[0]));
            Press(window, Key.Tab, PhysicalKey.Tab);
            Assert.All(rows, row => Assert.False(ToolTip.GetIsOpen(row)));
            Assert.True(vm.IsOpen);
        }
        finally { window.Close(); }
    }

    private static async Task<OutputDeliveryConfirmationViewModel> CreateViewModelAsync(TempWorkspace workspace, bool chineseDark)
    {
        CompositionHostServices host = CompositionHostServices.Create(IsolatedLocalState.CreateDirectory());
        string first = workspace.Write("source-with-a-long-filename-for-complete-delivery-details.bin", [0xA5, 0x5A]);
        string second = workspace.Write("second-source.bin", [0x12, 0x34, 0x56, 0x78]);
        var mappings = new GeneralMappingDraftState([
            new GeneralMappingDraftRow("first", ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(first), new ByteRange(0, 2), CompositionAddressSpaceIds.OutputImage,
                new ByteRange(0, 2), OverlapPolicy.Reject, 1, "source details"),
            new GeneralMappingDraftRow("second", ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(second), new ByteRange(0, 4), CompositionAddressSpaceIds.OutputImage,
                new ByteRange(2, 4), OverlapPolicy.Reject, 2, "source details")]);
        GeneralAuthoringSessionPreparation prepared = await host.GeneralAuthoring.PrepareMergeSessionAsync(
            new AuthoringSessionState(ExperienceIds.GeneralMerge), "NT51926",
            new GeneralMergeDraftState(new GeneralMergeOutputInitializer(16), mappings), TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded, string.Join(Environment.NewLine, prepared.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        CompositionOutputBundleProposal proposal = await host.CompositionOutputNaming.PrepareBundleProposalAsync(
            prepared.AcceptedSession!, TestContext.Current.CancellationToken);
        var vm = new OutputDeliveryConfirmationViewModel(host.CompositionOutputNaming,
            () => ShellTextResources.For(chineseDark ? ShellLanguage.ChineseTraditional : ShellLanguage.English));
        vm.Open(new OutputDeliveryRequest(proposal, false, null, () => true, null, null, null, _ => Task.CompletedTask));
        vm.SetSourcesExpanded(true);
        Assert.All(vm.InputRows, row => Assert.Equal(64, row.Sha256.Length));
        return vm;
    }

    private static Grid[] SourceRows(OutputDeliveryConfirmationModal modal)
    {
        return [.. modal.FindControl<ItemsControl>("SourceFilesList")!.GetVisualDescendants().OfType<Grid>()
            .Where(row => ToolTip.GetTip(row) is StackPanel)];
    }

    private static void AssertDetails(Grid row, OutputConfirmationInputRow input)
    {
        StackPanel details = Assert.IsType<StackPanel>(ToolTip.GetTip(row));
        string?[] expected = [input.FileName, input.Size, input.Sha256];
        Assert.Equal(expected, details.Children.Select(child => Assert.IsType<TextBlock>(child).Text));
        Assert.NotNull(TopLevel.GetTopLevel(details));
        Assert.True(details.Bounds.Width > 0 && details.Bounds.Height > 0);
    }

    private static void Press(Window window, Key key, PhysicalKey physicalKey, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physicalKey, null);
        window.KeyRelease(key, modifiers, physicalKey, null);
        Render();
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
