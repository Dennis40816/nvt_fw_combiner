using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Preserves the loaded Event Buffer editor's spacing, density and editing affordances.</summary>
public sealed class SettingsEventBufferSpacingRoleTests
{
    /// <summary>Spacing roles retain the existing geometry across language, theme and viewport.</summary>
    [AvaloniaTheory]
    [InlineData(false, false, 1672, 941)]
    [InlineData(false, true, 1672, 941)]
    [InlineData(true, false, 1672, 941)]
    [InlineData(true, true, 1672, 941)]
    [InlineData(false, false, 980, 640)]
    [InlineData(false, true, 980, 640)]
    [InlineData(true, false, 980, 640)]
    [InlineData(true, true, 980, 640)]
    public async Task LoadedTemplatePreservesSpacingAndEditing(
        bool dark, bool traditionalChinese, double width, double height)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        MainWindowViewModel viewModel = await Task.Run(async () =>
        {
            PresentationHostServices services = await PresentationTestHost.CreateConfiguredFormatServicesAsync(
                workspace, ApplicationVersionProvider.InformationalVersion);
            return PresentationTestHost.PublishCanonicalCatalog(services, ShellViewModelFactory.Create(services,
                traditionalChinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English));
        }, TestContext.Current.CancellationToken);
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;
        var modal = new SettingsModal { DataContext = viewModel, IsOpen = true };
        var window = new Window
        {
            Width = width,
            Height = height,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            DataContext = viewModel,
            Content = modal,
        };
        foreach (string name in new[] { "SettingsVersionPageTemplate", "MainWindowPageTemplates" })
        {
            var source = new Uri($"avares://NvtFwCombiner.Presentation.Avalonia/Resources/{name}.axaml");
            window.Resources.MergedDictionaries.Add(new ResourceInclude(source) { Source = source });
        }
        foreach (string name in new[] { "MainWindowStyles", "MainWindowButtonStyles", "MainWindowVisualStyles" })
        {
            var source = new Uri($"avares://NvtFwCombiner.Presentation.Avalonia/Styles/{name}.axaml");
            window.Styles.Add(new StyleInclude(source) { Source = source });
        }

