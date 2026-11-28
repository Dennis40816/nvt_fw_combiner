// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Localized approved wording and faithful display of immutable facts supplied by the host.</summary>
public sealed class RecalculationConfirmFactsTests
{
    /// <summary>Missing coverage uses the approved sentence in each supported language.</summary>
    [AvaloniaTheory]
    [InlineData(false, "Decided by Combiner. The IC file does not declare it yet.")]
    [InlineData(true, "涵蓋範圍由 Combiner 決定，IC 檔尚未宣告")]
    public void UnknownCoverageShowsTheApprovedSentence(bool chinese, string expected)
    {
        using var host = new RecalculationConfirmTestHost(expanded: true, chinese: chinese);
        Assert.Equal(expected, host.Find<TextBlock>("CoverageText").Text);
        Assert.DoesNotContain("monoText", host.Find<TextBlock>("CoverageText").Classes);
    }

    /// <summary>Declared ranges retain their half-open coordinates, order and localized separator.</summary>
    [AvaloniaTheory]
    [InlineData(false, "[0x00200, 0x00300), [0x01000, 0x04000)")]
    [InlineData(true, "[0x00200, 0x00300)、[0x01000, 0x04000)")]
    public void DeclaredCoverageShowsTheSuppliedRanges(bool chinese, string expected)
    {
        using var host = new RecalculationConfirmTestHost(expanded: true, chinese: chinese,
            coverage: [new ByteRange(0x200, 0x100), new ByteRange(0x1000, 0x3000)]);
        Assert.Equal(expected, host.Find<TextBlock>("CoverageText").Text);
        Assert.Contains("monoText", host.Find<TextBlock>("CoverageText").Classes);
    }

    /// <summary>A host with no published CRC storage location says so instead of leaving the row empty.</summary>
    [AvaloniaTheory]
    [InlineData(false, "Not declared")]
    [InlineData(true, "未宣告")]
    public void UndeclaredStorageSaysSo(bool chinese, string expected)
    {
        using var host = new RecalculationConfirmTestHost(expanded: true, chinese: chinese, offsets: [],
            includeSegments: false);
        Assert.Equal(expected, host.Find<TextBlock>("StorageOffsets").Text);
        Assert.DoesNotContain("monoText", host.Find<TextBlock>("StorageOffsets").Classes);
    }

    /// <summary>Omitting covering segments removes the complete caution box.</summary>
    [AvaloniaFact]
    public void NoCoveringSegmentsMeansNoWarningBox()
    {
        using var host = new RecalculationConfirmTestHost(expanded: true, includeSegments: false);
        Assert.False(host.Find<Border>("CoveringSegmentsWarning").IsEffectivelyVisible);
        Assert.Empty(host.Model.CoveringSegments);
    }

    /// <summary>Both languages preserve the exact approved title, body, note, actions and numeric formatting.</summary>
    [AvaloniaTheory]
    [InlineData(false, "Recalculate header and CRC before output?", "0x23C, 0x24C, 0x26C, 0x27C",
        "Your choice is saved in this Saved Rule and preselected at the next build.", "Cancel", "Write without recalculating", "Recalculate and output")]
    [InlineData(true, "重算 header 與 CRC 再輸出？", "0x23C、0x24C、0x26C、0x27C",
        "你的選擇會存進這個 Saved Rule，下次建置時預選。", "取消", "不重算，照寫輸出", "重算並輸出")]
    public void RecalculationTextUsesTheApprovedLanguage(bool chinese, string title, string offsets,
        string note, string cancel, string skip, string recalculate)
    {
        using var host = new RecalculationConfirmTestHost(expanded: true, chinese: chinese);
        Assert.Equal(title, host.Find<TextBlock>("DialogTitle").Text);
        Assert.Equal(offsets, host.Find<TextBlock>("StorageOffsets").Text);
        Assert.Equal(note, host.Find<TextBlock>("SavedChoiceNote").Text);
        Assert.Equal(cancel, host.Find<Button>("CancelButton").Content);
        Assert.Equal(skip, host.Find<Button>("SkipButton").Content);
        Assert.Equal(recalculate, host.Find<Button>("RecalculateButton").Content);
        Assert.Equal("1.3.0", host.Find<TextBlock>("NfcVersion").Text);
        Assert.Equal("1.13.0", host.Find<TextBlock>("LegacyVersion").Text);
        Assert.Equal("NT51927BASED_GEN_CRC_MODE · CRC32", host.Find<TextBlock>("CommandMode").Text);
    }

