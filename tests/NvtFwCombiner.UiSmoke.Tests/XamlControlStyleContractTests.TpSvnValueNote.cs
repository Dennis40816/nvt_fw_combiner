using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.Behaviors;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>
    /// Decision 40: the TP SVN icon is a 12 px outline icon about 4 px after the value, vertically centred,
    /// with a keyboard-reachable standard tooltip below and right of it; the existing warning fact keeps its
    /// stretched row and right-aligned state icon.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(1180)]
    [InlineData(684)]
    public void TpSvnValueNoteSitsAfterTheValueWithoutRestylingTheFact(double width)
    {
        var slot = new FirmwareSlotViewModel("tp", "TP BIN", "TP input", FirmwareSlotKind.Tp)
        {
            FilePath = @"C:\firmware\nt51923-tp-input.bin",
        };
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "Verified");
        slot.SetFirmwareFacts(
        [
            new("TP Version", "T06-00", FirmwareSlotFactState.Warning, "Warning", "Invalid version bar"),
            new("PID", "0x1606"),
            new("Common FW Version", "1.3.0"),
            new("Event Buffer Version", "Common (0x82)"),
            new("TP SVN", "C3 80 13 30")
            {
                Note = new FirmwareSlotFactNote(
                    [new("Flags:", "LOCAL_BUILD, DIFF_EXIST"), new("Revision:", "801330", IsTechnicalValue: true)],
                    [new("Undefined flag bits 0x03. InsertPID.py may be faulty.")]),
            },
        ]);
        var card = new FirmwareSlotCard
        {
            BrowseLabel = "Browse",
            ClearSelectionLabel = "Clear selected file",
            DataContext = slot,
            VerticalAlignment = VerticalAlignment.Top,
            Width = width,
        };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.Width = width;
        host.Height = 360;

        try
        {
            host.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Border[] cells = [.. card.GetVisualDescendants().OfType<Border>()
                .Where(static border => border.Classes.Contains("firmwareSlotFact"))];
            Border svnCell = cells.Single(static cell => ((FirmwareSlotFactViewModel)cell.DataContext!).Label == "TP SVN");
            Border versionCell = cells.Single(static cell => ((FirmwareSlotFactViewModel)cell.DataContext!).Label == "TP Version");
            Assert.DoesNotContain("warning", svnCell.Classes);

            TextBlock value = Assert.Single(svnCell.GetVisualDescendants().OfType<TextBlock>(),
                static block => block.Classes.Contains("firmwareSlotFactValue"));
            ShapePath note = Assert.Single(svnCell.GetVisualDescendants().OfType<ShapePath>(),
                static path => path.IsEffectivelyVisible);
            Assert.Contains("firmwareSlotFactNoteIcon", note.Classes);
            Assert.Contains("noteWarning", note.Classes);
            Assert.Equal(12, note.Bounds.Width, 1);
            Assert.Equal(12, note.Bounds.Height, 1);
            Point valueOrigin = value.TranslatePoint(default, svnCell)!.Value;
            Point noteOrigin = note.TranslatePoint(default, svnCell)!.Value;
            Assert.InRange(noteOrigin.X - (valueOrigin.X + value.Bounds.Width), 3, 5);
            Assert.InRange(
                noteOrigin.Y + (note.Bounds.Height / 2) - (valueOrigin.Y + (value.Bounds.Height / 2)), -1, 1);
            Assert.True(FocusToolTipBehavior.GetIsEnabled(note));
            Assert.True(note.Focusable);
            Assert.Equal(PlacementMode.BottomEdgeAlignedLeft, ToolTip.GetPlacement(note));
            Assert.Equal(360, Assert.IsType<ToolTip>(ToolTip.GetTip(note)).MaxWidth);
            Assert.True(note.Focus(NavigationMethod.Tab));
            Dispatcher.UIThread.RunJobs();
            Assert.True(ToolTip.GetIsOpen(note));

            ShapePath versionIcon = Assert.Single(versionCell.GetVisualDescendants().OfType<ShapePath>(),
                static path => path.IsEffectivelyVisible);
            Assert.Contains("firmwareSlotFactStateIcon", versionIcon.Classes);
            Assert.DoesNotContain("firmwareSlotFactNoteIcon", versionIcon.Classes);
            Point versionIconOrigin = versionIcon.TranslatePoint(default, versionCell)!.Value;
            Assert.InRange(versionCell.Bounds.Width - 16 - (versionIconOrigin.X + versionIcon.Bounds.Width), -1, 1);
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>
    /// Decision 94: the hexadecimal value in the warning line keeps a plain x. Measured on the shaped glyphs of the
    /// production tooltip text: "0x03" is shaped with the nominal glyphs of the UI font (contextual alternates and
    /// ligatures off), never with the glyphs the same font produces with its default features, which substitute the
    /// x between digits (the control proves the measurement can fail).
    /// </summary>
    [AvaloniaFact]
    public void TpSvnWarningLineShapesTheHexValueWithAPlainX()
    {
        var slot = new FirmwareSlotViewModel("tp", "TP BIN", "TP input", FirmwareSlotKind.Tp)
        {
            FilePath = @"C:\firmware\nt51923-tp-input.bin",
        };
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "Verified");
        slot.SetFirmwareFacts(
        [
            new("TP SVN", "C3 80 13 30")
            {
                Note = new FirmwareSlotFactNote(
                    [new("Flags:", "LOCAL_BUILD, DIFF_EXIST"), new("Revision:", "801330", IsTechnicalValue: true)],
                    [new("Undefined flag bits 0x03. InsertPID.py may be faulty.")]),
            },
        ]);
        var card = new FirmwareSlotCard
        {
            BrowseLabel = "Browse",
            ClearSelectionLabel = "Clear selected file",
            DataContext = slot,
            VerticalAlignment = VerticalAlignment.Top,
            Width = 1180,
        };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.Width = 1180;
        host.Height = 360;

        try
        {
            host.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            ShapePath note = Assert.Single(card.GetVisualDescendants().OfType<ShapePath>(),
                static path => path.IsEffectivelyVisible && path.Classes.Contains("firmwareSlotFactNoteIcon"));
            Assert.True(note.Focus(NavigationMethod.Tab));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Assert.True(ToolTip.GetIsOpen(note));

            ToolTip tip = Assert.IsType<ToolTip>(ToolTip.GetTip(note));
            TextBlock warning = Assert.Single(tip.GetVisualDescendants().OfType<TextBlock>(),
                static block => block.Classes.Contains("firmwareSlotFactNoteWarningText"));
            Assert.Equal("Undefined flag bits 0x03. InsertPID.py may be faulty.", warning.Text);
            var typeface = new Typeface(warning.FontFamily, warning.FontStyle, warning.FontWeight, warning.FontStretch);
            using var plain = new TextLayout("0x03", typeface, warning.FontSize, Brushes.Black,
                fontFeatures: FontFeatureCollection.Parse("-calt,-liga"));
            using var substituted = new TextLayout("0x03", typeface, warning.FontSize, Brushes.Black);
            ushort[] plainGlyphs = ShapedGlyphs(plain);
            ushort[] substitutedGlyphs = ShapedGlyphs(substituted);
            Assert.Equal(4, plainGlyphs.Length);
            Assert.NotEqual(plainGlyphs, substitutedGlyphs);

            ushort[] production = ShapedGlyphs(warning.TextLayout);
            Assert.True(ContainsRun(production, plainGlyphs), "The warning text must shape 0x03 with the plain x glyph.");
            Assert.False(ContainsRun(production, substitutedGlyphs), "The warning text must not substitute the x in 0x03.");
        }
        finally
        {
            host.Close();
        }

        static ushort[] ShapedGlyphs(TextLayout layout)
        {
            return [.. layout.TextLines.SelectMany(static line => line.TextRuns).OfType<ShapedTextRun>()
                .SelectMany(static run => run.GlyphRun.GlyphInfos).Select(static info => info.GlyphIndex)];
        }

        static bool ContainsRun(ushort[] glyphs, ushort[] run)
        {
            for (int start = 0; start + run.Length <= glyphs.Length; start++)
            {
                if (glyphs.AsSpan(start, run.Length).SequenceEqual(run))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>The tooltip leads with the warning line (triangle and warning text), then Flags and Revision.</summary>
    [AvaloniaFact]
    public void TpSvnValueNoteTooltipLeadsWithTheWarningLine()
    {
        var host = new Window();
        host.Resources.MergedDictionaries.Add(new ResourceInclude(ProductionSharedTemplatesUri)
        {
            Source = ProductionSharedTemplatesUri,
        });
        var note = new FirmwareSlotFactNote(
            [new("Flags:", "none"), new("Revision:", "000000", IsTechnicalValue: true)],
            [new("No SVN stamp. InsertPID.py may not have run.")]);
        var content = new ContentControl
        {
            Content = note,
            ContentTemplate = (IDataTemplate)host.FindResource("FirmwareSlotFactNoteTooltipTemplate")!,
        };
        host.Content = content;

        try
        {
            host.Show();
            Dispatcher.UIThread.RunJobs();

            string[] lines = [.. content.GetVisualDescendants().OfType<TextBlock>()
                .Where(static block => block.IsEffectivelyVisible).Select(static block => block.Text ?? string.Empty)];
            Assert.Equal(["No SVN stamp. InsertPID.py may not have run.", "Flags:", "none", "Revision:", "000000"], lines);
            TextBlock warning = content.GetVisualDescendants().OfType<TextBlock>().First();
            Assert.Contains("firmwareSlotFactNoteWarningText", warning.Classes);
            ShapePath triangle = Assert.Single(content.GetVisualDescendants().OfType<ShapePath>());
            Assert.Contains("firmwareSlotFactNoteWarningIcon", triangle.Classes);
            Assert.Equal(FirmwareSlotFactViewModel.WarningIconPathData, note.Warnings[0].IconPathData);
        }
        finally
        {
            host.Close();
        }
    }
}
