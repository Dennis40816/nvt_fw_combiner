using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.Behaviors;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using System.Xml.Linq;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>The selected soft-lift treatment stays theme-aware without restoring the old dark frame.</summary>
    [Fact]
    public void MemoryCoverageSoftLiftUsesSharedThemeMotionAndFocusContracts()
    {
        string styles = ReadPresentationFile("Styles/MemoryCoverageStyles.axaml");
        string templates = ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml");
        string workflowTemplates = ReadPresentationFile("Resources/MainWindowWorkflowTemplates.axaml");
        string themeTokens = ReadPresentationFile("Styles/ThemeTokens.axaml");
        string barSegment = ExtractStyle(styles, "Border.memoryCoverageBarSegment");
        string linkedRow = ExtractStyle(styles, "Border.memoryCoverageLinkedRow.linked");
        string linkedSegment = ExtractStyle(styles, "Border.memoryCoverageBarSegment.linked");

        Assert.Contains("NfcMemoryInteractionSurfaceBrush", linkedRow, StringComparison.Ordinal);
        Assert.Contains("NfcMemoryRowHoverShadow", linkedRow, StringComparison.Ordinal);
        Assert.Contains("translateY(-1px)", linkedRow, StringComparison.Ordinal);
        Assert.Contains("NfcMemorySegmentHoverShadow", linkedSegment, StringComparison.Ordinal);
        Assert.Contains("scaleY(1.18)", linkedSegment, StringComparison.Ordinal);
        Assert.Contains("Property=\"BorderThickness\" Value=\"0\"", barSegment, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderBrush", barSegment, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderThickness", linkedSegment, StringComparison.Ordinal);
        Assert.Equal(
            2,
            templates.Split("RenderTransformOrigin=\"50%,50%\"", StringSplitOptions.None).Length - 1);
        AssertTrackAllowsSegmentLift(templates, "{Binding MergeCoverageSegments}");
        AssertTrackAllowsSegmentLift(workflowTemplates, "{Binding ReplaceCoverageSegments}");
        Assert.DoesNotContain("NfcMemoryAddressTextBrush", linkedSegment, StringComparison.Ordinal);
        Assert.Contains("MemoryCoverageTooltipTemplate", templates, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FocusToolTipBehavior.IsEnabled",
            ExtractDataTemplate(templates, "MemoryCoverageSegmentBarTemplate"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FocusToolTipBehavior.IsEnabled",
            ExtractDataTemplate(templates, "MemoryCoveragePlainSegmentBarTemplate"),
            StringComparison.Ordinal);
        Assert.Contains(
            "FocusToolTipBehavior.IsEnabled",
            ExtractDataTemplate(templates, "MemoryCoverageLogicalItemTemplate"),
            StringComparison.Ordinal);
        Assert.Contains(
            "FocusToolTipBehavior.IsEnabled",
            ExtractDataTemplate(templates, "MemoryCoverageSegmentListTemplate"),
            StringComparison.Ordinal);
        Assert.Contains(
            "FocusToolTipBehavior.IsEnabled",
            ExtractDataTemplate(templates, "MemoryCoveragePlainSegmentListTemplate"),
            StringComparison.Ordinal);
        Assert.Equal(
            5,
            templates.Split(
                "ContentTemplate=\"{StaticResource MemoryCoverageTooltipTemplate}\"",
                StringSplitOptions.None).Length - 1);

        Assert.Contains("Border.memoryCoverageLinkedRow:focus-visible", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("Border.memoryCoverageBarSegment:focus-visible", styles, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", styles, StringComparison.Ordinal);
        Assert.Contains("linked.reducedMotion", styles, StringComparison.Ordinal);
        Assert.Equal(
            5,
            templates.Split(
                "Classes.reducedMotion=\"{ReflectionBinding $parent[Window].DataContext.IsReducedMotionEnabled}\"",
                StringSplitOptions.None).Length - 1);

        Assert.Contains("x:Key=\"NfcMemoryInteractionSurfaceBrush\" Color=\"#EEF5FF\"", themeTokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcMemoryInteractionSurfaceBrush\" Color=\"#1B3150\"", themeTokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcMemoryTrackBrush\" Color=\"#E7EDF5\"", themeTokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcMemoryTrackBrush\" Color=\"#263449\"", themeTokens, StringComparison.Ordinal);
        Assert.DoesNotContain("NfcMemorySegmentDividerBrush", themeTokens, StringComparison.Ordinal);
        Assert.Equal(2, themeTokens.Split("x:Key=\"NfcMemoryRowHoverShadow\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, themeTokens.Split("x:Key=\"NfcMemorySegmentHoverShadow\"", StringSplitOptions.None).Length - 1);
    }

    /// <summary>First, middle, and last segments lift without retaining the divider as a visible edge.</summary>
    [AvaloniaTheory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public void MemoryCoverageSoftLiftAppliesWithoutEdgeInProductionStyles(
        bool useDarkTheme,
        int activeSegmentIndex)
    {
        ThemeVariant theme = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        var row = new Border
        {
            Width = 300,
            Height = 48,
            Classes = { "memoryCoverageLinkedRow", "linked" },
        };
        Border[] segments = [.. Enumerable.Range(0, 3)
            .Select(index => new Border
            {
                Width = 80,
                Height = 34,
                Background = index switch
                {
                    0 => Brushes.SlateGray,
                    1 => Brushes.DeepSkyBlue,
                    _ => Brushes.MediumVioletRed,
                },
                Classes = { "memoryCoverageBarSegment" },
            })];
        Border segment = segments[activeSegmentIndex];
        segment.Classes.Add("linked");
        var track = new Border
        {
            Height = 34,
            ClipToBounds = false,
            Background = Brushes.SlateGray,
            Child = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Children = { segments[0], segments[1], segments[2] },
            },
        };
        var host = new Window
        {
            Width = 320,
            Height = 120,
            RequestedThemeVariant = theme,
            Content = new StackPanel
            {
                Children = { row, track },
            },
        };

        host.Show();
        try
        {
            host.Measure(new Size(320, 120));
            host.Arrange(new Rect(0, 0, 320, 120));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Assert.True(Avalonia.Application.Current!.TryGetResource(
                "NfcMemoryInteractionSurfaceBrush",
                theme,
                out object? expectedSurface));
            Assert.Equal(
                Assert.IsType<SolidColorBrush>(expectedSurface).Color,
                Assert.IsType<ISolidColorBrush>(row.Background, exactMatch: false).Color);
            Assert.True(row.BoxShadow.Count > 0);
            Assert.True(row.BoxShadow[0].Color.A > 0);
            Assert.NotNull(row.RenderTransform);
            Assert.False(row.RenderTransform.Value.IsIdentity);

            Assert.Equal(new Thickness(0), segment.BorderThickness);
            Assert.Null(segment.BorderBrush);
            Assert.True(segment.BoxShadow.Count > 0);
            Assert.True(segment.BoxShadow[0].Color.A > 0);
            Assert.NotNull(segment.RenderTransform);
            Assert.False(segment.RenderTransform.Value.IsIdentity);
            using Avalonia.Media.Imaging.Bitmap? frame = host.GetLastRenderedFrame();
            Assert.NotNull(frame);

            row.Classes.Add("reducedMotion");
            segment.Classes.Add("reducedMotion");
            Assert.True(row.RenderTransform.Value.IsIdentity);
            Assert.True(segment.RenderTransform.Value.IsIdentity);
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>The production Merge and Replace bars render the active segment beyond both track edges.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ProductionMemoryCoverageSegmentLiftCrossesTrackBoundary(
        bool useDarkTheme,
        bool useReplace)
    {
        ThemeVariant theme = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        MainWindowViewModel viewModel = await Task.Run(
            () => PresentationTestHost.CreateViewModel(),
            TestContext.Current.CancellationToken);
        if (useReplace)
        {
            viewModel.ShowReplaceCommand.Execute(null);
        }
        else
        {
            viewModel.ShowMergeCommand.Execute(null);
        }
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        var sharedResources = new ResourceInclude(ProductionSharedTemplatesUri)
        {
            Source = ProductionSharedTemplatesUri,
        };
        var workflowResources = new ResourceInclude(ProductionWorkflowTemplatesUri)
        {
            Source = ProductionWorkflowTemplatesUri,
        };
        ResourceInclude templateResources = useReplace ? workflowResources : sharedResources;
        Assert.True(templateResources.TryGetResource(
            useReplace ? "ReplaceOutputLayoutPanelTemplate" : "MergeOutputLayoutPanelTemplate",
            theme,
            out object? templateResource));
        IDataTemplate template = Assert.IsType<IDataTemplate>(templateResource, exactMatch: false);
        var panel = new ContentControl
        {
            Width = 430,
            Content = useReplace ? viewModel.Replace : viewModel.Merge,
            ContentTemplate = template,
        };
        var host = new Window
        {
            Width = 470,
            Height = 720,
            RequestedThemeVariant = theme,
            Content = panel,
        };
        host.Resources.MergedDictionaries.Add(sharedResources);
        if (useReplace)
        {
            host.Resources.MergedDictionaries.Add(workflowResources);
        }
        host.Styles.Add(new StyleInclude(ProductionMainWindowStylesUri)
        {
            Source = ProductionMainWindowStylesUri,
        });
        host.Styles.Add(new StyleInclude(ProductionButtonStylesUri)
        {
            Source = ProductionButtonStylesUri,
        });
        host.Styles.Add(new StyleInclude(ProductionVisualStylesUri)
        {
            Source = ProductionVisualStylesUri,
        });

        host.Show();
        try
        {
            host.Measure(new Size(470, 720));
            host.Arrange(new Rect(0, 0, 470, 720));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            ItemsControl items = Assert.Single(
                panel.GetVisualDescendants().OfType<ItemsControl>(),
                candidate => candidate.Height == 34 &&
                    candidate.GetVisualDescendants().OfType<Border>()
                        .Any(segment => segment.Classes.Contains("memoryCoverageBarSegment")));
            Border track = Assert.IsType<Border>(items.Parent, exactMatch: false);
            MemoryCoverageSegmentViewModel active = useReplace
                ? viewModel.Replace.ReplaceCoverageSegments[0]
                : viewModel.Merge.MergeCoverageSegments[0];
            var pointerOwner = new object();
            active.Interaction.SetPointerActive(pointerOwner, true);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Border activeSegment = Assert.Single(
                items.GetVisualDescendants().OfType<Border>(),
                candidate => ReferenceEquals(candidate.DataContext, active) &&
                    candidate.Classes.Contains("memoryCoverageBarSegment"));
            Assert.Contains("linked", activeSegment.Classes);
            Assert.NotNull(activeSegment.RenderTransform);
            Assert.Equal(1.18, activeSegment.RenderTransform.Value.M22, precision: 2);
            Assert.False(track.ClipToBounds);
            Assert.False(items.ClipToBounds);
            Assert.False(activeSegment.Focusable);
            Assert.False(FocusToolTipBehavior.GetIsEnabled(activeSegment));
            ContentControl segmentCard = Assert.IsType<ContentControl>(ToolTip.GetTip(activeSegment));
            ToolTip.SetIsOpen(activeSegment, true);
            Dispatcher.UIThread.RunJobs();
            Assert.True(ToolTip.GetIsOpen(activeSegment));
            Assert.Same(active, segmentCard.Content);
            Assert.NotNull(segmentCard.ContentTemplate);
            ToolTip.SetIsOpen(activeSegment, false);

            Border activeRow = Assert.Single(
                panel.GetVisualDescendants().OfType<Border>(),
                candidate => ReferenceEquals(candidate.DataContext, active) &&
                    candidate.Classes.Contains("memoryCoverageLinkedRow"));
            Assert.Contains("linked", activeRow.Classes);
            Assert.True(FocusToolTipBehavior.GetIsEnabled(activeRow));
            Assert.True(Avalonia.Application.Current!.TryGetResource(
                "NfcMemoryInteractionSurfaceBrush",
                theme,
                out object? expectedRowSurface));
            Assert.Equal(
                Assert.IsType<SolidColorBrush>(expectedRowSurface).Color,
                Assert.IsType<ISolidColorBrush>(activeRow.Background, exactMatch: false).Color);
            ContentControl rowCard = Assert.IsType<ContentControl>(ToolTip.GetTip(activeRow));
            activeRow.RaiseEvent(new FocusChangedEventArgs(InputElement.GotFocusEvent)
            {
                NavigationMethod = NavigationMethod.Tab,
            });
            Dispatcher.UIThread.RunJobs();
            Assert.True(ToolTip.GetIsOpen(activeRow));
            Assert.Same(active, rowCard.Content);
            Assert.NotNull(rowCard.ContentTemplate);
            activeRow.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));
            Assert.False(ToolTip.GetIsOpen(activeRow));

            for (Visual? ancestor = activeSegment; ancestor is not null && ancestor != track;
                 ancestor = ancestor.GetVisualParent())
            {
                if (ancestor is Control control)
                {
                    Assert.False(control.ClipToBounds);
                }
            }

            Point top = Assert.IsType<Point>(activeSegment.TranslatePoint(
                new Point(activeSegment.Bounds.Width / 2, 0),
                track));
            Point bottom = Assert.IsType<Point>(activeSegment.TranslatePoint(
                new Point(activeSegment.Bounds.Width / 2, activeSegment.Bounds.Height),
                track));
            Assert.True(top.Y < 0, $"Expected active segment top above track, got {top.Y:F2}.");
            Assert.True(
                bottom.Y > track.Bounds.Height,
                $"Expected active segment bottom below {track.Bounds.Height:F2}, got {bottom.Y:F2}.");
            Assert.NotNull(host.GetLastRenderedFrame());

            active.Interaction.SetPointerActive(pointerOwner, false);
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>The map is hover-only while information rows remain the sole persistent focus target.</summary>
    [Fact]
    public void MemoryCoverageMapCannotPinSelectionButInformationRowsCan()
    {
        string templates = ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml");
        foreach (string templateName in new[]
        {
            "MemoryCoverageSegmentBarTemplate",
            "MemoryCoveragePlainSegmentBarTemplate",
        })
        {
            string mapTemplate = ExtractDataTemplate(templates, templateName);
            Assert.Contains("Focusable=\"False\"", mapTemplate, StringComparison.Ordinal);
            Assert.Contains(
                "MemoryCoverageInteractionBehavior.IsEnabled=\"True\"",
                mapTemplate,
                StringComparison.Ordinal);
            Assert.DoesNotContain("FocusToolTipBehavior.IsEnabled", mapTemplate, StringComparison.Ordinal);
            Assert.DoesNotContain(" Command=", mapTemplate, StringComparison.Ordinal);
            Assert.DoesNotContain(" Click=", mapTemplate, StringComparison.Ordinal);
            Assert.DoesNotContain("Tapped=", mapTemplate, StringComparison.Ordinal);
            Assert.DoesNotContain("PointerPressed=", mapTemplate, StringComparison.Ordinal);
        }

        foreach (string templateName in new[]
        {
            "MemoryCoverageSegmentListTemplate",
            "MemoryCoveragePlainSegmentListTemplate",
            "MemoryCoverageLogicalItemTemplate",
        })
        {
            string rowTemplate = ExtractDataTemplate(templates, templateName);
            Assert.Contains("Focusable=\"True\"", rowTemplate, StringComparison.Ordinal);
            Assert.Contains("FocusToolTipBehavior.IsEnabled=\"True\"", rowTemplate, StringComparison.Ordinal);
            Assert.Contains(
                "MemoryCoverageInteractionBehavior.IsEnabled=\"True\"",
                rowTemplate,
                StringComparison.Ordinal);
        }
    }

    /// <summary>Pointer and focus owners keep every linked row/segment state synchronized.</summary>
    [Fact]
    public void MemoryCoverageInteractionLinksLogicalRowsAndEverySegment()
    {
        var first = new MemoryCoverageSegmentViewModel(
            "0x00000-0x0000F",
            "NF CtrlRAM",
            "Selected bytes",
            MemoryCoverageFillRole.CtrlRamNf,
            10,
            rangeStart: 0,
            rangeEndExclusive: 0x10);
        var second = new MemoryCoverageSegmentViewModel(
            "0x00010-0x0001F",
            "NF CtrlRAM",
            "Retained bytes",
            MemoryCoverageFillRole.CtrlRamNf,
            10,
            rangeStart: 0x10,
            rangeEndExclusive: 0x20);
        var item = new MemoryCoverageLogicalItemViewModel(
            "slot:replace-ctrlram-nf",
            [first, second],
            ShellTextResources.For(ShellLanguage.English));
        var pointerOwner = new object();
        var focusOwner = new object();

        item.Interaction.SetPointerActive(pointerOwner, true);
        Assert.All(item.Segments, segment => Assert.True(segment.Interaction.IsActive));

        second.Interaction.SetFocusActive(focusOwner, true);
        item.Interaction.SetPointerActive(pointerOwner, false);
        Assert.All(item.Segments, segment => Assert.True(segment.Interaction.IsActive));

        second.Interaction.SetFocusActive(focusOwner, false);
        Assert.All(item.Segments, segment => Assert.False(segment.Interaction.IsActive));

        var row = new Border { DataContext = item };
        MemoryCoverageInteractionBehavior.SetIsEnabled(row, true);
        row.RaiseEvent(new FocusChangedEventArgs(InputElement.GotFocusEvent));
        Assert.All(item.Segments, segment => Assert.True(segment.Interaction.IsActive));
        row.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));
        Assert.All(item.Segments, segment => Assert.False(segment.Interaction.IsActive));
    }

    /// <summary>Pointer ownership migrates across recycled DataContexts and clears on exit.</summary>
    [AvaloniaFact]
    public void MemoryCoveragePointerLeaseClearsRecycledDataContext()
    {
        MemoryCoverageLogicalItemViewModel first = CreateInteractionItem("first");
        MemoryCoverageLogicalItemViewModel second = CreateInteractionItem("second");
        var row = new Border
        {
            Width = 80,
            Height = 32,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Background = Brushes.Transparent,
            DataContext = first,
        };
        MemoryCoverageInteractionBehavior.SetIsEnabled(row, true);
        var host = new Window
        {
            Width = 200,
            Height = 100,
            Content = row,
        };

        host.Show();
        try
        {
            host.MouseMove(new Point(10, 10), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(first.Interaction.IsActive);
            Assert.False(row.IsFocused);

            row.DataContext = second;
            Dispatcher.UIThread.RunJobs();
            Assert.False(first.Interaction.IsActive);
            Assert.True(second.Interaction.IsActive);

            host.MouseMove(new Point(180, 80), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.False(first.Interaction.IsActive);
            Assert.False(second.Interaction.IsActive);
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>Focus leases clear on recycle, disable and detach without cancelling another owner.</summary>
    [AvaloniaFact]
    public void MemoryCoverageFocusLeaseCleansLifecycleAndPreservesOtherOwners()
    {
        MemoryCoverageLogicalItemViewModel first = CreateInteractionItem("first");
        MemoryCoverageLogicalItemViewModel second = CreateInteractionItem("second");
        var firstOwner = new Border { Focusable = true, DataContext = first };
        var secondOwner = new Border { Focusable = true, DataContext = first };
        MemoryCoverageInteractionBehavior.SetIsEnabled(firstOwner, true);
        MemoryCoverageInteractionBehavior.SetIsEnabled(secondOwner, true);
        var panel = new StackPanel { Children = { firstOwner, secondOwner } };
        var host = new Window { Content = panel };

        host.Show();
        try
        {
            firstOwner.RaiseEvent(new FocusChangedEventArgs(InputElement.GotFocusEvent));
            secondOwner.RaiseEvent(new FocusChangedEventArgs(InputElement.GotFocusEvent));
            Assert.True(first.Interaction.IsActive);

            firstOwner.DataContext = second;
            Assert.True(first.Interaction.IsActive);
            Assert.True(second.Interaction.IsActive);
            firstOwner.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));
            Assert.False(second.Interaction.IsActive);
            Assert.True(first.Interaction.IsActive);

            secondOwner.IsEnabled = false;
            Assert.False(first.Interaction.IsActive);

            firstOwner.IsEnabled = true;
            firstOwner.RaiseEvent(new FocusChangedEventArgs(InputElement.GotFocusEvent));
            Assert.True(second.Interaction.IsActive);
            _ = panel.Children.Remove(firstOwner);
            Dispatcher.UIThread.RunJobs();
            Assert.False(second.Interaction.IsActive);
        }
        finally
        {
            host.Close();
        }
    }

    private static MemoryCoverageLogicalItemViewModel CreateInteractionItem(string id)
    {
        var segment = new MemoryCoverageSegmentViewModel(
            "0x00000-0x0000F",
            id,
            "Selected bytes",
            MemoryCoverageFillRole.CtrlRamNf,
            10,
            rangeStart: 0,
            rangeEndExclusive: 0x10);
        return new MemoryCoverageLogicalItemViewModel(
            $"slot:{id}",
            [segment],
            ShellTextResources.For(ShellLanguage.English));
    }

    private static void AssertTrackAllowsSegmentLift(string xaml, string itemsSource)
    {
        var document = XDocument.Parse(xaml);
        XElement items = Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "ItemsControl" &&
            (string?)element.Attribute("ItemsSource") == itemsSource &&
            ((string?)element.Attribute("ItemTemplate"))?.Contains(
                "BarTemplate",
                StringComparison.Ordinal) == true);
        XElement track = Assert.IsType<XElement>(items.Parent);
        Assert.Equal("Border", track.Name.LocalName);
        Assert.Equal("False", (string?)track.Attribute("ClipToBounds"));
        Assert.Equal("False", (string?)items.Attribute("ClipToBounds"));
    }
}