    /// <summary>No-rule states use their own approved title, warning sentence and two actions.</summary>
    [AvaloniaTheory]
    [InlineData(false, "This IC has no recalculation rule. The output is not recalculated.",
        "The header and CRC may not match the new content. Flashing or the boot check may fail.", "Cancel", "Write output as is")]
    [InlineData(true, "這個 IC 沒有重算規則，輸出不會重算",
        "header 與 CRC 可能和新內容對不上，燒錄或開機檢查可能失敗。", "取消", "照寫輸出")]
    public void NoRuleTextUsesTheApprovedLanguage(bool chinese, string title, string detail, string cancel, string write)
    {
        using var host = new RecalculationConfirmTestHost(canRecalculate: false, chinese: chinese);
        Assert.Equal(title, host.Find<TextBlock>("DialogTitle").Text);
        Assert.Equal(detail, host.Find<TextBlock>("NoRuleText").Text);
        Assert.Equal(cancel, host.Find<Button>("NoRuleCancelButton").Content);
        Assert.Equal(write, host.Find<Button>("WriteButton").Content);
    }

    /// <summary>Each warning uses the supplied segment and covered-offset count in the approved language order.</summary>
    [AvaloniaTheory]
    [InlineData(false, "Your segment [0x00200, 0x00300) covers 4 CRC words: 0x23C, 0x24C, 0x26C, 0x27C. Recalculation overwrites your bytes there.")]
    [InlineData(true, "你的段落 [0x00200, 0x00300) 蓋到 0x23C、0x24C、0x26C、0x27C 這 4 個 CRC，重算會覆蓋你寫的值。")]
    public void WarningUsesTheApprovedSentenceAndHostFacts(bool chinese, string expected)
    {
        using var host = new RecalculationConfirmTestHost(expanded: true, chinese: chinese);
        TextBlock warning = Assert.Single(host.Find<Border>("CoveringSegmentsWarning").GetVisualDescendants().OfType<TextBlock>());
        Assert.Equal(expected, RecalculationConfirmTestHost.InlineText(warning));
    }