        try
        {
            window.Show();
            Render(window);
            using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            Grid page = Assert.Single(window.GetVisualDescendants().OfType<Grid>(),
                grid => grid.Name == "EventBufferFormatPageRoot");
            Assert.Equal(window.RequestedThemeVariant, page.ActualThemeVariant);
            Assert.Equal(20, Assert.IsType<double>(page.FindResource("NfcEventBufferSectionSpacing")));
            Assert.Equal(28, Assert.IsType<double>(page.FindResource("NfcEventBufferPageGap")));
            Assert.Equal(new Thickness(0, 20, 0, 0),
                Assert.IsType<Thickness>(page.FindResource("NfcEventBufferSectionDividerMargin")));
            Assert.Equal(new Thickness(0, 28, 0, 0),
                Assert.IsType<Thickness>(page.FindResource("NfcEventBufferPageActionsPadding")));
            Assert.Equal(new Thickness(36, 20, 36, 32), page.Margin);
            Assert.Equal(28, page.RowSpacing);
            StackPanel heading = Assert.Single(page.Children.OfType<StackPanel>());
            Assert.Contains("eventBufferTitleDetail", heading.Classes);
            Assert.Equal(8, heading.Spacing);
            TextBlock title = Assert.Single(heading.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Classes.Contains("pageTitle"));
            TextBlock subtitle = Assert.Single(heading.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Classes.Contains("pageSubtitle"));
            Assert.Equal(viewModel.Text.EventBufferFormatTitle, title.Text);
            Assert.Equal(viewModel.Text.EventBufferFormatSubtitle, subtitle.Text);
            Grid titleRow = Assert.IsType<Grid>(title.Parent);
            Assert.Equal(12, titleRow.ColumnSpacing);
            AssertVerticalGap(titleRow, subtitle, page, 8);
            Border divider = Assert.Single(heading.Children.OfType<Border>());
            Assert.Equal(new Thickness(0, 20, 0, 0), divider.Margin);
            AssertVerticalGap(subtitle, divider, page, 28);

            ScrollViewer scroll = Assert.Single(page.Children.OfType<ScrollViewer>());
            StackPanel sections = Assert.IsType<StackPanel>(scroll.Content);
            Assert.Equal(20, sections.Spacing);
            Border columnHeader = Assert.Single(sections.Children.OfType<Border>());
            Grid headerColumns = Assert.IsType<Grid>(columnHeader.Child);
            Assert.Equal(20, headerColumns.ColumnSpacing);
            EventBufferFormatDraftRowViewModel row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
            ComboBox identity = Assert.Single(sections.GetVisualDescendants().OfType<ComboBox>(),
                control => ReferenceEquals(control.DataContext, row));
            Grid columns = Assert.IsType<Grid>(identity.Parent);
            Assert.Equal(20, columns.ColumnSpacing);
            Assert.Equal("1.05*,1.15*,1.7*,0.85*", columns.ColumnDefinitions.ToString());
            TextBox alias = Assert.Single(columns.Children.OfType<TextBox>());
            Border rowBorder = Assert.IsType<Border>(columns.Parent);
            StackPanel rowSection = Assert.IsType<StackPanel>(rowBorder.Parent);
            Assert.Equal(20, rowSection.Spacing);
            Assert.Equal(new Thickness(12, 6, 12, 24), rowBorder.Padding);
            TextBlock applicability = Assert.Single(rowSection.Children.OfType<TextBlock>());
            AssertVerticalGap(rowBorder, applicability, sections, 20);
            ItemsControl rows = Assert.Single(sections.Children.OfType<ItemsControl>());
            AssertVerticalGap(columnHeader, rows, sections, 20);
            Grid identityHint = Assert.Single(sections.Children.OfType<Grid>());
            AssertVerticalGap(rows, identityHint, sections, 20);
            Assert.Equal(44, identity.Bounds.Height);
            Assert.Equal(44, alias.Bounds.Height);
            Assert.Equal(20, Bounds(alias, page).Left - Bounds(identity, page).Right, 1);
            Assert.Equal(Bounds(identity, page).Center.Y, Bounds(alias, page).Center.Y, 1);
            TextBlock[] headers = [.. headerColumns.Children.OfType<TextBlock>()];
            Assert.Equal(4, headers.Length);
            foreach (Control cell in columns.Children)
            {
                int column = Grid.GetColumn(cell);
                Assert.Equal(Bounds(headers[column], page).Left, Bounds(cell, page).Left, 1);
                Assert.Equal(Bounds(headers[column], page).Right, Bounds(cell, page).Right, 1);
            }

            Border footer = Assert.Single(page.Children.OfType<Border>(), border => Grid.GetRow(border) == 2);
            Assert.Equal(new Thickness(0, 28, 0, 0), footer.Padding);
            Grid actions = Assert.IsType<Grid>(footer.Child);
            AssertVerticalGap(heading, scroll, page, 28);
            AssertVerticalGap(scroll, footer, page, 28);
            Assert.Equal(29, Bounds(actions, page).Top - Bounds(footer, page).Top, 1);
            Button Action(System.Windows.Input.ICommand command)
            {
                return Assert.Single(page.GetVisualDescendants().OfType<Button>(),
                    button => ReferenceEquals(button.Command, command));
            }
            Button save = Action(viewModel.Settings.SaveEventBufferFormatCommand);
            Button discard = Action(viewModel.Settings.DiscardEventBufferFormatChangesCommand);
            Assert.False(save.IsEnabled);
            Assert.False(discard.IsEnabled);
            Assert.All(actions.Children.OfType<Button>(), button =>
            {
                Assert.Equal(48, button.Bounds.Height);
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
                AssertFits(button, page);
            });
            AssertFits(title, page);
            AssertFits(subtitle, page);

            Press(window, Action(row.BeginAddRecognitionValueCommand));
            TextBox byteInput = Assert.Single(columns.GetVisualDescendants().OfType<TextBox>(),
                control => control.IsEffectivelyVisible && control.Text == "0x");
            Assert.Same(byteInput, window.FocusManager?.GetFocusedElement());
            byteInput.Text = "0x84";
            Press(window, Action(row.AddRecognitionValueCommand));
            Assert.Equal([0x97, 0xA6, 0x84], row.RecognitionValues);
            alias.Text = "Edited alias";
            Render(window);
            Assert.Equal("Edited alias", row.AliasName);
            Assert.True(save.IsEnabled);
            Assert.True(discard.IsEnabled);
            viewModel.CloseSettingsCommand.Execute(null);
            Render(window);
            Border confirmation = Assert.Single(page.Children.OfType<Border>(),
                border => border.Name == "EventBufferFormatCloseConfirmation");
            Assert.True(confirmation.IsEffectivelyVisible);
            Grid confirmationContent = Assert.IsType<Grid>(confirmation.Child);
            StackPanel detail = Assert.Single(confirmationContent.Children.OfType<StackPanel>(),
                panel => Grid.GetColumn(panel) == 0);
            Assert.Contains("eventBufferTitleDetail", detail.Classes);
            Assert.Contains("compact", detail.Classes);
            Assert.Equal(4, detail.Spacing);
            AssertVerticalGap(detail.Children[0], detail.Children[1], page, 4);
            AssertFits(confirmation, page);
            Press(window, Action(viewModel.Settings.CancelEventBufferFormatCloseCommand));
            Assert.False(viewModel.Settings.IsEventBufferFormatCloseConfirmationOpen);
            Assert.Equal("Edited alias", row.AliasName);
            Assert.Equal([0x97, 0xA6, 0x84], row.RecognitionValues);

            // A distinct value proves each loaded caller consumes its role rather than a matching literal.
            page.Resources["NfcEventBufferSectionSpacing"] = 21d;
            page.Resources["NfcEventBufferSectionDividerMargin"] = new Thickness(0, 21, 0, 0);
            page.Resources["NfcEventBufferPageGap"] = 29d;
            page.Resources["NfcEventBufferPageActionsPadding"] = new Thickness(0, 29, 0, 0);
            Render(window);
            Assert.Equal(21, sections.Spacing);
            Assert.Equal(21, rowSection.Spacing);
            Assert.Equal(new Thickness(0, 21, 0, 0), divider.Margin);
            Assert.Equal(29, page.RowSpacing);
            Assert.Equal(new Thickness(0, 29, 0, 0), footer.Padding);
            AssertVerticalGap(columnHeader, rows, sections, 21);
            AssertVerticalGap(rowBorder, applicability, sections, 21);
            AssertVerticalGap(heading, scroll, page, 29);
            AssertVerticalGap(scroll, footer, page, 29);
            Assert.Equal(8, heading.Spacing);
            Assert.Equal(4, detail.Spacing);
            Assert.Equal(20, columns.ColumnSpacing);
            Assert.Equal(20, headerColumns.ColumnSpacing);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static void Press(Window window, Button button)
    {
        Assert.True(button.Focus());
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        Render(window);
    }

    private static Rect Bounds(Control control, Control relativeTo)
    {
        return new(Assert.IsType<Point>(control.TranslatePoint(default, relativeTo)), control.Bounds.Size);
    }

    private static void AssertVerticalGap(Control above, Control below, Control relativeTo, double expected)
    {
        Assert.Equal(expected, Bounds(below, relativeTo).Top - Bounds(above, relativeTo).Bottom, 1);
    }

    private static void AssertFits(Control control, Control container)
    {
        Rect bounds = Bounds(control, container);
        Assert.True(bounds.Width > 0 && bounds.Height > 0);
        Assert.InRange(bounds.Left, -0.5, container.Bounds.Width);
        Assert.InRange(bounds.Right, 0, container.Bounds.Width + 0.5);
        Assert.InRange(bounds.Top, -0.5, container.Bounds.Height);
        Assert.InRange(bounds.Bottom, 0, container.Bounds.Height + 0.5);
    }
}
