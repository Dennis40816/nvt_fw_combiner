// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Renders the existing segment's details without interpreting their text or firmware meaning.</summary>
internal sealed class OutputLayoutDetails
{
    internal OutputLayoutDetails(OutputLayoutPanel owner, OutputLayoutPanelText text,
        OutputLayoutPanelPresenter.EntryKey key, MemoryCoverageSegmentViewModel segment, bool technicalExpanded)
    {
        OutputLayoutPanelModel model = owner.Model ?? throw new InvalidOperationException("Details require a display publication.");
        StackPanel content = OutputLayoutVisuals.Stack();
        if (model.CanRequestIssue && segment.DiagnosticSeverity == MemoryDiagnosticSeverity.Warning)
        {
            var action = new Button
            {
                Classes = { "semanticAction", "command" },
                Content = text.ShowWarning,
                Command = new RelayCommand(() => owner.RequestIssue(segment)),
                Tag = new OutputLayoutPanelPresenter.FocusAnchor(key, null, "issue"),
            };
            OutputLayoutVisuals.Name(action, text.ShowWarning, "OutputLayout.ShowIssue");
            content.Children.Add(action);
        }
        content.Children.Add(OutputLayoutVisuals.Fact(text.Shell.MemoryTargetAddressLabel, segment.AddressRangeLabel));
        content.Children.Add(OutputLayoutVisuals.Fact(text.Shell.SizeLabel, segment.SizeValue));
        content.Children.Add(OutputLayoutVisuals.Fact(segment.SourceFieldLabel, segment.SourceLabel));
        content.Children.Add(OutputLayoutVisuals.Text(segment.PreservationSummary, "captionText"));
        StackPanel technical = OutputLayoutVisuals.Stack();
        AddTechnical(technical, segment, text.Shell, model.RangeFor(segment)?.Space);
        IEnumerable<MemoryCoverageSegmentViewModel> members = segment.DisplayParts.Count == 0 ? [segment] : segment.DisplayParts;
        string[] logicalGroups = [.. members.Select(static part => part.LogicalCoverageGroupId).OfType<string>()];
        foreach (MemoryCoverageSegmentViewModel supporting in model.CoverageRows.Where(row =>
            !row.IsPrimaryContent && row.LogicalCoverageGroupId is { } identity && logicalGroups.Contains(identity, StringComparer.Ordinal)))
        {
            technical.Children.Add(OutputLayoutVisuals.Text(supporting.DisplayTitle, "bodyEmphasisText"));
            AddTechnical(technical, supporting, text.Shell, model.RangeFor(supporting)?.Space);
        }
        Technical = new(OutputLayoutVisuals.Text(text.Shell.MemoryProcessingDetailsLabel, "bodyEmphasisText"),
            technical, text.Shell.MemoryProcessingDetailsLabel, "OutputLayout.Technical", technicalExpanded);
        Technical.Header.Tag = new OutputLayoutPanelPresenter.FocusAnchor(key, null, "technical");
        content.Children.Add(new Border { Classes = { "outputLayoutDivider" } });
        content.Children.Add(Technical.Root);
        Root = OutputLayoutVisuals.Indent(new Border { Classes = { "outputLayoutDetail" }, Child = OutputLayoutVisuals.Indent(content) });
    }

    internal Control Root { get; }
    internal OutputLayoutDisclosure Technical { get; }

    private static void AddTechnical(StackPanel target, MemoryCoverageSegmentViewModel segment, ShellTextResources text, string? displaySpace)
    {
        target.Children.Add(OutputLayoutVisuals.Fact(text.MemoryAddressSpaceLabel, displaySpace ?? segment.AddressSpaceId));
        foreach (MemoryRegionFact fact in segment.ProcessingFacts) { target.Children.Add(OutputLayoutVisuals.Fact(fact.Label, fact.Value)); }
        target.Children.Add(OutputLayoutVisuals.Text(segment.PreservationSummary, "captionText"));
        if (segment.HasAdditionalDetail) { target.Children.Add(OutputLayoutVisuals.Text(segment.Detail, "captionText")); }
        foreach (DiffDlmPreservationDetailViewModel detail in segment.PreservationDetails)
        {
            StackPanel part = OutputLayoutVisuals.Stack("NfcSpace2");
            part.Children.Add(OutputLayoutVisuals.Text(detail.IcLabel, "bodyEmphasisText"));
            part.Children.Add(OutputLayoutVisuals.Text(detail.BlockLabel, "technicalValue"));
            part.Children.Add(OutputLayoutVisuals.Text(detail.ArtifactRangeLabel, "technicalValue"));
            part.Children.Add(OutputLayoutVisuals.Text(detail.FlashRangeLabel, "technicalValue"));
            part.Children.Add(OutputLayoutVisuals.Text(detail.DispositionLabel, "captionText"));
            target.Children.Add(part);
        }
    }
}