    /// <summary>Expanded explanations retain every approved heading and the complete reason for recalculation.</summary>
    [AvaloniaTheory]
    [InlineData(false, "Details", "Why recalculate", "What is recalculated", "Versions",
        "After a segment is replaced, the header size, checksums and CRC no longer match the new content. Flashing or the boot check may fail.")]
    [InlineData(true, "詳細說明", "為什麼要重算", "重算範圍", "版本",
        "換掉段落後，header 的大小、檢查碼與 CRC 會和新內容對不上，\n燒錄或開機檢查可能失敗。")]
    public void ExpandedExplanationsUseTheApprovedText(bool chinese, string details, string why, string range, string versions, string reason)
    {
        using var host = new RecalculationConfirmTestHost(expanded: true, chinese: chinese);
        TextBlock label = Assert.Single(host.Find<global::Avalonia.Controls.Primitives.ToggleButton>("DetailsButton")
            .GetVisualDescendants().OfType<TextBlock>());
        Assert.Equal(details, label.Text);
        // The checked toggle must not repaint its label with the theme's checked foreground.
        global::Avalonia.Controls.Shapes.Path chevron = host.Find<global::Avalonia.Controls.Primitives.ToggleButton>("DetailsButton")
            .GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>().First(path => path.IsEffectivelyVisible);
        Assert.Equal(chevron.Stroke, label.Foreground);
        Assert.Equal([why, range, versions], host.Find<Border>("DetailsPanel").GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.Classes.Contains("panelTitle")).Select(text => text.Text));
        Assert.Equal(reason, host.Find<TextBlock>("WhyText").Text);
    }

    /// <summary>The first outcome is terminal before the host regains control and invokes another command.</summary>
    [Fact]
    public void ReentrantHostCannotReportASecondOutcome()
    {
        var outcomes = new List<RecalculationConfirmOutcome>();
        RecalculationConfirmModel? model = null;
        model = RecalculationConfirmTestHost.Example(outcome =>
        {
            outcomes.Add(outcome);
            RecalculationConfirmModel current = model!;
            current.CancelCommand.Execute(null);
            current.WriteCommand.Execute(null);
        });
        model.RecalculateCommand.Execute(null);
        Assert.Equal(RecalculationConfirmOutcome.RecalculateAndOutput, Assert.Single(outcomes));
    }

    /// <summary>The immutable snapshot owns every caller collection, including nested covered-offset lists.</summary>
    [Fact]
    public void ModelOwnsItsInputCollectionsAndDoesNotInferCoverage()
    {
        var offsets = new List<long> { 0x77, 0x88 };
        var coverage = new List<ByteRange> { new(0, 0x100) };
        var covered = new List<long> { 0x77 };
        var segments = new List<RecalculationConfirmSegment> { new(new ByteRange(0x40, 0x40), covered) };
        var model = new RecalculationConfirmModel("arbitrary badge", true, offsets, coverage, segments,
            "9.8.7", "6.5.4", "host-mode", "host-algorithm", static _ => { });
        offsets.Clear();
        coverage.Clear();
        covered.Clear();
        segments.Clear();
        Assert.Equal([0x77L, 0x88L], model.CrcStorageOffsets);
        Assert.Equal([new ByteRange(0, 0x100)], model.CrcCoverageRanges);
        RecalculationConfirmSegment segment = Assert.Single(model.CoveringSegments);
        Assert.Equal(new ByteRange(0x40, 0x40), segment.Range);
        Assert.Equal([0x77L], segment.CrcOffsets);
        Assert.Equal("arbitrary badge", model.IcName);
        Assert.Equal("host-mode · host-algorithm", model.ModeText);
        Assert.Equal(RecalculationConfirmChoice.Recalculate, model.PreselectedChoice);
        Assert.Equal("Recalculate header and CRC before output?", model.Title);
    }

    /// <summary>The CRC algorithm name is left out while the host does not publish it.</summary>
    [Fact]
    public void AnUnpublishedAlgorithmNameIsOmitted()
    {
        var model = new RecalculationConfirmModel("NT51927", canRecalculate: true, [0x23C], null, [],
            "1.3.0", "1.13.0", "host-mode", null, _ => { });

        Assert.Equal("host-mode", model.ModeText);
    }

    /// <summary>Multiple supplied segments remain separate and retain host ordering and their own counts.</summary>
    [AvaloniaFact]
    public void MultipleWarningsRetainTheHostOrderAndOffsets()
    {
        using var host = new RecalculationConfirmTestHost();
        var model = new RecalculationConfirmModel("IC", true, [0x250, 0x260, 0x450], null,
            [new(new ByteRange(0x400, 0x100), [0x450]), new(new ByteRange(0x200, 0x100), [0x250, 0x260])],
            "1", "2", "mode", "CRC", host.Outcomes.Add);
        host.Model.CancelCommand.Execute(null);
        model.ToggleDetailsCommand.Execute(null);
        host.Modal.Model = model;
        host.Layout();
        string[] warnings = [.. host.Find<Border>("CoveringSegmentsWarning").GetVisualDescendants().OfType<TextBlock>()
            .Select(RecalculationConfirmTestHost.InlineText)];
        Assert.Equal(
            ["Your segment [0x00400, 0x00500) covers 1 CRC word: 0x450. Recalculation overwrites your bytes there.",
             "Your segment [0x00200, 0x00300) covers 2 CRC words: 0x250, 0x260. Recalculation overwrites your bytes there."], warnings);
    }
}
